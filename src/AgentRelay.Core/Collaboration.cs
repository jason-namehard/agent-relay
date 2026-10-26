namespace AgentRelay.Core;

public sealed record CollaborationOptions(string AuthorId, string? Criteria = null, int MaxReviewRounds = 5);
public sealed record FileHandoffOptions(CollaborationOptions? Collaboration = null, bool AutoDeliver = false,
    int? TurnTimeoutMinutes = null);

public sealed record CollaborationStep(int Turn, string AgentId, string Phase, string Outcome, DateTimeOffset At,
    string? ResultPath = null, string[]? OpenItems = null, string? Note = null);

public sealed class CollaborationRecord
{
    public required string AuthorId { get; set; }
    public required string ReviewerId { get; set; }
    public string? Criteria { get; set; }
    public int MaxReviewRounds { get; set; }
    // implement → review → (revise → review)* → accept → delivered
    public required string Phase { get; set; }
    public int ReviewRound { get; set; }
    public string? SubmissionPath { get; set; }
    public string? ApprovedSubmissionPath { get; set; }
    public string[] OpenItems { get; set; } = [];
    public List<CollaborationStep> History { get; set; } = [];
    public string? Deliverable { get; set; }
}

/// <summary>What the board shows: who holds the task, how the turn travels and whether the user is needed.</summary>
public sealed record HandoffProgress(string? CurrentAgentId, string? Phase, string WaitingOn, string Delivery,
    int Turn, int? MaxTurns, int? ReviewRound, int? MaxReviewRounds, string? NextIfPassed, string? NextIfChanged,
    string? PauseReason, string? PauseMessage, bool Finished, string? Completion);

public sealed partial class RelayService
{
    public const int MaxReviewRounds = 20;

    internal static string[] AllowedOutcomes(string phase) => phase switch
    {
        "implement" or "revise" => ["submitted"],
        "review" => ["changes_requested", "approved"],
        "accept" => ["accepted", "submitted"],
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown collaboration phase.")
    };

    public TaskView ConfigureFileHandoff(string taskId, FileHandoffOptions options) => store.Write(s =>
    {
        var task = s.Task(taskId);
        if (!task.IsFileHandoff || task.State != TaskState.AwaitingManual || task.ManualTurn != 0 ||
            task.ManualReceiptJson is not null || task.CliFileTurn is not null || task.HandoffPlan is not null)
            throw new RelayException("handoff_options_started", "协作方式只能在首次投递和回执之前设置。");
        if (options.TurnTimeoutMinutes is < 1 or > 43200)
            throw new RelayException("handoff_turn_timeout", "单轮时限为 1–43200 分钟。");
        if (options.Collaboration is { } collaboration)
        {
            if (collaboration.AuthorId != task.RequesterId && collaboration.AuthorId != task.RecipientId)
                throw new RelayException("collaboration_author", "作者必须是本任务的发送方或接收方。");
            if (collaboration.MaxReviewRounds is < 1 or > MaxReviewRounds)
                throw new RelayException("collaboration_rounds", $"验收轮数为 1–{MaxReviewRounds}。");
            var reviewer = collaboration.AuthorId == task.RequesterId ? task.RecipientId : task.RequesterId;
            var reviewFirst = task.RecipientId == reviewer;
            task.Collaboration = new CollaborationRecord
            {
                AuthorId = collaboration.AuthorId, ReviewerId = reviewer,
                Criteria = string.IsNullOrWhiteSpace(collaboration.Criteria) ? null : collaboration.Criteria.Trim(),
                MaxReviewRounds = collaboration.MaxReviewRounds,
                Phase = reviewFirst ? "review" : "implement", ReviewRound = reviewFirst ? 1 : 0,
                // When the sender already holds the work, the original request file is the first submission.
                SubmissionPath = reviewFirst ? task.RequestPath : null
            };
        }
        task.AutoDeliver = options.AutoDeliver;
        task.TurnTimeoutMinutes = options.TurnTimeoutMinutes;
        task.DeadlineAt = Now + TurnTimeout(s, task);
        s.Save(task);
        s.Event(task, "handoff.configured", "operator", Now, RelayJson.Write(options));
        return ManualView(task);
    });

