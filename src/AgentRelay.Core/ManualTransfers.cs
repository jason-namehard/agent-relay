using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentRelay.Core;

public sealed record ManualTransferDraft(string From, string To, string File, string Message = "");
public sealed record ManualReceipt(string TaskId, string AttemptId, string Sender, int Turn, string Status,
    bool NeedsReply, string? ResultFile = null, string? NextAction = null);
public sealed record ManualReceiptError(string TaskId, string Path, string Message);
public sealed record ManualReceiptImportResult(string[] UpdatedTaskIds, ManualReceiptError[] Errors);

public static class ManualTransfers
{
    private static readonly JsonSerializerOptions InputJson = new(RelayJson.Options)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static ManualTransferDraft Parse(string json, IReadOnlyList<AgentRecord> agents)
    {
        var draft = Read<ManualTransferDraft>(json);
        var from = Resolve(draft.From, agents); var to = Resolve(draft.To, agents);
        if (from == to) throw new RelayException("manual_same_agent", "发送方与接收方不能相同。");
        RequireFile(draft.File);
        return draft with { From = from, To = to, File = Path.GetFullPath(draft.File) };
    }

    public static ManualReceipt ParseReceipt(string json) => Read<ManualReceipt>(json);

    private static T Read<T>(string json)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 64 * 1024)
            throw new RelayException("manual_json_too_large", "交接或回执 JSON 不能超过 64 KB。");
        var trimmed = json.Trim();
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith("```", StringComparison.Ordinal))
            trimmed = trimmed[7..^3].Trim();
        try
        {
            return JsonSerializer.Deserialize<T>(trimmed, InputJson) ?? throw new JsonException("Expected JSON object.");
        }
        catch (JsonException error)
        {
            throw new RelayException("manual_json_invalid", "交接或回执 JSON 格式不正确：" + error.Message);
        }
    }

    private static string Resolve(string value, IReadOnlyList<AgentRecord> agents)
    {
        var byId = agents.SingleOrDefault(a => a.AgentId == value);
        if (byId is not null) return byId.AgentId;
        var byName = agents.Where(a => a.DisplayName == value).ToArray();
        return byName.Length switch
        {
            1 => byName[0].AgentId,
            0 => throw new RelayException("manual_agent_unknown", "未找到已登记的 Agent：" + value),
            _ => throw new RelayException("manual_agent_ambiguous", "同名 Agent 不止一个，请使用节点 ID：" + value)
        };
    }

    internal static void RequireFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !System.IO.File.Exists(path))
            throw new RelayException("manual_file_missing", "请提供存在的完整文件路径：" + path);
        using var file = System.IO.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length == 0) throw new RelayException("manual_file_empty", "交接文件或回稿不能为空。");
    }
}

public sealed partial class RelayService
{
    public const int MaxManualTurns = 4;

    public string ManualReceiptPath(TaskRecord task) => Path.Combine(workspace.AttemptDirectory(task.TaskId, task.AttemptId), ".relay", "receipt.json");

    public TaskView CreateFileHandoff(ManualTransferDraft draft)
    {
        var normalized = ManualTransfers.Parse(RelayJson.Write(draft), Agents());
        return store.Write(s =>
        {
            Agent(s, normalized.From); Agent(s, normalized.To);
            var active = s.Query("SELECT json FROM tasks", r => RelayJson.Read<TaskRecord>(r.GetString(0)))
                .FirstOrDefault(t => t.IsFileHandoff && !Terminal(t.State) && t.RequesterId == normalized.From &&
                    t.RecipientId == normalized.To && t.ManualSourceFile == normalized.File);
            if (active is not null) return ManualView(active);
            var now = Now;
            var settings = s.Json<RelaySettings>("SELECT json FROM settings WHERE id=1")!;
            var task = new TaskRecord
            {
                TaskId = Guid.NewGuid().ToString(), AttemptId = Guid.NewGuid().ToString(),
                RequesterId = normalized.From, RecipientId = normalized.To, AdapterId = "manual-file", Mode = "manual",
                RequestPath = "", ResultPath = "", CreatedAt = now, DeadlineAt = now.AddMinutes(settings.TaskTimeoutMinutes),
                State = TaskState.AwaitingManual, IsFileHandoff = true, AwaitingAgentId = normalized.To,
                ManualSourceFile = normalized.File, ManualNextAction = normalized.Message
            };
            var artifact = KeepManualFile(s, task, normalized.File, "request", now, settings.ArtifactTtlDays);
            task.RequestPath = artifact.Path; task.ManualCurrentFile = artifact.Path;
            task.ResultPath = Path.Combine(workspace.AttemptDirectory(task.TaskId, task.AttemptId), "reply.md");
            var control = Path.GetDirectoryName(ManualReceiptPath(task))!;
            Directory.CreateDirectory(control);
            if (OperatingSystem.IsWindows()) File.SetAttributes(control, File.GetAttributes(control) | FileAttributes.Hidden);
            var input = new DispatchRequest(task.RequesterId, task.RecipientId, normalized.Message, "manual", "manual-file", task.TaskId);
            s.Execute("INSERT INTO tasks VALUES($id,$from,$key,$input,$json)", ("$id", task.TaskId), ("$from", task.RequesterId),
                ("$key", task.TaskId), ("$input", RelayJson.Write(input)), ("$json", RelayJson.Write(task)));
            s.Event(task, "task.created", task.RequesterId, now);
            s.Notify(task, "waiting", "请复制交接提示词给接收方，等待读取并回报。", now);
            return ManualView(task);
        });
    }

