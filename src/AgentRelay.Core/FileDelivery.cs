using System.ComponentModel;

namespace AgentRelay.Core;

public sealed partial class RelayService
{
    public bool CanSendFileHandoff(TaskRecord task)
    {
        if (!task.IsFileHandoff || Terminal(task.State) || task.CliFileTurn == task.ManualTurn ||
            task.State is not (TaskState.AwaitingManual or TaskState.AwaitingReply)) return false;
        var recipient = task.AwaitingAgentId ?? task.RecipientId;
        var sender = recipient == task.RecipientId ? task.RequesterId : task.RecipientId;
        var extension = CliExtension(recipient);
        if (extension is null) return false;
        var binding = TaskSession(task.TaskId, recipient);
        if (binding?.SessionId is not null && extension.Recipe.Resume is null) return false;
        if (binding?.Binding == "ephemeral" || task.ManualTurn > 0 && binding is null) return false;
        return
            ConnectionPermissions().Any(p => p.From == sender && p.To == recipient && p.Decision == "allow");
    }

    public async Task<TaskView> SendFileHandoffAsync(string taskId, string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt)) throw new RelayException("file_prompt_required", "文件交接提示不能为空。");
        var task = Status(taskId).Task;
        if (!CanSendFileHandoff(task)) throw new RelayException("file_cli_unavailable", "当前方向未启用 CLI、本轮已发送，或没有可续接的明确会话。请检查连接图和会话选择，必要时手动传递。");
        var recipient = task.AwaitingAgentId ?? task.RecipientId;
        var extension = CliExtension(recipient)!;
        var binding = TaskSession(taskId, recipient);
        var resume = binding?.SessionId is not null;
        SessionRecord? validated = null;
        if (resume)
        {
            var check = await ValidateDiscoveredSessionAsync(recipient, binding!.SessionId!, cancellationToken);
            if (check.Status != "exists")
                throw new RelayException("session_rebind_required", check.Message ??
                    "无法确认绑定会话仍然存在。请重新选择已有会话；不支持会话发现时仍可手动复制提示词。");
            validated = check.Session;
        }
        var command = resume ? extension.Recipe.Resume! : extension.Recipe.Create;
        if (resume) command = command with { WorkingDirectory = validated?.Cwd ?? binding!.Cwd ?? command.WorkingDirectory };
        if (resume)
        {
            var bound = binding!;
            var context = command.Environment is null ? new Dictionary<string,string>() : new(command.Environment);
            context["AGENT_RELAY_CALLER_AGENT_ID"] = recipient;
            context["AGENT_RELAY_CALLER_SESSION_ID"] = bound.SessionId!;
            context["AGENT_RELAY_CALLER_HOST"] = bound.Host;
            context["AGENT_RELAY_CALLER_CWD"] = command.WorkingDirectory ?? validated!.Cwd ?? Directory.GetCurrentDirectory();
            if (bound.Profile is not null) context["AGENT_RELAY_CALLER_PROFILE"] = bound.Profile;
            command = command with { Environment = context };
        }
        store.Write(s =>
        {
            var current = s.Task(taskId);
            if (current.ManualTurn != task.ManualTurn || current.CliFileTurn == current.ManualTurn ||
                current.State is not (TaskState.AwaitingManual or TaskState.AwaitingReply) ||
                (current.AwaitingAgentId ?? current.RecipientId) != recipient || ReadCliExtension(s,recipient)?.Revision != extension.Revision ||
                ReadTaskSession(s,current,recipient) != binding)
                throw new RelayException("file_cli_changed", "任务已经变化，本次未重复发送。");
            RequirePermission(s,recipient==current.RecipientId?current.RequesterId:current.RecipientId,recipient);
            current.CliFileTurn = current.ManualTurn; current.CliFileStatus = "submitting";
            current.CliFileError = null; current.CliFileSessionId = null; current.Mode = "cli"; current.AdapterId = "cli:" + recipient;
            s.Save(current); s.Event(current, "file.cli_submitting", "operator", Now, resume ? "resume " + binding!.SessionId : "create");
            return true;
        });
        string status; string? session = null; string? error;
        try
        {
            var process = await CliRecipes.InvokeAsync(command, prompt, binding?.SessionId, Home,
                TimeSpan.FromSeconds(Settings().DeliveryTimeoutSeconds), cancellationToken);
            var receipt = CliRecipes.Receipt(process, CliRecipes.OutputFormatFor(extension.Recipe));
            var matching = receipt is not null && (!resume || receipt.Value.SessionId == binding!.SessionId);
            status = matching ? "submitted" : "unknown";
            session = receipt?.SessionId;
            error = matching ? null : resume && receipt is not null
                ? "续接返回了不同的会话 ID，本轮结果未确认。原绑定保持不变，请检查目标会话，不要重复发送。" : process.TimedOut
                ? "CLI 已启动但等待超时，可能已提交。请检查目标会话，不要重复发送。"
                : CliFailureDiagnostics.Explain(process);
        }
        catch (Win32Exception failure)
        {
            status = "failed"; error = "无法启动 CLI：" + failure.Message;
        }
        return store.Write(s =>
        {
            var current = s.Task(taskId);
            if (current.AttemptId == task.AttemptId && status == "submitted")
            {
                var agent = Agent(s, recipient);
                var value = new TaskSessionBinding(taskId, task.AttemptId, task.ManualTurn, recipient, agent.Host,
                    session, validated?.Cwd ?? binding?.Cwd ?? command.WorkingDirectory,
                    validated?.Profile ?? binding?.Profile, validated?.SessionPath ?? binding?.SessionPath,
                    resume ? binding!.Binding : session is null ? "ephemeral" : "created",
                    validated?.Visibility ?? binding?.Visibility ?? "unknown", Now,
                    resume ? "adapter resumed the exact bound session" : session is null
                        ? "CLI completed without a persistent session ID" : "adapter parsed the created session ID");
                SaveTaskSession(s, current, value);
            }
            // A receipt may have finished this turn while the CLI was returning its output.
            if (current.ManualTurn == task.ManualTurn && !Terminal(current.State))
            {
                current.CliFileStatus = status; current.CliFileError = error; current.CliFileSessionId = session;
                if (status == "submitted" && current.State is TaskState.AwaitingManual or TaskState.AwaitingReply)
                    current.State = TaskState.Dispatched;
                s.Save(current); s.Event(current, "file.cli_" + status, "operator", Now, error ?? session ?? "");
            }
            return View(current, s);
        });
    }
}