    public TaskView ContinueFileHandoff(string taskId, string? note = null, int extraRounds = 0) => store.Write(s =>
    {
        var task = s.Task(taskId);
        if (task.State != TaskState.Paused || task.PauseReason is null || task.PausedFrom is null)
            throw new RelayException("handoff_not_paused", "只有已暂停的交接可以继续。", 5);
        if (extraRounds is < 0 or > MaxReviewRounds)
            throw new RelayException("handoff_extra_rounds", $"追加轮数为 0–{MaxReviewRounds}。");
        var reason = task.PauseReason;
        switch (reason)
        {
            case "round_limit":
                if (extraRounds < 1) throw new RelayException("handoff_extra_rounds", "轮数已用完，继续前请追加至少 1 轮。");
                if (task.Collaboration is { } collaboration) collaboration.MaxReviewRounds += extraRounds;
                else task.MaxTurns = (task.MaxTurns ?? MaxManualTurns) + extraRounds;
                break;
            case "agent_failed":
                // The failed turn's delivery is spent; the retry is a new turn for the same Agent.
                task.ManualTurn++;
                break;
        }
        if (!string.IsNullOrWhiteSpace(note))
            task.ManualNextAction = "用户说明：" + note.Trim() + (string.IsNullOrWhiteSpace(task.ManualNextAction) ? "" : "\n" + task.ManualNextAction);
        task.State = task.PausedFrom.Value;
        task.PauseReason = null; task.PauseMessage = null; task.PausedFrom = null;
        task.DeadlineAt = Now + TurnTimeout(s, task);
        s.Save(task);
        s.Event(task, "handoff.continued", "operator", Now, RelayJson.Write(new { reason, note, extra_rounds = extraRounds }));
        s.Notify(task, "continued_" + task.ManualTurn + "_" + reason, "用户已决定继续交接。", Now);
        return ManualView(task);
    });

    public HandoffProgress FileHandoffProgress(TaskRecord task)
    {
        if (!task.IsFileHandoff) throw new RelayException("file_handoff_required", "此接口只用于文件或 CLI 工作交接。");
        var finished = Terminal(task.State);
        var current = finished ? null : task.AwaitingAgentId ?? task.RecipientId;
        var collaboration = task.Collaboration;
        var completion = task.State switch
        {
            _ when task.ResumeStatus == "operator_completed" => "operator_completed",
            TaskState.Succeeded => collaboration?.Deliverable is not null ? "delivered" : "completed",
            TaskState.Cancelled => "cancelled",
            TaskState.Failed => "failed",
            TaskState.TimedOut => "timed_out",
            _ => null
        };
        var cliTurn = task.CliFileTurn == task.ManualTurn;
        var delivery = finished || task.State == TaskState.Paused ? "none"
            : cliTurn && task.CliFileStatus is "submitting" ? "cli_in_flight"
            : cliTurn && task.CliFileStatus is "unknown" or "failed" ? "cli_unconfirmed"
            : cliTurn && task.CliFileStatus == "submitted" || task.State is TaskState.Dispatched or TaskState.Acknowledged or TaskState.Running ? "agent_working"
            : CanSendFileHandoff(task) ? task.AutoDeliver && CanAutoDeliver(task) ? "cli_auto" : "cli_ready"
            : "copy_prompt";
        var waiting = finished ? "none" : task.State == TaskState.Paused || delivery is "cli_unconfirmed" or "cli_ready" or "copy_prompt" ? "user" : "agent";
        string? passed = null, changed = null;
        if (collaboration is not null && !finished)
            (passed, changed) = collaboration.Phase switch
            {
                "review" => (collaboration.AuthorId, collaboration.AuthorId),
                "accept" => (null, collaboration.ReviewerId),
                _ => (collaboration.ReviewerId, collaboration.ReviewerId)
            };
        return new(current, collaboration?.Phase, waiting, delivery, task.ManualTurn,
            collaboration is null ? task.MaxTurns ?? MaxManualTurns : null,
            collaboration?.ReviewRound, collaboration?.MaxReviewRounds, passed, changed,
            task.PauseReason, task.PauseMessage, finished, completion);
    }