    public ManualReceiptImportResult ImportManualReceipts()
    {
        var tasks = store.Read(s => s.Query("SELECT json FROM tasks", r => RelayJson.Read<TaskRecord>(r.GetString(0))))
            .Where(t => t.IsFileHandoff && !Terminal(t.State)).ToArray();
        var changed = new List<string>(); var errors = new List<ManualReceiptError>();
        foreach (var task in tasks)
        {
            if (task.DeadlineAt <= Now)
            {
                var expired = store.Write(s =>
                {
                    var current = s.Task(task.TaskId);
                    if (Terminal(current.State) || current.DeadlineAt > Now) return false;
                    ExpireManualTask(s, current);
                    return true;
                });
                if (expired) changed.Add(task.TaskId);
                continue;
            }
            var path = ManualReceiptPath(task);
            if (!File.Exists(path)) continue;
            try
            {
                var json = File.ReadAllText(path);
                if (json == task.ManualReceiptJson) continue;
                var receipt = ManualTransfers.ParseReceipt(json);
                var updated = ApplyManualReceipt(task.TaskId, receipt, json);
                if (updated) changed.Add(task.TaskId);
            }
            catch (Exception error) when (error is RelayException or IOException or UnauthorizedAccessException)
            {
                // Agents may still be publishing an external receipt; report it without changing the ledger.
                errors.Add(new(task.TaskId, path, error.Message));
            }
        }
        return new(changed.ToArray(), errors.ToArray());
    }

    private bool ApplyManualReceipt(string id, ManualReceipt receipt, string json) => store.Write(s =>
    {
        var task = s.Task(id);
        if (Terminal(task.State) || task.ManualReceiptJson == json) return false;
        if (task.ManualReceiptJson is { } prior && RelayJson.Read<ManualReceipt>(prior) == receipt) return false;
        if (receipt.TaskId != task.TaskId || receipt.AttemptId != task.AttemptId)
            throw new RelayException("manual_receipt_identity", "回执的任务或尝试标识不匹配。");
        if (receipt.Turn < task.ManualTurn) return false;
        if (receipt.Turn != task.ManualTurn)
            throw new RelayException("manual_receipt_turn", "回执轮次与当前交接不匹配。");
        if (receipt.Sender != task.AwaitingAgentId)
            throw new RelayException("manual_receipt_sender", "此回执必须由当前等待的 Agent 提交。");
        if (receipt.Status is not ("received" or "working" or "done" or "failed"))
            throw new RelayException("manual_receipt_status", "回执状态应为 received、working、done 或 failed。");
        if (task.DeadlineAt <= Now)
        {
            ExpireManualTask(s, task);
            return true;
        }
        if (receipt.Status is "received" or "working")
        {
            if (receipt.NeedsReply) throw new RelayException("manual_receipt_reply", "只有完成当前工作后才能要求回传。");
            if (receipt.Status == "received" && task.State == TaskState.Running)
                throw new RelayException("manual_receipt_order", "处理中任务不能退回已接收状态。");
            task.State = receipt.Status == "received" ? TaskState.Acknowledged : TaskState.Running;
        }
        else
        {
            if (receipt.Status == "failed" && receipt.NeedsReply)
                throw new RelayException("manual_receipt_reply", "失败回执不能自动开启下一轮。");
            if (receipt.NeedsReply && (string.IsNullOrWhiteSpace(receipt.ResultFile) || string.IsNullOrWhiteSpace(receipt.NextAction)))
                throw new RelayException("manual_receipt_reply", "需要对方继续时，必须给出回稿文件和具体的下一步工作；已确认或寒暄无需回传。");
            if (!string.IsNullOrWhiteSpace(receipt.ResultFile))
            {
                ManualTransfers.RequireFile(receipt.ResultFile);
                var settings = s.Json<RelaySettings>("SELECT json FROM settings WHERE id=1")!;
                var artifact = KeepManualFile(s, task, receipt.ResultFile, "result", Now, settings.ArtifactTtlDays);
                task.ResultPath = artifact.Path; task.ManualCurrentFile = artifact.Path;
            }
            if (receipt.Status == "failed")
            {
                task.State = TaskState.Failed; task.ErrorCode = "manual_agent_failed";
                task.ErrorMessage = receipt.NextAction ?? "Agent 报告处理失败，请检查回执或回稿。";
            }
            else if (!receipt.NeedsReply) task.State = TaskState.Succeeded;
            else if (task.ManualTurn + 1 >= MaxManualTurns)
            {
                task.State = TaskState.Failed; task.ErrorCode = "manual_round_limit";
                task.ErrorMessage = "交接已达到 4 轮上限，已暂停继续传递。请人工判断是否需要另建任务，避免重复往返。";
            }
            else
            {
                task.AwaitingAgentId = receipt.Sender == task.RequesterId ? task.RecipientId : task.RequesterId;
                task.ManualTurn++; task.State = TaskState.AwaitingReply;
                task.ManualNextAction = receipt.NextAction;
            }
        }
        task.ManualReceiptJson = json;
        s.Save(task); s.Event(task, "manual." + receipt.Status, receipt.Sender, Now, "turn=" + receipt.Turn);
        s.Notify(task, "manual_turn_" + receipt.Turn + "_" + receipt.Status,
            task.State == TaskState.AwaitingReply ? "对方需要继续处理，请复制当前交接提示词。" :
            task.State == TaskState.Succeeded ? "本次交接已结束，无需继续传递。" : task.ErrorMessage ?? "交接进度已更新。", Now);
        return true;
    });

