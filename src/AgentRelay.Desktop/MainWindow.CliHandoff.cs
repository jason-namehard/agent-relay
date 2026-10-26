using AgentRelay.Core;

namespace AgentRelay.Desktop;

public partial class MainWindow
{
    private async Task NewCliHandoff()
    {
        if(demo)throw new RelayException("demo_cli_disabled",T("Demo mode blocks real CLI calls. Keep this workspace and switch to normal use to test your registered Agents."));
        if(service.Agents().Count<2)throw new RelayException("agents_required",T("Add at least two Agents first."));
        var dialog=new CliHandoffDialog(this,service,preferences.Language);
        StatusText.Text=T("Waiting for handoff details…");
        if(dialog.ShowDialog()!=true){reviewCancelled=true;return;}
        var request=dialog.Result!;
        var created=service.CreateCliHandoff(request);taskId=created.Task.TaskId;
        if(dialog.OriginBinding=="user_selected")service.BindTaskSession(taskId,request.From,dialog.Origin!);
        if(request.TargetSessionId is not null&&dialog.TargetBinding=="user_selected")service.BindTaskSession(taskId,request.To,dialog.Target!);
        StatusText.Text=T("Sending through CLI…");
        await service.SendFileHandoffAsync(taskId,service.FileHandoffPrompt(service.Status(taskId).Task));
    }
}
