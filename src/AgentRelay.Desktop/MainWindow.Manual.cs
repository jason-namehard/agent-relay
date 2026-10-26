using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AgentRelay.Core;
using Microsoft.Win32;

namespace AgentRelay.Desktop;

public partial class MainWindow
{
    private FileSystemWatcher? receiptWatcher;
    private bool receiptRefreshQueued;
    private string? migrationSummary;

    private void RecordMigration(string previousHome)
    {
        var files=Directory.EnumerateFiles(Path.Combine(service.Home,"workspace"),"*",SearchOption.AllDirectories).Count();
        migrationSummary=string.Format(CultureInfo.CurrentUICulture,T("Current relay: {0} handoffs and {1} workspace files. Folder: {2}. Previous folder kept as backup: {3}"),service.Tasks().Count,files,service.Home,previousHome);
    }

    private void WatchReceipts()
    {
        receiptWatcher?.Dispose();
        var path=Path.Combine(service.Home,"workspace");Directory.CreateDirectory(path);
        receiptWatcher=new FileSystemWatcher(path,"receipt.json"){IncludeSubdirectories=true,NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite|NotifyFilters.Size};
        receiptWatcher.Created+=(_,_)=>ReceiptChanged();receiptWatcher.Changed+=(_,_)=>ReceiptChanged();receiptWatcher.Renamed+=(_,_)=>ReceiptChanged();
        receiptWatcher.Error+=(_,_)=>Dispatcher.BeginInvoke(new Action(()=>LedgerHint.Text=T("Receipt monitoring needs a refresh. Click Refresh to read saved receipts.")));
        receiptWatcher.EnableRaisingEvents=true;
    }

    private void ReceiptChanged()
    {
        Dispatcher.BeginInvoke(new Action(async()=>
        {
            if(receiptRefreshQueued)return;
            receiptRefreshQueued=true;
            if(busy)return;
            await ReadReceipts();
        }));
    }

    private async Task ReadReceipts()
    {
        receiptRefreshQueued=false;
        var drafts=CaptureInputs();
        await Run(()=>Task.CompletedTask,"Ready",refresh:Navigation.SelectedItem is ListBoxItem {Tag:"Home"},clearError:false,reportStatus:false);
        RestoreInputs(drafts);
    }

    private string FileState(TaskRecord task)
    {
        var agent=AgentName(task.AwaitingAgentId??task.RecipientId);
        return task.State switch
        {
            TaskState.AwaitingManual=>string.Format(CultureInfo.CurrentUICulture,T("Waiting for {0} to receive and reply"),agent),
            TaskState.Dispatched or TaskState.Acknowledged or TaskState.Running=>string.Format(CultureInfo.CurrentUICulture,T("{0} is processing"),agent),
            TaskState.AwaitingReply=>string.Format(CultureInfo.CurrentUICulture,T("Waiting for {0} to handle the reply"),agent),
            _=>T(task.State.ToString())
        };
    }

    private string FilePrompt(TaskRecord task)
    {
        var receipt=service.ManualReceiptPath(task);
        var context=Path.Combine(Path.GetDirectoryName(receipt)!,"context.json");
        var current=task.AwaitingAgentId??task.RecipientId;
        var template=new ManualReceipt(task.TaskId,task.AttemptId,current,task.ManualTurn,"done",false);
        File.WriteAllText(context,RelayJson.Write(new
        {
            sender_name=AgentName(task.ManualTurn==0?task.RequesterId:current==task.RequesterId?task.RecipientId:task.RequesterId),
            recipient_name=AgentName(current),current_file=task.ManualCurrentFile??task.RequestPath,request=task.ManualNextAction,
            reply_file=task.ResultPath,receipt_file=receipt,receipt_template=template,
            instructions="Read current_file and perform the request. Write the receipt JSON to receipt_file when finished; do not ask the user to import your reply or create another handoff JSON. status: received/working/done/failed. Keep task_id, attempt_id, sender and turn unchanged. Set needs_reply=false when complete. Only use needs_reply=true for concrete remaining work; include result_file and next_action. No greetings or repeated confirmations. Limit: 4 completion turns. Permanent deliverables belong in the project. Publish one complete receipt or rename a temporary file."
        }));
        return string.Format(CultureInfo.CurrentUICulture,T("A file is waiting for you. Read {0}, perform the request and write its receipt. No extra acknowledgement message is needed."),context);
    }