    private void ExpireManualTask(StoreSession session, TaskRecord task)
    {
        task.State = TaskState.TimedOut;
        task.ErrorCode = "deadline_exceeded";
        task.ErrorMessage = "交接已超过设定时限，已停止继续等待。请确认双方处理情况，再决定是否新建交接。";
        session.Save(task);
        session.Event(task, "task.timed_out", "relay", Now);
        session.Notify(task, "timed_out", task.ErrorMessage, Now);
    }

    private ArtifactRecord KeepManualFile(StoreSession s, TaskRecord task, string source, string kind, DateTimeOffset now, int ttl)
    {
        var full = Path.GetFullPath(source);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var existing = s.Json<ArtifactRecord>("SELECT json FROM artifacts WHERE path=$path", ("$path", full));
        if (existing is not null && existing.TaskId == task.TaskId && existing.AttemptId == task.AttemptId)
        {
            if (existing.State != "available" || existing.ExpiresAt <= now)
                throw new RelayException("artifact_expired", "此交接文件已过期，请重新生成。");
            return existing;
        }
        var path = full;
        if (!full.StartsWith(workspace.Root + Path.DirectorySeparatorChar, comparison) || existing is not null)
        {
            var directory = workspace.AttemptDirectory(task.TaskId, task.AttemptId);
            Directory.CreateDirectory(directory);
            path = workspace.RequireManaged(Path.Combine(directory, Path.GetFileName(source)));
            if (File.Exists(path)) path = workspace.RequireManaged(Path.Combine(directory,
                Path.GetFileNameWithoutExtension(source) + "-" + Guid.NewGuid().ToString("N")[..8] + Path.GetExtension(source)));
            File.Copy(full, path);
        }
        else path = workspace.RequireManaged(full);
        var artifact = new ArtifactRecord(Guid.NewGuid().ToString(), task.TaskId, task.AttemptId, kind, path, now, now.AddDays(ttl), "available");
        AddArtifact(s, artifact);
        return artifact;
    }

    private TaskView ManualView(TaskRecord task)
    {
        var overdue = !Terminal(task.State) && task.DeadlineAt <= Now;
        var finished = Terminal(task.State);
        return new(task, overdue, finished ? "inspect" : "copy_file_handoff", finished ?
            task.State == TaskState.Succeeded ? "交接已完成，无需继续传递。" : task.ErrorMessage ?? "交接已结束。" :
            $"有文件交接给你。请读取 {Path.Combine(Path.GetDirectoryName(ManualReceiptPath(task))!, "context.json")}，完成其中要求并写回执，无需额外寒暄或确认消息。");
    }
}
