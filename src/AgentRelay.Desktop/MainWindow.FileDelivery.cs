using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AgentRelay.Core;

namespace AgentRelay.Desktop;

public partial class MainWindow
{
    private readonly HashSet<string> sendingTasks = [];

    private string FileDeliveryHint(TaskRecord task, HandoffProgress progress, string recipient) => progress.Delivery switch
    {
        _ when task.State == TaskState.Paused => task.PauseMessage!,
        "cli_in_flight" or "agent_working" => string.Format(CultureInfo.CurrentUICulture,T("Waiting for {0} to finish and write its receipt. Do not send the same request again."),recipient),
        "cli_unconfirmed" => task.CliFileError ?? T("CLI delivery is unconfirmed. Inspect the target session before sending again."),
        "cli_auto" => string.Format(CultureInfo.CurrentUICulture,T("Relay sends this turn to {0} through CLI automatically."),recipient),
        "cli_ready" => string.Format(CultureInfo.CurrentUICulture,T("CLI is ready for {0}. Send through CLI, or copy the prompt manually."),recipient) +
            (CliBlockMessage(task) is { } blocked ? "\n" + blocked : ""),
        _ => string.Format(CultureInfo.CurrentUICulture,T("Next: copy the prompt to {0}. Relay updates automatically when the Agent writes its receipt."),recipient)
    };

    private string? CliBlockMessage(TaskRecord task) => service.CliExtension(task.AwaitingAgentId ?? task.RecipientId)?.Block?.Message;

    private void AddFileDeliveryAction(Panel actions, TaskRecord task, HandoffProgress progress)
    {
        if (!demo && service.CanSendFileHandoff(task) && !sendingTasks.Contains(task.TaskId))
            Add(actions,"Send via bridge",()=>{StartCliSend(task.TaskId);return Task.CompletedTask;});
        if (progress.Delivery == "cli_unconfirmed")
            Add(actions,"Not received · Allow resending",()=>
            {
                if (MessageBox.Show(this,T("Check the target conversation first. Allow one more CLI send only if this turn did not arrive; otherwise the Agent may do the work twice."),
                        T("Not received · Allow resending"),MessageBoxButton.OKCancel,MessageBoxImage.Warning)!=MessageBoxResult.OK)
                { reviewCancelled=true; return Task.CompletedTask; }
                service.AllowFileHandoffResend(task.TaskId,"User confirmed the turn did not arrive",true);
                return Task.CompletedTask;
            });
    }

    private void StartAutoDeliveries()
    {
        if (demo) return;
        foreach (var id in service.AutoDeliveryCandidates()) StartCliSend(id);
    }

    /// <summary>Print-mode CLIs return only when the Agent finishes its turn, so sends run off the UI thread.</summary>
    private void StartCliSend(string id)
    {
        if (demo || !sendingTasks.Add(id)) return;
        var prompt = FilePrompt(service.Status(id).Task);
        StatusText.Text = T("Sending through CLI…");
        _ = Task.Run(async () =>
        {
            string? failure = null;
            // The send records its own outcome in the ledger; only refusals before launch need to be shown here.
            try { await service.SendFileHandoffAsync(id, prompt); }
            catch (Exception error) when (error is RelayException or IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
            { failure = error is RelayException relay ? DesktopLocalizedContent.SetupError(relay, preferences.Language) : error.Message; }
            await Dispatcher.InvokeAsync(() =>
            {
                sendingTasks.Remove(id);
                if (failure is not null) { ErrorText.Text = failure; ErrorBanner.Visibility = Visibility.Visible; }
                QueueRefresh();
            });
        });
    }
}
