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
        return CliExtension(recipient) is { CanCreate: true } &&
            ConnectionPermissions().Any(p => p.From == sender && p.To == recipient && p.Decision == "allow");
    }

    public async Task<TaskView> SendFileHandoffAsync(string taskId, string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt)) throw new RelayException("file_prompt_required", "文件交接提示不能为空。");
        var task = Status(taskId).Task;
        if (!CanSendFileHandoff(task)) throw new RelayException("file_cli_unavailable", "当前方向未启用已验证的 CLI，或本轮已经发送。请检查连接图和会话，必要时手动传递。");
        var recipient = task.AwaitingAgentId ?? task.RecipientId;
        var extension = CliExtension(recipient)!;
        store.Write(s =>
        {
            var current = s.Task(taskId);
            if (current.ManualTurn != task.ManualTurn || current.CliFileTurn == current.ManualTurn ||
                current.State is not (TaskState.AwaitingManual or TaskState.AwaitingReply) ||
                (current.AwaitingAgentId ?? current.RecipientId) != recipient || ReadCliExtension(s,recipient)?.Revision != extension.Revision)
                throw new RelayException("file_cli_changed", "任务已经变化，本次未重复发送。");
            RequirePermission(s,recipient==current.RecipientId?current.RequesterId:current.RecipientId,recipient);
            current.CliFileTurn = current.ManualTurn; current.CliFileStatus = "submitting";
            current.CliFileError = null; current.CliFileSessionId = null; current.Mode = "cli"; current.AdapterId = "cli:" + recipient;
            s.Save(current); s.Event(current, "file.cli_submitting", "operator", Now);
            return true;
        });
        string status; string? session = null; string? error;
        try
        {
            var process = await CliRecipes.InvokeAsync(extension.Recipe.Create, prompt, null, Home,
                TimeSpan.FromSeconds(Settings().DeliveryTimeoutSeconds), cancellationToken);
            var receipt = CliRecipes.Receipt(process, CliRecipes.OutputFormatFor(extension.Recipe));
            status = receipt is not null ? "submitted" : "unknown";
            session = receipt?.SessionId;
            error = receipt is not null ? null : process.TimedOut
                ? "CLI 已启动但等待超时，可能已提交。请检查目标会话，不要重复发送。"
                : "CLI 未返回可识别的结果，请检查目标会话。" + process.Error;
        }
        catch (Win32Exception failure)
        {
            status = "failed"; error = "无法启动 CLI：" + failure.Message;
        }
        return store.Write(s =>
        {
            var current = s.Task(taskId);
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
