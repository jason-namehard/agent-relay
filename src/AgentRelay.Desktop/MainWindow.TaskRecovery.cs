using System.Windows;
using AgentRelay.Core;

namespace AgentRelay.Desktop;

public partial class MainWindow
{
    private Task ConfirmTaskCancellation(string id)
    {
        if(MessageBox.Show(this,T("Cancel this task now? Relay will end the task immediately, stop waiting for receipts and stop scheduling further deliveries, even if a delivery is in progress. Work already started in the host may continue and must be stopped there separately. Existing files will be kept."),
            T("Cancel task"),MessageBoxButton.OKCancel,MessageBoxImage.Warning)!=MessageBoxResult.OK)
        {reviewCancelled=true;return Task.CompletedTask;}
        service.Cancel(id,"Cancelled by user");return Task.CompletedTask;
    }

    private async Task RecheckDesktopBridge(string targetAgentId)
    {
        var result=await Task.Run(()=>service.RecheckDesktopBridgeAsync(targetAgentId));
        if(!result.BlockCleared&&service.CliExtension(targetAgentId)?.Block is not null)
            throw new RelayException("bridge_not_restored",result.Message+"\n"+string.Join("\n",result.Checks.Where(c=>c.Id!="block"&&c.Status=="fail").Select(c=>c.Message)));
        MessageBox.Show(this,result.Message,T("Bridge recovery result"),MessageBoxButton.OK,MessageBoxImage.Information);
    }

    private bool CanRecheckDesktopBridge(string id)=>service.CliExtension(id) is {Block.Kind:"auth_required"} extension&&
        CliRecipes.OutputFormatFor(extension.Recipe)=="dsh-desktop";
}
