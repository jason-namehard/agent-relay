using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using AgentRelay.Core;

namespace AgentRelay.Desktop;

public sealed class CliHandoffDialog : Window
{
    public CliHandoffRequest? Result { get; private set; }
    public SessionRecord? Origin { get; private set; }
    public SessionRecord? Target { get; private set; }
    public string OriginBinding { get; private set; }="user_selected";
    public string TargetBinding { get; private set; }="user_selected";

    public CliHandoffDialog(Window owner,RelayService service,string language)
    {
        var ui=new EntryDialogLayout(this,owner,UiText.Translate("New task",language),language);
        ui.Body.Children.Add(ui.Note("Write the work request and choose the conversations. Agents with a bridge receive it automatically; for others you copy the prompt. A real call may incur model charges."));
        var agents=service.Agents().Select(a=>new HandoffAgentOption(a.AgentId,a.DisplayName??a.AgentId)).ToArray();
        ComboBox AgentChoice(string label,string name,int index)
        {
            ui.Body.Children.Add(ui.Note(label));
            var choice=new ComboBox{Name=name,ItemsSource=agents,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedIndex=index,MinHeight=38};
            AutomationProperties.SetAutomationId(choice,name);ui.Body.Children.Add(choice);return choice;
        }
        var from=AgentChoice("Sender","CliTaskFrom",0);
        var originLabel=ui.Note("Choose the source conversation for exact replies.");ui.Body.Children.Add(originLabel);
        ui.Button(ui.Body,"Choose source conversation","CliTaskOrigin",()=>
        {
            if(from.SelectedValue is not string id)throw new RelayException("agent_required",ui.T("Select an Agent."));
            var picker=new SessionPickerDialog(this,service,id,language);
            if(picker.ShowDialog()!=true)return;
            Origin=picker.SelectedSession;OriginBinding=picker.Binding;originLabel.Text=(Origin!.Title??Origin.SessionId)+"\n"+Origin.Cwd;
        });
        var to=AgentChoice("Recipient","CliTaskTo",Math.Min(1,agents.Length-1));
        var bridgeCard=BridgePresentation.Card(this,"CliTaskBridgeCard");ui.Body.Children.Add(bridgeCard);
        void ShowBridge()
        {
            if(to.SelectedItem is HandoffAgentOption recipient)BridgePresentation.Show(bridgeCard,service.Bridge(recipient.Id),recipient.Name,ui.T);
        }
        ShowBridge();
        var create=new RadioButton{Name="CliTaskNewSession",Content=ui.T("New conversation"),IsChecked=true,Margin=new(0,8,0,8)};
        var existing=new RadioButton{Name="CliTaskExistingSession",Content=ui.T("Use existing conversation"),Margin=new(0,4,0,8)};
        ui.Body.Children.Add(create);ui.Body.Children.Add(existing);
        var targetLabel=ui.Note("New conversation");ui.Body.Children.Add(targetLabel);
        var newCwd=ui.Field(ui.Body,"New conversation folder (optional)","CliTaskNewCwd");
        var choose=ui.Button(ui.Body,"Choose existing conversation","CliTaskTarget",()=>
        {
            if(to.SelectedValue is not string id)throw new RelayException("agent_required",ui.T("Select an Agent."));
            var picker=new SessionPickerDialog(this,service,id,language,Origin?.Cwd);
            if(picker.ShowDialog()!=true)return;
            Target=picker.SelectedSession;TargetBinding=picker.Binding;targetLabel.Text=(Target!.Title??Target.SessionId)+"\n"+Target.Cwd;
        });choose.IsEnabled=false;
        create.Checked+=(_,_)=>{choose.IsEnabled=false;newCwd.IsEnabled=true;};existing.Checked+=(_,_)=>{choose.IsEnabled=true;newCwd.IsEnabled=false;};
        from.SelectionChanged+=(_,_)=>{Origin=null;originLabel.Text=ui.T("Choose the source conversation for exact replies.");};
        to.SelectionChanged+=(_,_)=>{Target=null;targetLabel.Text=ui.T("New conversation");create.IsChecked=true;ShowBridge();};
        var prompt=ui.Field(ui.Body,"Work request","CliTaskPrompt",multiline:true);prompt.Height=160;
        var options=new CollaborationOptionsPanel(ui,ui.Body,"CliTask",service.Settings().TaskTimeoutMinutes,true);
        var permission=new CheckBox{Name="CliTaskEnableDirections",Content=ui.T("Allow enabling the connection directions required by this task"),Margin=new(0,4,0,10)};
        ui.Body.Children.Add(permission);
        ui.Body.Children.Add(ui.Note("This enables required routes that are paused or not yet authorized. It does not add a bridge or sign in to an Agent."));
        ui.Body.Children.Add(ui.Note("Chosen conversations stay bound to this task. Without a source conversation, turns for the sender are copied manually."));
        ui.Button(ui.Actions,"Create task","SendCliTask",()=>
        {
            if(from.SelectedValue is not string sender||to.SelectedValue is not string recipient)throw new RelayException("agent_required",ui.T("Select an Agent."));
            if(sender==recipient)throw new RelayException("manual_same_agent",ui.T("Sender and recipient must be different."));
            if(existing.IsChecked==true&&Target is null)throw new RelayException("session_selection_required",ui.T("Select a conversation first."));
            if(string.IsNullOrWhiteSpace(prompt.Text))throw new RelayException("cli_work_required",ui.T("Enter the work request."));
            var choice=options.Build(sender,recipient);
            Result=new(sender,recipient,prompt.Text,Origin?.SessionId,existing.IsChecked==true?Target!.SessionId:null,
                permission.IsChecked==true,Origin?.Cwd,existing.IsChecked==true?Target?.Cwd:string.IsNullOrWhiteSpace(newCwd.Text)?null:newCwd.Text.Trim(),
                choice.Collaboration,choice.AutoDeliver,choice.TurnTimeoutMinutes);
            DialogResult=true;
        },true);
        var cancel=ui.Button(ui.Actions,"Cancel","CancelCliTask",()=>DialogResult=false);cancel.IsCancel=true;
    }
}
