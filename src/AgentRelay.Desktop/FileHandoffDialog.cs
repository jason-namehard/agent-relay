using System.Windows;
using System.Windows.Controls;
using AgentRelay.Core;
using Microsoft.Win32;

namespace AgentRelay.Desktop;

public sealed class FileHandoffDialog : Window
{
    public ManualTransferDraft? Result { get; private set; }

    public FileHandoffDialog(Window owner,RelayService service,string language)
    {
        var ui=new EntryDialogLayout(this,owner,UiText.Translate("New handoff",language),language);
        var agents=service.Agents();
        ui.Body.Children.Add(ui.Note("Ask Agent A to write a file for B, then append the copied instructions. Paste its JSON here and create the handoff. Installed Agent rules can provide this format automatically."));
        var reply=ui.Field(ui.Body,"Agent handoff JSON","FileHandoffReply",multiline:true);reply.Height=150;
        var actions=new WrapPanel();ui.Body.Children.Add(actions);
        ui.Button(actions,"Copy handoff format","CopyFileHandoffFormat",()=>
        {
            Clipboard.SetText(UiText.Translate("After creating the requested file, return only this JSON. Use registered node IDs or names and an existing absolute file path. This message describes a handoff; do not create a second file containing the JSON.",language)+"\n"+
                RelayJson.Write(new {from=agents.FirstOrDefault()?.AgentId??"",to=agents.Skip(1).FirstOrDefault()?.AgentId??"",file="",message=""})+
                "\nNodes: "+RelayJson.Write(agents.Select(a=>new{id=a.AgentId,name=a.DisplayName??a.AgentId})));
        });
        ui.Button(actions,"Parse and create handoff","ParseFileHandoff",()=>
        {Result=ManualTransfers.Parse(reply.Text,agents);DialogResult=true;},true);

        var manual=new StackPanel{Margin=new(10)};
        var expander=new Expander{Header=ui.T("Or select a file yourself"),Content=manual,Margin=new(0,16,0,8)};ui.Body.Children.Add(expander);
        ComboBox Node(string label,string name,int selected)
        {
            manual.Children.Add(ui.Note(label));
            var box=new ComboBox{Name=name,ItemsSource=agents.Select(a=>new HandoffAgentOption(a.AgentId,a.DisplayName??a.AgentId)).ToArray(),
                DisplayMemberPath="Name",SelectedValuePath="Id",SelectedIndex=selected,MinHeight=38};
            manual.Children.Add(box);return box;
        }
        var from=Node("Sender","FileHandoffFrom",0);var to=Node("Recipient","FileHandoffTo",1);
        var file=ui.Field(manual,"Handoff file","FileHandoffFile");
        ui.Button(manual,"Choose file","ChooseHandoffFile",()=>{var picker=new OpenFileDialog();if(picker.ShowDialog(this)==true)file.Text=picker.FileName;});
        var message=ui.Field(manual,"Short work request","FileHandoffMessage",multiline:true);message.Height=75;
        ui.Button(manual,"Create handoff","CreateFileHandoff",()=>
        {
            Result=ManualTransfers.Parse(RelayJson.Write(new ManualTransferDraft(from.SelectedValue as string??"",to.SelectedValue as string??"",file.Text,message.Text)),agents);
            DialogResult=true;
        });
        var cancel=ui.Button(ui.Actions,"Cancel","CancelFileHandoff",()=>DialogResult=false);cancel.IsCancel=true;
    }
}