    private TimeSpan TurnTimeout(StoreSession s, TaskRecord task) => TimeSpan.FromMinutes(
        task.TurnTimeoutMinutes ?? s.Json<RelaySettings>("SELECT json FROM settings WHERE id=1")!.TaskTimeoutMinutes);

    private void PauseFileHandoff(StoreSession s, TaskRecord task, string reason, string message, TaskState resumeAs)
    {
        task.PausedFrom = resumeAs; task.State = TaskState.Paused;
        task.PauseReason = reason; task.PauseMessage = message;
        s.Event(task, "handoff.paused", "relay", Now, reason);
        s.Notify(task, "paused_" + task.ManualTurn + "_" + reason, message, Now);
    }

    private void PauseForTimeout(StoreSession s, TaskRecord task)
    {
        var minutes = (int)TurnTimeout(s, task).TotalMinutes;
        PauseFileHandoff(s, task, "turn_timeout",
            $"本轮已超过设定时限（{minutes} 分钟），已暂停等待你的决定。Agent 可能仍在处理，晚到的回执仍会被接收；也可以继续等待、取消或人工标记完成。",
            task.State);
    }

    private void AdvanceFileHandoff(StoreSession s, TaskRecord task, string next, string? action)
    {
        task.AwaitingAgentId = next; task.ManualTurn++; task.State = TaskState.AwaitingReply;
        task.ManualNextAction = action;
        task.DeadlineAt = Now + TurnTimeout(s, task);
    }

