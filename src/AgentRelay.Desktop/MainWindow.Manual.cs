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
        string Phase(string phase)=>T(phase switch{"implement"=>"Implementing","revise"=>"Revising","review"=>"Reviewing","accept"=>"Accepting the result",_=>phase});
        return task.State switch
        {
            TaskState.Paused=>T("Paused · Waiting for your decision"),
            TaskState.Succeeded when task.Collaboration?.Deliverable is not null=>T("Delivered"),
            TaskState.AwaitingManual or TaskState.AwaitingReply when task.Collaboration is {} c=>string.Format(CultureInfo.CurrentUICulture,T("Waiting for {0} · {1}"),agent,Phase(c.Phase)),
            TaskState.Dispatched or TaskState.Acknowledged or TaskState.Running when task.Collaboration is {} c=>string.Format(CultureInfo.CurrentUICulture,T("{0} is processing · {1}"),agent,Phase(c.Phase)),
            TaskState.AwaitingManual=>string.Format(CultureInfo.CurrentUICulture,T("Waiting for {0} to receive and reply"),agent),
            TaskState.Dispatched or TaskState.Acknowledged or TaskState.Running=>string.Format(CultureInfo.CurrentUICulture,T("{0} is processing"),agent),
            TaskState.AwaitingReply=>string.Format(CultureInfo.CurrentUICulture,T("Waiting for {0} to handle the reply"),agent),
            _=>T(task.State.ToString())
        };
    }

    private string FilePrompt(TaskRecord task)
    {
        _=service.FileHandoffPrompt(task);
        var receipt=service.ManualReceiptPath(task);
        var context=Path.Combine(Path.GetDirectoryName(receipt)!,"context.json");
        return string.Format(CultureInfo.CurrentUICulture,T("A file is waiting for you. Read {0}, perform the request and write its receipt. No extra acknowledgement message is needed."),context);
    }

    private void FileTaskPane(StackPanel pane,TaskView view)
    {
        var task=view.Task;var progress=view.Progress!;
        Heading(pane,AgentName(task.RequesterId)+" → "+AgentName(task.RecipientId),FileState(task));
        if(task.ResumeStatus=="operator_completed")pane.Children.Add(Note(T("Marked complete by the user. Agent completion was not verified.")));
        if(task.ErrorMessage is not null)pane.Children.Add(Note(task.ErrorMessage));
        if(task.HandoffPlan is {} plan)pane.Children.Add(Note(T("Planned steps")+": "+string.Join(" → ",plan.Select(AgentName))+" · "+T("Current step")+" "+(task.ManualTurn+1)+"/"+plan.Length));
        if(task.Collaboration is {} c)
        {
            pane.Children.Add(Note(string.Format(CultureInfo.CurrentUICulture,T("Review loop · Author {0} · Reviewer {1} · Review round {2}/{3}"),
                AgentName(c.AuthorId),AgentName(c.ReviewerId),Math.Min(c.ReviewRound,c.MaxReviewRounds),c.MaxReviewRounds)));
            if(c.Criteria is not null)pane.Children.Add(Note(T("Acceptance criteria")+": "+c.Criteria));
            if(c.OpenItems.Length>0)pane.Children.Add(Note(T("Open items")+":\n· "+string.Join("\n· ",c.OpenItems)));
            if(c.Deliverable is not null)pane.Children.Add(Note(T("Deliverable")+": "+c.Deliverable));
            if(!progress.Finished)
            {
                var next=c.Phase=="review"
                    ?string.Format(CultureInfo.CurrentUICulture,T("Next: if it passes, {0} accepts; if changes are needed, {0} revises."),AgentName(c.AuthorId))
                    :c.Phase=="accept"
                        ?string.Format(CultureInfo.CurrentUICulture,T("Next: if {0} accepts, the task is delivered; a new change goes back to {1} for review."),AgentName(c.AuthorId),AgentName(c.ReviewerId))
                        :string.Format(CultureInfo.CurrentUICulture,T("Next: {0} reviews the submission."),AgentName(c.ReviewerId));
                pane.Children.Add(Note(next));
            }
        }
        else if(!progress.Finished)pane.Children.Add(Note(string.Format(CultureInfo.CurrentUICulture,T("Turn {0} of at most {1}"),task.ManualTurn+1,progress.MaxTurns)));
        AddSessionBindings(pane,task);
        var actions=Row(pane);
        if(!progress.Finished)
        {
            var recipient=AgentName(task.AwaitingAgentId??task.RecipientId);
            pane.Children.Add(new Border{Background=Theme("AccentSoftBrush"),CornerRadius=new(8),Padding=new(12),Margin=new(0,8,0,8),Child=new TextBlock{
                Text=FileDeliveryHint(task,progress,recipient),TextWrapping=TextWrapping.Wrap}});
            if(task.State==TaskState.Paused)
            {
                var note=EntryDialogLayout.Input("ContinueNote",multiline:true);note.Height=60;
                pane.Children.Add(new Label{Content=T("Note for the next Agent (optional)"),Target=note,Padding=new(0,0,0,2)});pane.Children.Add(note);
                var extra=task.PauseReason=="round_limit"?2:0;
                Add(actions,extra>0?string.Format(CultureInfo.CurrentUICulture,T("Continue with {0} more rounds"),extra):T("Continue"),()=>
                {service.ContinueFileHandoff(task.TaskId,note.Text,extra);StartAutoDeliveries();return Task.CompletedTask;});
            }
            else
            {
                Add(actions,string.Format(CultureInfo.CurrentUICulture,T("Copy prompt for {0}"),recipient),()=>{Clipboard.SetText(FilePrompt(task));return Task.CompletedTask;},false);
                AddFileDeliveryAction(actions,task,progress);
            }
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
        if(task.Collaboration is {History.Count:>0} loop)
        {
            var rounds=new StackPanel();
            foreach(var step in loop.History)rounds.Children.Add(Note(step.At.ToLocalTime().ToString("g")+" · "+AgentName(step.AgentId)+" · "+T(step.Outcome)+
                (step.OpenItems is {} items?" · "+string.Join("；",items):"")+(step.ResultPath is null?"":" · "+step.ResultPath)));
            pane.Children.Add(new Expander{Header=T("Review history"),Content=rounds,IsExpanded=true});
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
