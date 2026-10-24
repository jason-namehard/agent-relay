using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using AgentRelay.Core;

namespace AgentRelay.Desktop;

public sealed class CliProbeDialog : Window
{
    private readonly RelayService service;
    private readonly string agentId;
    private readonly string language;
    private readonly bool demo;
    private readonly EntryDialogLayout layout;
    private bool running;

    public CliProbeDialog(Window owner, RelayService service, string agentId, string language, bool demo=false)
    {
        this.service=service;this.agentId=agentId;this.language=language;this.demo=demo;
        layout=new EntryDialogLayout(this,owner,T("Test CLI"),language);
        Closing+=(_,e)=>{if(running)e.Cancel=true;};
        Render();
    }

    private string T(string text)=>UiText.Translate(text,language);
    private TextBlock Note(string text)=>new(){Text=T(text),TextWrapping=TextWrapping.Wrap,Margin=new(0,8,0,10)};
    private Button Action(Panel parent,string label,string name,Func<Task> action)
    {
        var button=new Button{Name=name,Content=T(label),MinHeight=38,Height=double.NaN};
        System.Windows.Automation.AutomationProperties.SetAutomationId(button,name);
        button.Click+=async(_,_)=>
        {
            running=true;layout.Body.IsEnabled=false;layout.Actions.IsEnabled=false;layout.Status.Text=T("Working…");
            try{await action();Render();}
            catch(Exception error) when(error is RelayException or IOException or UnauthorizedAccessException or Win32Exception or ExternalException)
            {layout.Status.Text=T(error.Message);}
            finally{running=false;layout.Body.IsEnabled=true;layout.Actions.IsEnabled=true;}
        };
        parent.Children.Add(button);return button;
    }

    private void Render()
    {
        layout.Body.Children.Clear();layout.Actions.Children.Clear();layout.Status.Text="";
        var agent=service.Agents().Single(a=>a.AgentId==agentId);
        layout.Body.Children.Add(Note(agent.DisplayName??agent.AgentId));
        var extension=service.CliExtension(agentId);
        if(extension is null)
        {
            layout.Body.Children.Add(Note("No CLI recipe is registered. Open Settings → Advanced: CLI interface extensions to copy a prompt for an Agent that knows the calling method."));
        }
        else
        {
            layout.Body.Children.Add(Note("This sends a real, short probe to the selected CLI. Model charges may apply, including host context. It does not run project work. Check the command before starting."));
            var plan=EntryDialogLayout.Input("ProbeRecipe",RelayJson.Write(extension.Recipe),true);plan.IsReadOnly=true;plan.Height=170;
            layout.Body.Children.Add(new Expander{Header=T("Calling recipe"),Content=plan});
            if(demo)layout.Body.Children.Add(Note("Demo mode does not run external CLI calls. Use a normal workspace to test a real Agent."));
            var create=Action(layout.Body,"Run new-session probe","ProbeCreate",async()=>{await service.RunCliProbeAsync(agentId,"create");});
            create.IsEnabled=!demo;
            if(extension.CreateProbe is {} probe)ProbeResult(probe);
            if(extension.CanCreate && extension.Recipe.Resume is not null)
            {
                var resume=Action(layout.Body,"Run same-session probe","ProbeResume",async()=>{await service.RunCliProbeAsync(agentId,"resume");});
                resume.IsEnabled=!demo;
                if(extension.ResumeProbe is {} resumed)ProbeResult(resumed);
            }
            layout.Body.Children.Add(Note(extension.CanResume?"New session and resume verified":extension.CanCreate?"New session verified":"CLI is not enabled until the receipt passes and you confirm visibility."));
            layout.Body.Children.Add(Note("New-session verification enables delivery to a new visible session. Resume verification tests the same probe session; it does not bind future work to that probe session."));
        }
        var close=new Button{Name="CloseCliProbe",Content=T("Close"),IsCancel=true};
        close.Click+=(_,_)=>DialogResult=true;
        layout.Actions.Children.Add(close);
    }

    private void ProbeResult(CliProbe probe)
    {
        var text=EntryDialogLayout.Input("Probe"+(probe.Operation=="create"?"Create":"Resume")+"Result",
            T("Marker")+": "+probe.Marker+"\n"+T("Session")+": "+probe.SessionId+"\n"+
            (probe.MachinePassed?T("Machine receipt passed"):probe.Error),true);
        text.IsReadOnly=true;text.Height=100;layout.Body.Children.Add(text);
        if(!probe.MachinePassed)return;
        var confirm=new CheckBox{Content=new TextBlock{Text=T(probe.Operation=="create"?
            "I found this marker in the target Agent's session list.":"I found this marker in the SAME session, not a new session."),TextWrapping=TextWrapping.Wrap},
            IsChecked=probe.UserConfirmed,Margin=new(0,8,0,10),Name=probe.Operation=="create"?"ConfirmCreateVisible":"ConfirmResumeVisible"};
        layout.Body.Children.Add(confirm);
        var save=Action(layout.Body,"Save visibility confirmation",probe.Operation=="create"?"SaveCreateConfirmation":"SaveResumeConfirmation",()=>
        {service.ConfirmCliProbe(agentId,probe.ProbeId,confirm.IsChecked==true);return Task.CompletedTask;});
        save.IsEnabled=false;
        confirm.Checked+=(_,_)=>save.IsEnabled=true;
        confirm.Unchecked+=(_,_)=>save.IsEnabled=probe.UserConfirmed;
    }
}