    private void ApplyCollaborationOutcome(StoreSession s, TaskRecord task, CollaborationRecord c, ManualReceipt receipt)
    {
        var items = (receipt.OpenItems ?? []).Select(i => i.Trim()).Where(i => i.Length > 0).ToArray();
        var phase = c.Phase;
        void Record(string outcome, string? result) => c.History.Add(new(receipt.Turn, receipt.Sender, phase, outcome, Now,
            result, items.Length == 0 ? null : items, receipt.NextAction));
        if (receipt.Status == "failed")
        {
            if (receipt.Outcome is not null)
                throw new RelayException("collaboration_outcome", "失败回执不填写 outcome，请在 next_action 说明失败原因。");
            Record("failed", KeepReceiptResult(s, task, receipt.ResultFile)?.Path);
            PauseFileHandoff(s, task, "agent_failed", receipt.NextAction ?? "Agent 报告本轮处理失败，请查看回稿后决定是否让它重试。", TaskState.AwaitingReply);
            return;
        }
        var allowed = AllowedOutcomes(phase);
        if (receipt.Outcome is null || !allowed.Contains(receipt.Outcome))
            throw new RelayException("collaboration_outcome", "当前阶段的 outcome 只能是：" + string.Join(" / ", allowed) + "。");
        if (receipt.NeedsReply is { } needsReply && needsReply != (receipt.Outcome != "accepted"))
            throw new RelayException("manual_receipt_contradiction", "needs_reply 与 outcome 矛盾；持续修改与验收任务以 outcome 为准，可省略 needs_reply。");
        if (items.Length > 0 && receipt.Outcome != "changes_requested")
            throw new RelayException("collaboration_open_items", receipt.Outcome == "approved"
                ? "仍有未解决问题时不能通过验收，请改为 changes_requested。" : "只有要求修改时填写 open_items。");
        var author = c.AuthorId;
        switch (receipt.Outcome)
        {
            case "submitted":
            {
                if (string.IsNullOrWhiteSpace(receipt.ResultFile))
                    throw new RelayException("collaboration_submission", "提交时必须在 result_file 给出真实产物或修改说明文件。");
                var previous = c.SubmissionPath;
                var artifact = KeepReceiptResult(s, task, receipt.ResultFile)!;
                Record("submitted", artifact.Path);
                var unchanged = previous is not null && File.Exists(previous) && SameContent(previous, artifact.Path);
                c.SubmissionPath = artifact.Path; c.ApprovedSubmissionPath = null; c.OpenItems = [];
                c.ReviewRound++; c.Phase = "review";
                AdvanceFileHandoff(s, task, c.ReviewerId, receipt.NextAction ?? "请按验收标准检查作者的最新提交。");
                if (c.ReviewRound > c.MaxReviewRounds)
                    PauseFileHandoff(s, task, "round_limit", $"已用完 {c.MaxReviewRounds} 轮验收，最新提交尚未复验。请决定追加轮数继续、取消或人工标记完成。", TaskState.AwaitingReply);
                else if (unchanged)
                    PauseFileHandoff(s, task, "no_progress", "作者本轮提交的文件与上一版完全相同，自动传递已暂停。请确认是否仍交给验收者复验。", TaskState.AwaitingReply);
                break;
            }
            case "changes_requested":
            {
                if (items.Length == 0)
                    throw new RelayException("collaboration_open_items", "要求修改时必须在 open_items 逐条列出具体未解决问题。");
                var notes = KeepReceiptResult(s, task, receipt.ResultFile);
                var lastReview = c.History.LastOrDefault(h => h.Outcome == "changes_requested");
                Record("changes_requested", notes?.Path);
                var repeated = lastReview?.OpenItems is { } prior && prior.SequenceEqual(items);
                c.OpenItems = items; c.Phase = "revise";
                // Authors revise the submission; review notes are attached only when the reviewer wrote one.
                if (notes is null && c.SubmissionPath is not null) task.ManualCurrentFile = c.SubmissionPath;
                AdvanceFileHandoff(s, task, author, receipt.NextAction ?? "请逐条处理 open_items 中的问题后重新提交。");
                if (repeated)
                    PauseFileHandoff(s, task, "no_progress", "验收者提出的问题与上一轮完全相同，说明上次修改未解决。自动传递已暂停，请判断是否继续。", TaskState.AwaitingReply);
                break;
            }
            case "approved":
            {
                Record("approved", KeepReceiptResult(s, task, receipt.ResultFile)?.Path);
                c.ApprovedSubmissionPath = c.SubmissionPath ?? task.RequestPath; c.OpenItems = []; c.Phase = "accept";
                task.ManualCurrentFile = c.ApprovedSubmissionPath;
                AdvanceFileHandoff(s, task, author, receipt.NextAction ?? "验收已通过。请核对通过的版本，确认接受并写明交付物。");
                break;
            }
            case "accepted":
            {
                if (string.IsNullOrWhiteSpace(receipt.Deliverable))
                    throw new RelayException("collaboration_deliverable", "确认接受时必须在 deliverable 写明最终交付物的位置或交付说明。");
                Record("accepted", KeepReceiptResult(s, task, receipt.ResultFile)?.Path);
                c.Deliverable = receipt.Deliverable.Trim();
                task.State = TaskState.Succeeded;
                s.Event(task, "collaboration.delivered", receipt.Sender, Now, c.Deliverable);
                break;
            }
        }
    }

    private ArtifactRecord? KeepReceiptResult(StoreSession s, TaskRecord task, string? file)
    {
        if (string.IsNullOrWhiteSpace(file)) return null;
        ManualTransfers.RequireFile(file);
        var settings = s.Json<RelaySettings>("SELECT json FROM settings WHERE id=1")!;
        var artifact = KeepManualFile(s, task, file, "result", Now, settings.ArtifactTtlDays);
        task.ResultPath = artifact.Path; task.ManualCurrentFile = artifact.Path;
        return artifact;
    }

    private static bool SameContent(string first, string second)
    {
        if (string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            return true;
        if (new FileInfo(first).Length != new FileInfo(second).Length) return false;
        return File.ReadAllBytes(first).AsSpan().SequenceEqual(File.ReadAllBytes(second));
    }
}
