using System.Globalization;
using System.Windows.Controls;
using AgentRelay.Core;

namespace AgentRelay.Desktop;

public partial class MainWindow
{
    private string FileDeliveryHint(TaskRecord task, bool processing, string recipient)
    {
        if (processing || task.State == TaskState.Dispatched || task.CliFileTurn == task.ManualTurn && task.CliFileStatus == "submitted")
            return string.Format(CultureInfo.CurrentUICulture,T("Waiting for {0} to finish and write its receipt. Do not send the same request again."),recipient);
        if (task.CliFileTurn == task.ManualTurn && task.CliFileStatus is "unknown" or "failed" or "submitting")
            return task.CliFileError ?? T("CLI delivery is unconfirmed. Inspect the target session before sending again.");
        if (!demo && service.CanSendFileHandoff(task))
            return string.Format(CultureInfo.CurrentUICulture,T("CLI is ready for {0}. Send through CLI, or copy the prompt manually."),recipient);
        return string.Format(CultureInfo.CurrentUICulture,T("Next: copy the prompt to {0}. Relay updates automatically when the Agent writes its receipt."),recipient);
    }

    private void AddFileDeliveryAction(Panel actions, TaskRecord task)
    {
        if (!demo && service.CanSendFileHandoff(task))
            Add(actions,"Send through CLI",async()=>await service.SendFileHandoffAsync(task.TaskId,FilePrompt(task)));
    }

    private async Task AutoDeliverFile(TaskRecord task)
    {
        if (demo || !service.CanSendFileHandoff(task)) return;
        StatusText.Text=T("Sending through CLI…");
        await service.SendFileHandoffAsync(task.TaskId,FilePrompt(task));
    }
}