    private void FileTaskPane(StackPanel pane,TaskView view)
    {
        var task=view.Task;
        Heading(pane,AgentName(task.RequesterId)+" → "+AgentName(task.RecipientId),FileState(task));
        if(task.ResumeStatus=="operator_completed")pane.Children.Add(Note(T("Marked complete by the user. Agent completion was not verified.")));
        if(task.ErrorMessage is not null)pane.Children.Add(Note(task.ErrorMessage));
        AddSessionBindings(pane,task);
        var live=task.State is not(TaskState.Succeeded or TaskState.Failed or TaskState.Cancelled or TaskState.TimedOut);
        var actions=Row(pane);
        if(live)
        {
            var recipient=AgentName(task.AwaitingAgentId??task.RecipientId);
            var processing=task.State is TaskState.Acknowledged or TaskState.Running;
            pane.Children.Add(new Border{Background=Theme("AccentSoftBrush"),CornerRadius=new(8),Padding=new(12),Margin=new(0,8,0,8),Child=new TextBlock{
                Text=FileDeliveryHint(task,processing,recipient),TextWrapping=TextWrapping.Wrap}});
            Add(actions,string.Format(CultureInfo.CurrentUICulture,T("Copy prompt for {0}"),recipient),()=>{Clipboard.SetText(FilePrompt(task));return Task.CompletedTask;},false);
            AddFileDeliveryAction(actions,task);
            Add(actions,"Cancel",()=>{service.Cancel(task.TaskId,"Cancelled by user");return Task.CompletedTask;});
        }
        AddTaskOperatorActions(actions,task);
        Add(actions,"Open task folder",()=>{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.GetDirectoryName(task.RequestPath)!){UseShellExecute=true});return Task.CompletedTask;},false);
        var current=task.ManualCurrentFile??task.RequestPath;
        pane.Children.Add(Note(T("Handoff file")+": "+current));
        if(!string.IsNullOrWhiteSpace(task.ManualNextAction))pane.Children.Add(Note(task.ManualNextAction));
        if(Path.GetExtension(current) is ".md" or ".txt" && File.Exists(current))
        {
            var text=Output("ManualFilePreview",150);text.Text=File.ReadAllText(current);
            pane.Children.Add(new Expander{Header=T("File preview"),Content=text});
        }
        var history=new StackPanel();
        foreach(var entry in service.Audit(task.TaskId))history.Children.Add(Note(entry.RecordedAt.ToLocalTime().ToString("g")+" · "+entry.Event));
        pane.Children.Add(new Expander{Header=T("Processing history"),Content=history});
    }

    private void InstallRules(string id)
    {
        var picker=new OpenFileDialog{Title=T("Choose the Agent instruction file"),Filter="Agent instructions|*.md|All files|*.*",FileName="AGENTS.md",CheckFileExists=false};
        if(picker.ShowDialog(this)!=true){reviewCancelled=true;return;}
        var preview=service.PreviewGuidance(id,picker.FileName);
        var message=T("Relay will append one small rule to this file. Detailed instructions load only when the Agent follows that rule. Removing Relay's ZIP folder cannot remove this rule; use Remove rules first.")+"\n\n"+preview.InstructionFile+"\n"+preview.Rule;
        if(MessageBox.Show(this,message,T("Install handoff rule"),MessageBoxButton.OKCancel)!=MessageBoxResult.OK){reviewCancelled=true;return;}
        service.InstallGuidance(id,picker.FileName);
    }
}
