using System.Windows;
using System.Windows.Controls;
using AgentRelay.Core;

namespace AgentRelay.Desktop;

public partial class MainWindow
{
    private void AddSessionBindings(Panel pane,TaskRecord task)
    {
        var body=new StackPanel{Margin=new(10)};
        pane.Children.Add(new Expander{Header=T("Conversations"),Content=body,Margin=new(0,8,0,8)});
        foreach(var id in new[]{task.RequesterId,task.RecipientId})
        {
            var binding=service.TaskSession(task.TaskId,id);
            var description=binding?.SessionId is {} session?session+" · "+binding.Binding:binding?.Binding=="new"||id==task.RecipientId?T("New conversation"):T("Source conversation is not identified.");
            body.Children.Add(Note(AgentName(id)+": "+description));
            if(binding?.Cwd is not null)body.Children.Add(Note(binding.Cwd));
            if(task.State is TaskState.Succeeded or TaskState.Cancelled or TaskState.TimedOut or TaskState.Failed)continue;
            var actions=new WrapPanel();body.Children.Add(actions);
            Add(actions,service.SessionDiscoverySupported(id)?"Change conversation":"Enter Session ID manually",()=>
            {
                var picker=new SessionPickerDialog(this,service,id,preferences.Language,binding?.Cwd);
                if(picker.ShowDialog()==true)service.BindTaskSession(task.TaskId,id,picker.SelectedSession!,picker.Binding);
                else reviewCancelled=true;
                return Task.CompletedTask;
            });
            Add(actions,"New conversation",()=>
            {
                if(MessageBox.Show(this,T("Create a new conversation on the next CLI delivery? The previous conversation is kept."),T("New conversation"),MessageBoxButton.OKCancel)==MessageBoxResult.OK)
                    service.UseNewTaskSession(task.TaskId,id);
                else reviewCancelled=true;
                return Task.CompletedTask;
            });
        }
    }
}
