using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AgentRelay.Core;
using AgentRelay.Desktop;

namespace AgentRelay.Desktop.Smoke;

internal static class Program
{
    private static readonly List<string> checks=[];
    [STAThread]
    private static int Main(string[] args)
    {
        if(args.Length!=2)throw new ArgumentException("Arguments: <new fixture directory> <relay.dll>");
        var root=Path.GetFullPath(args[0]);if(Directory.Exists(root))throw new IOException("Fixture directory must be new.");
        Directory.CreateDirectory(root);var service=new RelayService(Path.Combine(root,"data"));service.Initialize();
        var config=Path.Combine(root,"config.toml");File.WriteAllText(config,"name = 'smoke fixture'\n");
        foreach(var (id,name) in new[]{("agent-a","自选节点甲"),("agent-b","自选节点乙"),("agent-c","自选节点丙")})service.Register(id,"fixture",config,displayName:name);
        service.ValidateAgent("agent-a");service.ValidateAgent("agent-b");
        service.SetPermission("agent-a","agent-b","allow");service.SetPermission("agent-b","agent-a","deny");service.SetPermission("agent-a","agent-c","allow");
        var task=service.DispatchAsync(new("agent-a","agent-b","## Review\nInspect the fixture.","manual","manual","smoke")).GetAwaiter().GetResult().Task;
        File.WriteAllText(task.ResultPath,"## Result\n54\n## Verification\nFixture completed.");service.Complete(task.TaskId,task.AttemptId,"agent-b","done",task.ResultPath);
        var waiting=service.DispatchAsync(new("agent-a","agent-b","## Review\nWait for manual delivery.","manual","manual","waiting")).GetAwaiter().GetResult().Task;
        Appearance.Save(service.Home,new DesktopPreferences("en"));
        var app=new Application();var window=new MainWindow(service);
        window.ContentRendered+=async(_,_)=>
        {
            try
            {
                await Task.Delay(100);await Exercise(window,service,task,waiting,config,root,args[1]);
                File.WriteAllText(Path.Combine(root,"report.json"),RelayJson.Write(new{status="passed",checks}));app.Shutdown(0);
            }
            catch(Exception error)
            {
                File.WriteAllText(Path.Combine(root,"report.json"),RelayJson.Write(new{status="failed",error=error.ToString(),checks}));app.Shutdown(1);
            }
        };
        return app.Run(window);
    }

    private static async Task Exercise(MainWindow window,RelayService service,TaskRecord task,TaskRecord waiting,string config,string root,string cli)
    {
        var navigation=(ListBox)window.FindName("Navigation");
        void Navigate(string page){navigation.SelectedItem=navigation.Items.Cast<ListBoxItem>().Single(i=>(string)i.Tag==page);window.UpdateLayout();}
        await Wait(()=>((TextBlock)window.FindName("StatusText")).Text!="Working…");
        await CheckTray(window);
        await CheckNavigationAndSounds(window,root);
        await CheckCloseBehavior(window,root);
        await CheckSessionDialogs(window,root);
        await CheckCliTaskEntry(window,root);
        await CheckReviewLoopPane(window,root);
        await CheckModelDefaults(window,root);
        await CheckNotificationDelivery(window,root);
        await CheckBatchTasks(window,root);
        await CheckBlockedDeliveryUi(window,root);
        await CheckFileCancellation(window,root);
        Check(navigation.Items.Cast<ListBoxItem>().Select(i=>(string)i.Tag).SequenceEqual(new[]{"Home","Agents","Connections","Settings"}),"Exactly four main pages");
        Check(Find<DataGrid>(window,"TasksTable").Items.Count==2,"Board combines all handoffs");
        var batchColumn=Find<DataGrid>(window,"TasksTable").Columns[0];
        var cellName=(Binding)batchColumn.CellStyle.Setters.OfType<Setter>().Single(s=>s.Property==AutomationProperties.NameProperty).Value;
        Check(cellName.Path.Path=="Route"&&cellName.StringFormat!.Contains("{0}"),"Task checkbox cells have a meaningful accessible name");
        Check(Find<DataGrid>(window,"TasksTable").Items.Cast<object>().All(r=>(string)r.GetType().GetProperty("Mode")!.GetValue(r)! == "Manual transfer"),"Manual task mode matches the creation label");
        Check(((Binding)((DataGridTextColumn)Find<DataGrid>(window,"TasksTable").Columns.Last()).Binding).StringFormat=="yyyy-MM-dd HH:mm:ss","Task timestamps use local 24-hour time without an offset");
        SelectTask(window,task.TaskId);
        await Click(window,"Copy prompt for 自选节点甲");
        var copiedHandoff=Clipboard.GetText();
        Check(copiedHandoff.Contains(task.TaskId)&&copiedHandoff.Contains(service.Status(task.TaskId).Task.ResultPath)&&copiedHandoff.Contains(service.Home),
            $"Copied completed handoff includes identity, reply path and relay location (identity={copiedHandoff.Contains(task.TaskId)}, path={copiedHandoff.Contains(service.Status(task.TaskId).Task.ResultPath)}, home={copiedHandoff.Contains(service.Home)}, length={copiedHandoff.Length}, status={((TextBlock)window.FindName("StatusText")).Text}, error={((TextBox)window.FindName("ErrorText")).Text})");
        Check(Clipboard.GetText().Contains("自选节点甲")&&Clipboard.GetText().Contains("自选节点乙"),"Completed handoff prompt identifies both user-selected Agent names");
        SelectTask(window,waiting.TaskId);
        await Click(window,"Copy prompt for 自选节点乙");
        Check(Clipboard.GetText().Contains("自选节点甲")&&Clipboard.GetText().Contains("自选节点乙")&&Clipboard.GetText().Contains("--sender agent-b"),"Receiver prompt uses friendly names while callback arguments retain exact node IDs");
        await SaveImage(window,Path.Combine(root,"Board.png"));

        ((ComboBox)window.FindName("QuickLanguage")).SelectedValue="zh-CN";await Task.Delay(30);
        Navigate("Agents");
        var registry=Find<DataGrid>(window,"AgentsTable");
        registry.SelectedItem=registry.Items.Cast<object>().Single(r=>(string)r.GetType().GetProperty("Name")!.GetValue(r)! == "自选节点乙");
        Check((string)registry.SelectedItem.GetType().GetProperty("Activity")!.GetValue(registry.SelectedItem)! == "有未完成交接","Registry identifies Agents with unfinished handoffs");
        await Click(window,"Delete Agent");
        Check(service.Agents().Any(a=>a.AgentId=="agent-b") && ((Border)window.FindName("ErrorBanner")).Visibility==Visibility.Visible && ((TextBox)window.FindName("ErrorText")).Text.Contains("未完成交接"),"Removing an active Agent is blocked before any confirmation dialog");
        Check(!FindAll<TextBox>(window).Any(b=>b.Name.StartsWith("AgentEntry")||b.Name.EndsWith("SetupReply")),"Agent page has no competing setup form");
        var agentToolbar=(Panel)FindAll<Button>(window).Single(b=>Equals(b.Tag,"New Agent")).Parent;
        Check(agentToolbar.Children.OfType<Button>().Select(b=>(string)b.Tag).SequenceEqual(new[]{"New Agent","Edit Agent","Delete Agent","Test bridge","Recheck and restore bridge","One-click setup"}),"Agent toolbar exposes bridge recovery alongside the configuration actions");
        Check(FindAll<Button>(window).Single(b=>Equals(b.Tag,"Test bridge")).IsEnabled,"CLI test remains available to explain missing calling methods instead of silently disabling itself");
        Check(FindAll<Button>(window).Any(b=>Equals(b.Tag,"Install handoff rule"))&&FindAll<Button>(window).Any(b=>Equals(b.Tag,"Remove handoff rule")),"Agent page exposes explicit installation and removal of handoff rules");
        var count=service.Agents().Count;
        await Modal<AgentEntryDialog>(window,"New Agent",async dialog=>
        {
            Check(((TextBlock)window.FindName("StatusText")).Text!=UiText.Translate("Working…","zh-CN"),"Agent form reports a waiting state instead of processing during modal input");
            Expand(dialog);
            Check(!FindAll<TextBox>(dialog).Any(b=>b.Name is "AgentEntryHost" or "AgentEntryReply"),"Manual Agent entry has one name and no redundant host-name or AI reply field");
            Check(!FindAll<Button>(dialog).Any(b=>b.Name.Contains("ParseReply",StringComparison.Ordinal)||b.Name.Contains("CopyPrompt",StringComparison.Ordinal)),"Manual Agent entry has no competing AI configuration buttons");
            Find<TextBox>(dialog,"AgentEntryName").Text="自由命名甲";
            Find<TextBox>(dialog,"AgentEntryConfig").Text="relative-config.toml";
            Press(dialog,"SaveAgentEntry");
            Check(dialog.Result is null&&Find<TextBox>(dialog,"AgentEntryName").Text=="自由命名甲"&&service.Agents().Count==count,"Invalid manual path preserves the named draft and creates no Agent");
            Find<TextBox>(dialog,"AgentEntryConfig").Text="";
            Check(Find<TextBox>(dialog,"AgentEntryCliArguments").AcceptsReturn&&Find<ComboBox>(dialog,"AgentEntryOutputFormat").Items.Count>=6,"CLI entry separates executable, arguments and output format into explicit controls");
            await CheckGlyphs(dialog,Find<TextBox>(dialog,"AgentEntryName"));
            await SaveImage(dialog,Path.Combine(root,"AgentEntry-zh.png"));
            Press(dialog,"CancelAgentEntry");
        });
        Check(service.Agents().Count==count,"Cancelled Agent entry writes nothing");
        await Modal<AgentEntryDialog>(window,"New Agent",dialog=>
        {
            Expand(dialog);
            Find<TextBox>(dialog,"AgentEntryName").Text="任意节点甲";
            Press(dialog,"SaveAgentEntry");return Task.CompletedTask;
        });
        var manual=service.Agents().Single(a=>a.DisplayName=="任意节点甲");
        Check(manual.ConfigPath==""&&manual.Host=="manual","Name alone registers a manual Agent without configuration paths");
        await SaveImage(window,Path.Combine(root,"Agents.png"));
        Navigate("Home");
        ((ComboBox)window.FindName("QuickLanguage")).SelectedValue="en";await Task.Delay(30);
        var taskCount=service.Tasks().Count;
        var source=Path.Combine(root,"handoff.zip");File.WriteAllBytes(source,[1,2,3,4]);
        await Modal<FileHandoffDialog>(window,"New manual transfer task",async dialog=>
        {
            Check(((TextBlock)window.FindName("StatusText")).Text!=UiText.Translate("Working…","en"),"Handoff form reports a waiting state instead of processing during modal input");
            Expand(dialog);
            Find<TextBox>(dialog,"ManualWorkRequest").Text="Write a proposal, exchange three useful rounds, then deliver the agreed result.";
            Press(dialog,"CopyFileHandoffFormat");
            Check(Clipboard.GetText().Contains("Write a proposal")&&Clipboard.GetText().Contains("JSON")&&Clipboard.GetText().Contains("agent-a"),"Manual prompt includes the work request, exact nodes and output rules");
            Check(!FindAll<RadioButton>(dialog).Any()&&!FindAll<TextBox>(dialog).Any(b=>b.Name.EndsWith("Rounds")),"Manual creation has no automatic collaboration or round-budget controls");
            Find<TextBox>(dialog,"FileHandoffReply").Text="{bad";Press(dialog,"ParseFileHandoff");
            Check(dialog.Result is null&&service.Tasks().Count==taskCount,"Malformed handoff JSON cannot create a task");
            Check(FindAll<TextBlock>(dialog).Any(t=>t.Text.Contains("complete JSON",StringComparison.Ordinal))&&!FindAll<TextBlock>(dialog).Any(t=>t.Text.Contains("BytePositionInLine",StringComparison.Ordinal)),"Malformed JSON explains how to recover without displaying a runtime exception transcript");
            Find<TextBox>(dialog,"FileHandoffReply").Text=RelayJson.Write(new{from=manual.AgentId,to=manual.AgentId,file=source});
            Press(dialog,"ParseFileHandoff");
            Check(dialog.Result is null&&service.Tasks().Count==taskCount,"Same-node file handoff is rejected before creation");
            Check(!FindAll<Button>(dialog).Any(b=>b.Name.Contains("Paste",StringComparison.Ordinal)),"File handoff accepts ordinary text paste without a redundant Paste button");
            Find<TextBox>(dialog,"FileHandoffReply").Text=RelayJson.Write(new{from=manual.AgentId,to="agent-c",file=source,message="检查文件并决定是否需要回传。"});
            await SaveImage(dialog,Path.Combine(root,"Handoff.png"));
            Press(dialog,"ParseFileHandoff");
        });
        await Wait(()=>service.Tasks().Count==taskCount+1);
        var created=service.Tasks().Single(v=>v.Task.ManualSourceFile==source).Task;
        Check(created.IsFileHandoff&&created.Mode=="manual"&&File.Exists(created.RequestPath),"Parsing and creating registers one manual file handoff without CLI setup");
        var pendingRow=Find<DataGrid>(window,"TasksTable").Items.Cast<object>().Single(r=>(string)r.GetType().GetProperty("TaskId")!.GetValue(r)! == created.TaskId);
        Check(((string)pendingRow.GetType().GetProperty("State")!.GetValue(pendingRow)!).Contains("自选节点丙",StringComparison.Ordinal),"Pending handoff status names the actual Agent that must receive and reply");
        Check(File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(created.RequestPath)),"Binary file content survives handoff creation");
        Check(Directory.GetFiles(Path.GetDirectoryName(created.RequestPath)!).Length==1,"Binary handoff creates one visible managed file without wrapper snapshots");
        Check(!FindAll<Button>(window).Any(b=>Equals(b.Tag,"Import completed reply")||Equals(b.Tag,"Retry")),"New file handoff does not require manual result import or retry controls");
        await Click(window,"Copy prompt for 自选节点丙");
        var receiverPrompt=Clipboard.GetText();
        var contextPath=Path.Combine(Path.GetDirectoryName(service.ManualReceiptPath(created))!,"context.json");
        Check(receiverPrompt.Contains(contextPath)&&receiverPrompt.Length-contextPath.Length<180,"Receiver prompt is a short pointer to task context instead of a long protocol transcript: "+receiverPrompt);
        var context=RelayJson.Read<System.Text.Json.JsonElement>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(service.ManualReceiptPath(created))!,"context.json")));
        Check(context.GetProperty("current_file").GetString()==created.RequestPath && context.GetProperty("receipt_template").GetProperty("sender").GetString()=="agent-c","Task context agrees with the installed rule and exact receipt identity");
        File.WriteAllText(service.ManualReceiptPath(created),RelayJson.Write(new ManualReceipt(created.TaskId,created.AttemptId,"agent-c",0,"working",false)));
        await Wait(()=>service.Status(created.TaskId).Task.State==TaskState.Running);
        await Wait(()=>FindAll<TextBlock>(window).Any(t=>t.Text==string.Format(UiText.Translate("Waiting for {0} to finish and write its receipt. Do not send the same request again.","en"),"自选节点丙")));
        Check(!FindAll<TextBlock>(window).Any(t=>t.Text.StartsWith("Next: copy the prompt",StringComparison.Ordinal)),"Processing handoff asks the user to wait rather than repeat the delivery");
        var replyFile=Path.Combine(root,"b-reply.md");File.WriteAllText(replyFile,"Select option 2 before finalizing.");
        File.WriteAllText(service.ManualReceiptPath(created),RelayJson.Write(new ManualReceipt(created.TaskId,created.AttemptId,"agent-c",0,"done",true,replyFile,"选择第二个选项并确认最终方案")));
        await Wait(()=>service.Status(created.TaskId).Task.State==TaskState.AwaitingReply);
        Check(service.Status(created.TaskId).Task.AwaitingAgentId==manual.AgentId,"Agent receipt automatically keeps the task ongoing and selects the requester for necessary followup");
        await Wait(()=>FindAll<Button>(window).Any(b=>Equals(b.Tag,"Copy prompt for 任意节点甲")));
        await Click(window,"Copy prompt for 任意节点甲");
        Check(Clipboard.GetText().Length<500,"Followup prompt remains short and changes its target Agent");
        Check(Directory.GetFiles(Path.GetDirectoryName(created.RequestPath)!).Length==2,"Reply adds one file without creating duplicate result snapshots");
        File.WriteAllText(service.ManualReceiptPath(created),RelayJson.Write(new ManualReceipt(created.TaskId,created.AttemptId,manual.AgentId,1,"done",false,TaskComplete:true)));
        await Wait(()=>service.Status(created.TaskId).Task.State==TaskState.Succeeded);
        Check(service.Status(created.TaskId).Task.State==TaskState.Succeeded,"Requester final receipt automatically completes the existing task without another paste or import");
        await SaveImage(window,Path.Combine(root,"Manual-completed.png"));

        var directSource=Path.Combine(root,"direct.md");File.WriteAllText(directSource,"No followup is needed after review.");
        var beforeDirect=service.Tasks().Count;
        await Modal<FileHandoffDialog>(window,"New manual transfer task",dialog=>
        {
            Find<TextBox>(dialog,"FileHandoffReply").Text=RelayJson.Write(new{from=manual.AgentId,to="agent-c",file=directSource});
            Press(dialog,"ParseFileHandoff");return Task.CompletedTask;
        });
        await Wait(()=>service.Tasks().Count==beforeDirect+1);
        var direct=service.Tasks().Single(v=>v.Task.ManualSourceFile==directSource).Task;
        File.WriteAllText(service.ManualReceiptPath(direct),RelayJson.Write(new ManualReceipt(direct.TaskId,direct.AttemptId,"agent-c",0,"done",false,TaskComplete:true)));
        await Wait(()=>service.Status(direct.TaskId).Task.State==TaskState.Succeeded);
        Check(service.Status(direct.TaskId).Task.ManualTurn==0,"Receipt that needs no reply ends the handoff immediately without returning to its sender");
        await CheckTaskOperatorActions(window,service,manual.AgentId,root);

        Navigate("Connections");
        Check(FindAll<System.Windows.Shapes.Ellipse>(window).Count(e=>AutomationProperties.GetAutomationId(e).StartsWith("CliIndicator:",StringComparison.Ordinal))==4,"Each Agent has a separate CLI capability indicator");
        var missingIndicator=ByAutomation<System.Windows.Shapes.Ellipse>(window,"CliIndicator:agent-c");
        Check(missingIndicator.Fill==window.FindResource("UnavailableBrush")&&missingIndicator.ToolTip.ToString()!.Contains("No CLI calling method"),"Missing CLI methods use a gray indicator with an explicit registration explanation");
        Check(FindAll<System.Windows.Shapes.Path>(window).Count(p=>AutomationProperties.GetAutomationId(p).StartsWith("ConnectionPair:"))==6,"Four nodes use six shared connection lines instead of twelve directed lines");
        Check(new[]{"ConnectionLegendSingle","ConnectionLegendDouble","ConnectionLegendNone"}.All(id=>FindAll<FrameworkElement>(window).Any(e=>AutomationProperties.GetAutomationId(e)==id)),"Connection legend contains visible single-arrow, double-arrow and unavailable-line examples");
        Check(FindAll<System.Windows.Shapes.Path>(window).Where(p=>AutomationProperties.GetAutomationId(p).StartsWith("ConnectionStroke:")).All(p=>p.StrokeDashArray.Count>0),"Unconfigured routes use dashed gray lines");
        Check(!FindAll<System.Windows.Shapes.Polygon>(window).Any(p=>AutomationProperties.GetAutomationId(p).StartsWith("ConnectionArrow:")),"Unavailable routes have no arrowheads");
        SeedExtension(service,"agent-a",true);SeedExtension(service,"agent-b",true);
        Navigate("Home");Navigate("Connections");
        var readyIndicator=ByAutomation<System.Windows.Shapes.Ellipse>(window,"CliIndicator:agent-a");
        Check(readyIndicator.Fill==window.FindResource("AllowedBrush")&&readyIndicator.ToolTip.ToString()!.Contains("verified"),"Verified CLI indicator is green and distinguishes verification from registration");
        var forward=ByAutomation<System.Windows.Shapes.Path>(window,"ConnectionPair:agent-a:agent-b");
        forward.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});
        Press(window,"PauseConnection_agent_a_agent_b");await Task.Delay(30);
        Check(service.Connections().Single(e=>e.From=="agent-a"&&e.To=="agent-b").Display=="Denied","Green arrow can pause its own direction");
        Check(service.Connections().Single(e=>e.From=="agent-b"&&e.To=="agent-a").Display=="Denied","Changing forward route leaves reverse route independent");
        Press(window,"EnableConnection_agent_a_agent_b");await Task.Delay(30);
        Check(service.Connections().Single(e=>e.From=="agent-a"&&e.To=="agent-b").Display=="Allowed","Red arrow can enable its own direction");
        Check(ByAutomation<System.Windows.Shapes.Path>(window,"ConnectionStroke:agent-a:agent-b").StrokeDashArray.Count==0,"Available enabled route is a solid stroke");
        Check(ByAutomation<System.Windows.Shapes.Polygon>(window,"ConnectionArrow:agent-a:agent-b").Points.Count==3,"Available route has a geometric arrowhead");
        Check(!FindAll<Border>(window).Any(b=>AutomationProperties.GetAutomationId(b).StartsWith("ConnectionLabel:")),"Shared connection lines do not repeat overlapping state labels");
        await SaveImage(window,Path.Combine(root,"Connections.png"));
        await CheckGraphPairs(window,root);
        CheckGraphCliStates(window);

        var probe=new CliProbeDialog(window,service,"agent-c","en");
        await Show(probe,dialog=>
        {
            Check(!FindAll<Button>(dialog).Any(b=>b.Name=="ProbeCreate"),"No recipe provides explanation without a misleading run button");
            Check(FindAll<TextBlock>(dialog).Any(t=>t.Text.Contains("One-click setup",StringComparison.Ordinal)&&t.Text.Contains("Edit Agent",StringComparison.Ordinal)&&t.Text.Contains("not a failed probe",StringComparison.Ordinal)),"Missing CLI method explains setup options without presenting it as a failed test");
            Press(dialog,"CloseCliProbe");return Task.CompletedTask;
        });
        SeedExtension(service,"agent-c",false);
        await Show(new CliProbeDialog(window,service,"agent-c","en"),async dialog=>
        {
            Check(!service.CliExtension("agent-c")!.CanCreate,"Passing machine receipt alone does not enable CLI");
            Check(!Find<CheckBox>(dialog,"ConfirmCreateVisible").IsChecked!.Value,"Visibility is never preconfirmed");
            Check(Find<TextBlock>(dialog,"CreateProbeTestedAt").Text.Contains("saved result"),"Probe results show their test time and remain separate from current connection health");
            Find<CheckBox>(dialog,"ConfirmCreateVisible").IsChecked=true;Press(dialog,"SaveCreateConfirmation");
            await Wait(()=>service.CliExtension("agent-c")!.CanCreate);
            Check(service.CliExtension("agent-c")!.CanCreate&&!service.CliExtension("agent-c")!.CanResume,"Visibility confirmation enables only verified new-session ability");
            Check(FindAll<Button>(dialog).Any(b=>b.Name=="ProbeResume"),"Same-session verification is a separate next step");
            Press(dialog,"CloseCliProbe");
        });
        var ext=service.CliExtension("agent-c")!;
        SaveExtension(service,ext with{ResumeProbe=new("resume-fixture","resume","RESUME-FIXTURE","fixture-session",true,false,null,DateTimeOffset.UtcNow,FileWritten:true)});
        await Show(new CliProbeDialog(window,service,"agent-c","en"),async dialog=>
        {
            Check(!service.CliExtension("agent-c")!.CanResume,"Resume machine receipt still needs same-session visibility confirmation");
            Find<CheckBox>(dialog,"ConfirmResumeVisible").IsChecked=true;Press(dialog,"SaveResumeConfirmation");
            await Wait(()=>service.CliExtension("agent-c")!.CanResume);
            Check(service.CliExtension("agent-c")!.CanResume,"Separate same-session confirmation enables resume");
            Press(dialog,"CloseCliProbe");
        });

        var visibilityExtension=service.CliExtension("agent-c")!;
        foreach(var format in new[]{"zcode-json","dsh-desktop"})
        {
            SaveExtension(service,visibilityExtension with {Recipe=visibilityExtension.Recipe with {OutputFormat=format}});
            await Show(new CliProbeDialog(window,service,"agent-c","en"),async dialog=>
            {
                var hint=Find<TextBlock>(dialog,"ProbeVisibilityHint").Text;
                Check(format=="zcode-json"?hint.Contains("restart Zcode"):hint.Contains("without restarting"),"Probe advice distinguishes Zcode display delays from the live DSH desktop bridge");
                Press(dialog,"CopyBridgeDiagnostic");
                await Wait(()=>Find<Button>(dialog,"CopyBridgeDiagnostic").IsEnabled);
                Check(Clipboard.GetText().Contains("agent-c")&&Clipboard.GetText().Contains("cli-recipe doctor"),"The bridge test copies a targeted repair prompt without starting a probe");
                Press(dialog,"CloseCliProbe");
            });
        }
        SaveExtension(service,visibilityExtension);

        Navigate("Settings");
        Check(!FindAll<TextBox>(window).Any(box=>box.Name=="CliRecipeReply")&&!FindAll<ComboBox>(window).Any(box=>box.Name=="ExtensionAgent"),"Settings links to Agent configuration instead of duplicating CLI entry fields");
        await CheckProbeProgress(window,service,root);
        var notification=Find<CheckBox>(window,"EnableDesktopNotifications");
        notification.BringIntoView();window.UpdateLayout();
        var notificationPeer=System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(notification)!;
        Check(notificationPeer.GetName()=="Enable Notifications" && notificationPeer.GetAutomationId()=="EnableDesktopNotifications","Notification toggle exposes a stable accessible name and ID");
        new RelayStore(service.Home).Write(s=>s.Execute("INSERT INTO desktop VALUES(1,$path)",("$path",Path.Combine(root,"fixture-notifier.exe"))));
        Navigate("Home");Navigate("Settings");
        notification=Find<CheckBox>(window,"EnableDesktopNotifications");
        var togglePeer=System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(notification)!;
        var toggle=(System.Windows.Automation.Provider.IToggleProvider)togglePeer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Toggle)!;
        Find<TextBox>(window,"TtlDays").Text="88";
        toggle.Toggle();
        await Wait(()=>service.DesktopExecutable() is null);
        Check(((TextBlock)window.FindName("StatusText")).Text=="Desktop notifications disabled" && notification.IsChecked==false,"UI Automation Toggle persists notification state and reports a saved result");
        Check(Find<TextBox>(window,"TtlDays").Text=="88" && service.Settings().ArtifactTtlDays==3,"Accessible notification toggle preserves unsaved drafts");
        Find<TextBox>(window,"TtlDays").Text="5";await Click(window,"Save Settings");
        Check(service.Settings().ArtifactTtlDays==5,"Retention settings persist");
        Find<TextBox>(window,"TtlDays").Text="0";await Click(window,"Save Settings");
        Check(service.Settings().ArtifactTtlDays==5&&((Border)window.FindName("ErrorBanner")).Visibility==Visibility.Visible,"Invalid retention does not overwrite valid settings");
        Find<TextBox>(window,"CustomAccent").Text="#123456";await Click(window,"Save Accent");
        Find<TextBox>(window,"CustomAccent").Text="invalid";await Click(window,"Save Accent");
        Check(Appearance.Load(service.Home).Accent=="#123456","Invalid accent does not replace saved color");
        foreach(var palette in Appearance.Palettes)
        {
            Find<ComboBox>(window,"PaletteChoice").SelectedValue=palette.Id;
            await Task.Delay(30);
            Check(Appearance.Load(service.Home).Palette==palette.Id,"Palette persists: "+palette.Id);
        }
        foreach(var language in Appearance.Languages)
        {
            ((ComboBox)window.FindName("QuickLanguage")).SelectedValue=language.Id;await Task.Delay(30);
            Check(Appearance.Load(service.Home).Language==language.Id&&navigation.Items.Count==4,"Four-page language switch persists: "+language.Id);
        }
        ((ComboBox)window.FindName("QuickLanguage")).SelectedValue="zh-CN";await Task.Delay(30);
        await SaveImage(window,Path.Combine(root,"Settings-zh.png"));
        ((ComboBox)window.FindName("QuickLanguage")).SelectedValue="en";await Task.Delay(30);
        Find<TextBox>(window,"CustomAccent").Text="#345678";
        var before=((TextBlock)window.FindName("StatusText")).Text;
        await ExternalUpdate(service,cli);
        await Wait(()=>((TextBlock)window.FindName("LedgerHint")).Text==UiText.Translate("Ledger changed · Refresh","en"));
        Check(Find<TextBox>(window,"CustomAccent").Text=="#345678"&&((TextBlock)window.FindName("StatusText")).Text==before,"External event preserves Settings drafts and action status");
        Navigate("Home");before=((TextBlock)window.FindName("StatusText")).Text;
        await ExternalUpdate(service,cli);
        await Wait(()=>((TextBlock)window.FindName("LedgerHint")).Text==UiText.Translate("已读取事件更新","en"));
        Check(((TextBlock)window.FindName("StatusText")).Text==before,"Board refreshes on external CLI events without overwriting action status");
        var emptyService=new RelayService(Path.Combine(root,"empty"));emptyService.Initialize();
        Appearance.Save(emptyService.Home,new DesktopPreferences("en"));
        var empty=new MainWindow(emptyService);empty.Show();await Task.Delay(100);
        Check(FindAll<TextBlock>(empty).Any(t=>t.Text.Contains("No handoffs yet")),"Empty board explains how to start");
        await Click(empty,"New manual transfer task");
        Check(((Border)empty.FindName("ErrorBanner")).Visibility==Visibility.Visible&&emptyService.Tasks().Count==0,"New handoff explains missing Agents without creating work");
        empty.Close();
    }

    private static async Task CheckNavigationAndSounds(MainWindow window,string root)
    {
        var navigation=(ListBox)window.FindName("Navigation");
        navigation.IsEnabled=false;navigation.ApplyTemplate();window.UpdateLayout();
        var frame=(Border)navigation.Template.FindName("NavigationFrame",navigation);
        Check(frame.Background is SolidColorBrush brush&&brush.Color.A==0,"Disabled navigation retains its transparent background instead of a native white rectangle");
        await SaveImage(window,Path.Combine(root,"Navigation-disabled.png"));
        navigation.IsEnabled=true;
        var frames=new List<int>();
        foreach(var id in new[]{"default","information","warning"})
        {
            using var stream=Application.GetResourceStream(new Uri($"pack://application:,,,/AgentRelay.Desktop;component/Assets/Sounds/{id}.wav")).Stream;
            using var reader=new BinaryReader(stream);
            Check(Encoding.ASCII.GetString(reader.ReadBytes(4))=="RIFF","Embedded notification sound has a WAV header: "+id);
            stream.Position=24;Check(reader.ReadInt32()==22050,"Notification PCM sample rate: "+id);
            stream.Position=40;frames.Add(reader.ReadInt32()/2);
        }
        Check(frames.SequenceEqual(new[]{4410,8159,11466}),"Single chime, ascending pair and three low pulses have distinct embedded recordings");
        NotificationSounds.Play("none");
    }

    private static async Task CheckTray(MainWindow window)
    {
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var installed=(bool)typeof(MainWindow).GetField("trayInstalled",flags)!.GetValue(window)!;
        Check(installed,"Main window installs a system tray entry");
        window.WindowState=WindowState.Minimized;
        await Wait(()=>window.WindowState==WindowState.Minimized);
        Check(window.IsVisible&&window.WindowState==WindowState.Minimized&&window.ShowInTaskbar,"Minimize keeps the window on the taskbar; only Close can hide it to the tray");
        typeof(MainWindow).GetMethod("OpenFromTray",flags)!.Invoke(window,null);
        await Wait(()=>window.IsVisible&&window.WindowState==WindowState.Normal);
        Check(window.IsVisible&&window.WindowState==WindowState.Normal,"Tray Open restores the hidden window");
    }

    private static async Task CheckCloseBehavior(MainWindow owner,string root)
    {
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        void Restore(MainWindow window)=>typeof(MainWindow).GetMethod("OpenFromTray",flags)!.Invoke(window,null);
        RelayService Station(string name, bool? closeToTray=null)
        {
            var station=new RelayService(Path.Combine(root,name));station.Initialize();
            Appearance.Save(station.Home,new DesktopPreferences("en",CloseToTray:closeToTray));return station;
        }
        var station=Station("close-preferences");
        var window=new MainWindow(station){Owner=owner};window.Show();await Task.Delay(100);
        Check(Appearance.Load(station.Home).CloseToTray is null,"Existing preferences without a close choice ask only on the first user close");
        var choice=HandleNextDialog<CloseBehaviorDialog>(window,dialog=>{Press(dialog,"CloseToTrayChoice");return Task.CompletedTask;});
        window.RequestClose();await choice;
        Check(!window.IsVisible&&Appearance.Load(station.Home).CloseToTray==true,"First close can save minimize-to-tray and hide the window");
        Restore(window);window.RequestClose();
        Check(!window.IsVisible&&!Application.Current.Windows.OfType<CloseBehaviorDialog>().Any(),"Later closes reuse the saved tray choice without asking again");
        Restore(window);
        var navigation=(ListBox)window.FindName("Navigation");
        navigation.SelectedItem=navigation.Items.Cast<ListBoxItem>().Single(i=>(string)i.Tag=="Settings");
        await Wait(()=>FindAll<CheckBox>(window).Any(b=>b.Name=="CloseToTraySetting"));
        var setting=Find<CheckBox>(window,"CloseToTraySetting");
        Check(setting.IsChecked==true,"Settings reflects the first close choice");
        setting.IsChecked=false;await Wait(()=>Appearance.Load(station.Home).CloseToTray==false);
        var closed=false;window.Closed+=(_,_)=>closed=true;window.RequestClose();
        Check(closed&&!Application.Current.Windows.OfType<CloseBehaviorDialog>().Any(),"Disabling close-to-tray saves direct exit and never asks again");
        var exitStation=Station("close-first-exit");
        var exitWindow=new MainWindow(exitStation){Owner=owner};exitWindow.Show();await Task.Delay(100);
        var exited=false;exitWindow.Closed+=(_,_)=>exited=true;
        choice=HandleNextDialog<CloseBehaviorDialog>(exitWindow,dialog=>{Press(dialog,"ExitApplicationChoice");return Task.CompletedTask;});
        exitWindow.RequestClose();await choice;
        Check(exited&&Appearance.Load(exitStation.Home).CloseToTray==false,"Choosing direct exit on the first close respects and remembers that choice");
        var trayStation=Station("close-force-exit",true);
        var trayWindow=new MainWindow(trayStation){Owner=owner};trayWindow.Show();await Task.Delay(100);
        var forced=false;trayWindow.Closed+=(_,_)=>forced=true;trayWindow.RequestExit();
        Check(forced&&Appearance.Load(trayStation.Home).CloseToTray==true,"Tray Exit bypasses close-to-tray while preserving the saved preference");
    }

    private static async Task CheckSessionDialogs(MainWindow owner,string root)
    {
        var station=new RelayService(Path.Combine(root,"session-dialogs"));station.Initialize();
        station.RegisterManual("source","fixture","Source");station.RegisterManual("target","fixture","Target");
        var records=new SessionRecord[]{new("older","Older conversation",root,null,null,"unknown",DateTimeOffset.UtcNow.AddDays(-1)),
            new("newer","Newer conversation",root,null,null,"unknown",DateTimeOffset.UtcNow)};
        var cached=new SessionDiscoveryResult("target","fixture",true,records,DateTimeOffset.UtcNow,true);
        new RelayStore(station.Home).Write(s=>s.Execute("INSERT INTO session_discovery_cache VALUES($id,$json)",("$id","target"),("$json",RelayJson.Write(cached))));
        var picker=new SessionPickerDialog(owner,station,"target","en",root);
        var cachedTable=Find<DataGrid>(picker,"SessionsTable");
        Check(cachedTable.Items.Count==2&&cachedTable.SelectedItem is null,"Session Picker shows cached choices immediately without selecting a recent session");
        await Show(picker,dialog=>
        {
            Check(Find<DataGrid>(dialog,"SessionsTable").SelectedItem is null,"Refresh never automatically selects an existing conversation");
            Press(dialog,"UseSelectedSession");
            Check(dialog.SelectedSession is null,"Using an existing conversation requires explicit selection");
            Expand(dialog);Find<TextBox>(dialog,"ManualSessionId").Text="exact-manual-session";
            Find<TextBox>(dialog,"ManualSessionCwd").Text=root;Press(dialog,"UseManualSession");
            return Task.CompletedTask;
        });
        Check(picker.SelectedSession?.SessionId=="exact-manual-session"&&picker.Binding=="manual","Unsupported discovery allows an exact manually entered Session ID without inventing identity");
        var source=Path.Combine(root,"session-dialog-input.md");File.WriteAllText(source,"Choose a destination conversation explicitly.");
        var routing=new HandoffSessionDialog(owner,station,new("source","target",source),"en");
        await Show(routing,async dialog=>
        {
            Check(dialog.Origin is null&&FindAll<TextBlock>(dialog).Any(t=>t.Text.Contains("not identified",StringComparison.Ordinal)),"Pasted handoff honestly reports that its source conversation is unidentified");
            Check(Find<RadioButton>(dialog,"TargetNewSession").IsChecked==true&&!Find<Button>(dialog,"ChooseTargetSession").IsEnabled,"New target conversation is the default without silently choosing an existing conversation");
            Find<RadioButton>(dialog,"TargetExistingSession").IsChecked=true;Press(dialog,"ConfirmSessionHandoff");
            Check(dialog.Target is null&&station.Tasks().Count==0,"Existing-session handoff cannot proceed before a conversation is explicitly selected");
            var selected=HandleNextDialog<SessionPickerDialog>(dialog,child=>
            {
                Expand(child);Find<TextBox>(child,"ManualSessionId").Text="target-bound-id";
                Find<TextBox>(child,"ManualSessionCwd").Text=root;Press(child,"UseManualSession");return Task.CompletedTask;
            });
            Press(dialog,"ChooseTargetSession");await selected;
            Check(dialog.Target?.SessionId=="target-bound-id"&&dialog.TargetBinding=="manual","Routing keeps the exact manually selected target conversation");
            Press(dialog,"CancelSessionHandoff");
        });
        Check(station.Tasks().Count==0,"Cancelling conversation routing creates no task or CLI call");
    }

    private static async Task CheckCliTaskEntry(MainWindow owner,string root)
    {
        var station=new RelayService(Path.Combine(root,"cli-task-ui"));station.Initialize();
        station.RegisterManual("ui-source","fixture","Source");station.RegisterManual("ui-target","fixture","Target");
        var calls=Path.Combine(root,"cli-task-calls.txt");
        var command=new CliCommand("pwsh",["-NoLogo","-NoProfile","-NonInteractive","-Command",
            "$p=[Console]::In.ReadToEnd();[IO.File]::AppendAllText($env:RELAY_UI_CALLS,$p);@{session_id='created-ui-session';reply='work accepted';accepted=$true}|ConvertTo-Json -Compress"],
            "{prompt}",root,new Dictionary<string,string>{{"RELAY_UI_CALLS",calls}});
        station.ImportCliRecipe("ui-target",RelayJson.Write(new CliRecipe(1,"UI entry fixture","fixture",command,null,[],"fixture","local tests","MIT","No model calls.")));
        Appearance.Save(station.Home,new DesktopPreferences("en",NotificationSound:"none"));
        var window=new MainWindow(station){Owner=owner};window.Show();
        await Wait(()=>((TextBlock)window.FindName("StatusText")).Text!="Working…");
        await Modal<CliHandoffDialog>(window,"New automatic transfer task",async dialog=>
        {
            Check(Find<RadioButton>(dialog,"CliTaskNewSession").IsChecked==true,"Explicit CLI task entry offers a new target conversation by default");
            Check(!FindAll<TextBox>(dialog).Any(t=>t.Name=="FileHandoffReply"),"CLI work entry accepts instructions directly without a file or pasted Agent JSON");
            Check(Find<RadioButton>(dialog,"CliTaskSingle").IsChecked==true&&FindAll<RadioButton>(dialog).Any(r=>r.Name=="CliTaskLoop")&&!FindAll<CheckBox>(dialog).Any(c=>c.Name=="CliTaskReviewPlan"),"CLI task offers a single handoff or a review loop instead of a fixed step plan");
            string CardText()=>string.Join(" ",FindAll<TextBlock>(Find<Border>(dialog,"CliTaskBridgeCard")).Select(t=>t.Text));
            Check(CardText().Contains("Target",StringComparison.Ordinal)&&CardText().Contains("CLI bridge, not yet verified",StringComparison.Ordinal),"Choosing a recipient explains which bridge carries its turns: "+CardText());
            var recipientChoice=Find<ComboBox>(dialog,"CliTaskTo");recipientChoice.SelectedValue="ui-source";
            Check(CardText().Contains("has no Agent bridge",StringComparison.Ordinal)&&CardText().Contains("copy the prompt",StringComparison.Ordinal),"A recipient without a bridge is flagged before the task is created");
            await SaveImage(dialog,Path.Combine(root,"New-task-no-bridge.png"));
            recipientChoice.SelectedValue="ui-target";
            Find<TextBox>(dialog,"CliTaskPrompt").Text="UI fixture: perform this direct work request.";
            var selected=HandleNextDialog<SessionPickerDialog>(dialog,child=>
            {
                Expand(child);Find<TextBox>(child,"ManualSessionId").Text="exact-source-ui-session";
                Find<TextBox>(child,"ManualSessionCwd").Text=root;Press(child,"UseManualSession");return Task.CompletedTask;
            });
            Press(dialog,"CliTaskOrigin");await selected;
            Find<CheckBox>(dialog,"CliTaskEnableDirections").IsChecked=true;
            await SaveImage(dialog,Path.Combine(root,"New-cli-task.png"));
            Press(dialog,"SendCliTask");
        });
        await Wait(()=>station.Tasks().Count==1&&station.Tasks().Single().Task.CliFileStatus=="submitted");
        var task=station.Tasks().Single().Task;
        Check(File.Exists(calls)&&task.Mode=="cli"&&task.State==TaskState.Dispatched,"Creating a CLI task invokes the configured CLI through the visible UI path");
        Check(File.ReadAllText(task.RequestPath).Contains("UI fixture: perform this direct work request.",StringComparison.Ordinal),"CLI task persists the user's work request as the actual request document");
        Check(station.TaskSession(task.TaskId,"ui-source")?.SessionId=="exact-source-ui-session"&&station.TaskSession(task.TaskId,"ui-target")?.SessionId=="created-ui-session","CLI UI binds the explicit source and records the returned new target session ID");
        window.RequestExit();
    }

    private static async Task CheckModelDefaults(MainWindow owner,string root)
    {
        var station=new RelayService(Path.Combine(root,"model-ui"));station.Initialize();
        station.RegisterManual("zcode-ui","ZCode Desktop","Zcode UI");
        var config=Path.Combine(root,"model-ui-provider_config.json");
        File.WriteAllText(config,"{\"schemaVersion\":1,\"config\":{\"providerConfigRules\":{\"providerRules\":[{\"providerId\":\"oc\",\"providerName\":\"OpenCode Go\",\"enabled\":true,\"config\":{\"modelOrder\":[\"ds\"]}}]},\"modelConfigRules\":{\"providerModelRules\":[{\"providerId\":\"oc\",\"modelId\":\"ds\",\"config\":{\"enabled\":true,\"optionSpecs\":{\"reasoningLevel\":{\"values\":[\"low\",\"high\",\"max\"]}}}}],\"manualProviderModelRules\":[]}}}");
        var command=new CliCommand(Path.Combine(root,"never-run.exe"),["-p","{prompt}","--json"],null,null,new(){["ZCODE_PERSONAL_PROVIDER_CONFIG_FILE"]=config});
        station.ImportCliRecipe("zcode-ui",RelayJson.Write(new CliRecipe(1,"Zcode fixture","ZCode Desktop",command,null,[],"","","","","zcode-json")));
        Appearance.Save(station.Home,new DesktopPreferences("en",NotificationSound:"none"));
        var window=new MainWindow(station){Owner=owner};window.Show();
        await Wait(()=>((TextBlock)window.FindName("StatusText")).Text!="Working…");
        var navigation=(ListBox)window.FindName("Navigation");
        navigation.SelectedItem=navigation.Items.Cast<ListBoxItem>().Single(i=>(string)i.Tag=="Agents");
        await Wait(()=>FindAll<Button>(window).Any(b=>Equals(b.Tag,"Default model for new conversations")));
        foreach(var expander in FindAll<Expander>(window))expander.IsExpanded=true;
        await Click(window,"Default model for new conversations");
        await Wait(()=>FindAll<ComboBox>(window).Any(c=>c.Name=="AgentDefaultReasoning"));
        var level=Find<ComboBox>(window,"AgentDefaultReasoning");
        Check((string?)level.SelectedItem=="high","Without a saved default the reasoning level starts at the middle of the model's scale");
        await Click(window,"Save default model");
        await Wait(()=>System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(config))!["config"]!["defaultModelSelection"] is not null);
        var saved=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(config))!["config"]!["defaultModelSelection"]!;
        Check(saved["modelId"]!.GetValue<string>()=="ds"&&saved["options"]!["reasoningLevel"]!.GetValue<string>()=="high","Saving writes the host's default model and reasoning level");
        Check(FindAll<TextBlock>(window).Any(t=>t.Text.Contains("Continued conversations keep their own model",StringComparison.Ordinal)),"The panel says continued conversations keep their own model");
        await SaveImage(window,Path.Combine(root,"Agent-default-model.png"));
        window.RequestExit();
    }

    private static async Task CheckReviewLoopPane(MainWindow owner,string root)
    {
        var station=new RelayService(Path.Combine(root,"review-loop-ui"));station.Initialize();
        station.RegisterManual("loop-author","manual","Author");station.RegisterManual("loop-reviewer","manual","Reviewer");
        var work=Path.Combine(root,"loop-work.md");File.WriteAllText(work,"Write the release notes.");
        var task=station.CreateFileHandoff(new("loop-reviewer","loop-author",work,"Write and submit")).Task;
        station.ConfigureFileHandoff(task.TaskId,new(new CollaborationOptions("loop-author","Covers every change",1)));
        void Receipt(string outcome,string[]? items=null,string? deliverable=null)
        {
            task=station.Status(task.TaskId).Task;
            var reply=station.TurnReplyPath(task);File.WriteAllText(reply,"Version "+task.ManualTurn);
            File.WriteAllText(station.ManualReceiptPath(task),RelayJson.Write(new ManualReceipt(task.TaskId,task.AttemptId,task.AwaitingAgentId!,task.ManualTurn,"done",
                null,outcome=="submitted"?reply:null,null,outcome,items,deliverable)));
            station.ImportManualReceipts();
        }
        Receipt("submitted");Receipt("changes_requested",["Missing the CLI changes"]);Receipt("submitted");
        new RelayStore(station.Home).Write(s=>
        {
            var current=s.Task(task.TaskId);
            current.Collaboration!.OpenItems=["LONG_REQUIREMENT: "+new string('x',400)];
            current.ManualNextAction="LONG_NEXT_ACTION: fix and review";
            s.Save(current);return true;
        });
        Check(station.Status(task.TaskId).Task is {State:TaskState.Paused,PauseReason:"round_limit"},"Review loop pauses when its review budget is used up");
        Appearance.Save(station.Home,new DesktopPreferences("en",NotificationSound:"none"));
        var window=new MainWindow(station){Owner=owner};window.Show();
        await Wait(()=>((TextBlock)window.FindName("StatusText")).Text!="Working…");
        var table=Find<DataGrid>(window,"TasksTable");table.SelectedItem=table.Items.Cast<object>().Single();
        await Wait(()=>FindAll<Button>(window).Any(b=>Equals(b.Tag,"Continue with 2 more rounds")));
        Check(Find<TextBlock>(window,"HandoffRoundSummary").Text=="Completed steps: 3 · Completed reviews: 1 (limit: 1)"&&Find<TextBlock>(window,"HandoffNextRecipient").Text.Contains("Reviewer"),"Compact task summary distinguishes completed steps, completed reviews and the review limit");
        var contents=Find<Expander>(window,"ManualFileContents");
        var contentBlocks=FindAll<TextBlock>(contents).ToHashSet();
        Check(!contents.IsExpanded&&FindAll<TextBlock>(window).Where(t=>t.Text.Contains("LONG_REQUIREMENT")||t.Text.Contains("LONG_NEXT_ACTION")||t.Text.Contains("Covers every change")).All(contentBlocks.Contains),"Long requirements and next actions appear only inside collapsed file contents");
        Check(FindAll<Expander>(window).Single(e=>Equals(e.Header,"Review history")).IsExpanded==false,"Detailed review history stays collapsed");
        contents.IsExpanded=true;window.UpdateLayout();
        Check(Find<TextBox>(window,"ManualFilePreview").Text==File.ReadAllText(station.Status(task.TaskId).Task.ManualCurrentFile!),"Expanding file contents preserves the actual file body separately from metadata");
        contents.IsExpanded=false;
        Check(!FindAll<Button>(window).Any(b=>Equals(b.Tag,"Copy prompt for Reviewer")),"Paused loop does not offer to hand the next turn on before the user decides");
        await SaveImage(window,Path.Combine(root,"Review-loop-paused.png"));
        await Click(window,"Continue with 2 more rounds");
        await Wait(()=>station.Status(task.TaskId).Task.State==TaskState.AwaitingReply);
        Check(station.Status(task.TaskId).Task.Collaboration!.MaxReviewRounds==3,"Continuing records the user's extra review rounds");
        Receipt("approved");Receipt("accepted",deliverable:"Final release notes");
        await Click(window,"Refresh");
        Check(Find<TextBlock>(window,"HandoffRoundSummary").Text=="Completed steps: 5 · Completed reviews: 2 (limit: 3)"&&station.Status(task.TaskId).Task.State==TaskState.Succeeded,"Completed task reports all five steps and only the two reviews actually performed");
        window.RequestExit();
    }

    private static async Task CheckNotificationDelivery(MainWindow owner,string root)
    {
        var service=new RelayService(Path.Combine(root,"notification-ui"));service.Initialize();
        service.RegisterManual("notify-a","manual","Writer");service.RegisterManual("notify-b","manual","Reader");
        service.EnableDesktopFallback(Path.Combine(root,"removed-package","AgentRelay.Notify.exe"));
        Appearance.Save(service.Home,new DesktopPreferences("en",NotificationSound:"none"));
        var window=new MainWindow(service){Owner=owner};window.Show();await Task.Delay(100);
        var navigation=(ListBox)window.FindName("Navigation");
        navigation.SelectedItem=navigation.Items.Cast<ListBoxItem>().Single(i=>(string)i.Tag=="Settings");
        await Wait(()=>FindAll<ComboBox>(window).Any(c=>c.Name=="NotificationSoundChoice"));
        var sounds=Find<ComboBox>(window,"NotificationSoundChoice");
        Check(sounds.Items.Count==5,"Notification settings offer silent, default, information, warning and error sounds");
        sounds.SelectedValue="warning";await Wait(()=>Appearance.Load(service.Home).NotificationSound=="warning");
        sounds.SelectedValue="none";await Wait(()=>Appearance.Load(service.Home).NotificationSound=="none");
        Check(FindAll<Button>(window).Any(b=>Equals(b.Tag,"Preview sound")),"Notification sound selection persists and has an explicit preview action");
        await Click(window,"Send test notification");
        await Wait(()=>Application.Current.Windows.Cast<Window>().Any(w=>w.Name=="InAppNotification"));
        var popup=Application.Current.Windows.Cast<Window>().Single(w=>w.Name=="InAppNotification");
        Check(popup.ShowInTaskbar==false&&popup.Topmost&&service.Tasks().Count==0,"Missing native helper produces a visible in-app test notification without creating a task");
        var header=Find<Grid>(popup,"NotificationHeader");
        Check(ReferenceEquals(Find<Button>(popup,"CloseInAppNotification").Parent,header),"Notification close button shares the compact title header");
        Check(ReferenceEquals(popup.Resources,window.Resources),"Notification inherits the active application theme");
        Check(Find<Border>(popup,"NotificationCard").CornerRadius.TopLeft==14,"Notification uses a rounded themed card");
        await SaveImage(popup,Path.Combine(root,"In-app-notification.png"));popup.Close();
        var file=Path.Combine(root,"notification-work.md");File.WriteAllText(file,"A local notification fixture.");
        var task=service.CreateFileHandoff(new("notify-a","notify-b",file)).Task;
        await Click(window,"Refresh");
        await Wait(()=>Application.Current.Windows.Cast<Window>().Any(w=>w.Name=="InAppNotification"));
        popup=Application.Current.Windows.Cast<Window>().Single(w=>w.Name=="InAppNotification");
        Check(FindAll<TextBlock>(popup).Any(t=>t.Text.Contains("Reader",StringComparison.Ordinal)),"Waiting notifications identify the current receiving Agent");popup.Close();
        await Click(window,"Refresh");
        Check(!Application.Current.Windows.Cast<Window>().Any(w=>w.Name=="InAppNotification"),"Refreshing the same event does not repeat its notification");
        File.WriteAllText(service.ManualReceiptPath(task),RelayJson.Write(new ManualReceipt(task.TaskId,task.AttemptId,"notify-b",0,"done",false)));
        await Click(window,"Refresh");
        await Wait(()=>service.Status(task.TaskId).Task.State==TaskState.Succeeded);
        await Wait(()=>Application.Current.Windows.Cast<Window>().Any(w=>w.Name=="InAppNotification"));
        popup=Application.Current.Windows.Cast<Window>().Single(w=>w.Name=="InAppNotification");
        Check(FindAll<TextBlock>(popup).Any(t=>t.Text.Contains("已完成",StringComparison.Ordinal)),"Completion still updates the task and notifies when the configured helper no longer exists");
        popup.Close();window.RequestExit();
    }

    private static Task HandleNextDialog<T>(Window owner,Func<T,Task> exercise) where T:Window
    {
        var done=new TaskCompletionSource();
        _=owner.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(async()=>
        {
            var dialog=Application.Current.Windows.OfType<T>().Single(w=>w.Owner==owner);
            try{await exercise(dialog);done.SetResult();}
            catch(Exception error){dialog.Close();done.SetException(error);}
        }));
        return done.Task;
    }

    private static async Task CheckTaskOperatorActions(MainWindow window,RelayService service,string from,string root)
    {
        var source=Path.Combine(root,"operator-action.md");File.WriteAllText(source,"Work that the user may end manually.");
        var task=service.CreateFileHandoff(new(from,"agent-c",source)).Task;
        await Click(window,"Refresh");SelectTask(window,task.TaskId);
        await ConfirmAction(window,"Mark handoff complete",false);
        Check(service.Status(task.TaskId).Task.State==TaskState.AwaitingManual,"Cancelling manual completion preserves the active handoff");
        await ConfirmAction(window,"Mark handoff complete",true);
        Check(service.Status(task.TaskId).Task.State==TaskState.Succeeded&&service.Status(task.TaskId).Task.ResumeStatus=="operator_completed","Confirmed manual completion stops waiting and records an operator decision");
        Check(FindAll<TextBlock>(window).Any(t=>t.Text.Contains("Agent completion was not verified",StringComparison.Ordinal)),"Manually completed handoff identifies its result as an operator decision");
        await ConfirmAction(window,"Delete task",false);
        Check(service.Tasks().Any(v=>v.Task.TaskId==task.TaskId),"Cancelling deletion retains the task and history");
        await ConfirmAction(window,"Delete task",true);
        Check(service.Tasks().All(v=>v.Task.TaskId!=task.TaskId)&&service.Audit(task.TaskId).Count==0,"Confirmed deletion removes the task and its history from the board");
        Check(File.Exists(source)&&File.Exists(task.RequestPath),"Deleting a task preserves original and managed files");
        var active=service.CreateFileHandoff(new(from,"agent-c",source)).Task;
        await Click(window,"Refresh");SelectTask(window,active.TaskId);
        await ConfirmAction(window,"Delete task",true);
        Check(service.Tasks().All(v=>v.Task.TaskId!=active.TaskId),"User can confirm deletion of an unfinished handoff");
    }

    private static async Task CheckFileCancellation(MainWindow owner,string root)
    {
        var service=new RelayService(Path.Combine(root,"cancel-file-task"));service.Initialize();
        service.RegisterManual("source","fixture","Source");service.RegisterManual("target","fixture","Target");
        var file=Path.Combine(root,"cancel-file.md");File.WriteAllText(file,"Existing work stays on disk.");
        var task=service.CreateFileHandoff(new("source","target",file)).Task;
        new RelayStore(service.Home).Write(s=>
        {
            var current=s.Task(task.TaskId);current.CliFileTurn=current.ManualTurn;current.CliFileStatus="submitting";s.Save(current);return true;
        });
        Appearance.Save(service.Home,new DesktopPreferences("en",NotificationSound:"none"));
        var window=new MainWindow(service){Owner=owner};window.Show();await Task.Delay(100);SelectTask(window,task.TaskId);
        await ConfirmAction(window,"Cancel",false);
        Check(service.Status(task.TaskId).Task.State==TaskState.AwaitingManual,"Declining cancellation leaves an in-flight handoff unchanged");
        await ConfirmAction(window,"Cancel",true);
        Check(service.Status(task.TaskId).Task.State==TaskState.Cancelled&&File.Exists(file),"Confirmed cancellation ends an in-flight Relay task while keeping its files");
        File.WriteAllText(service.ManualReceiptPath(task),RelayJson.Write(new ManualReceipt(task.TaskId,task.AttemptId,"target",0,"done",false)));
        service.ImportManualReceipts();
        Check(service.Status(task.TaskId).Task.State==TaskState.Cancelled,"A late receipt cannot restart a cancelled handoff");
        window.RequestExit();
    }

    private static async Task CheckBlockedDeliveryUi(MainWindow owner,string root)
    {
        var service=new RelayService(Path.Combine(root,"blocked-delivery"));service.Initialize();
        service.RegisterManual("source","fixture","Source");service.RegisterManual("target","fixture","Target");
        SeedExtension(service,"target",true);service.SetPermission("source","target","allow");
        var file=Path.Combine(root,"blocked-work.md");File.WriteAllText(file,"Blocked fixture.");
        var task=service.CreateFileHandoff(new("source","target",file)).Task;
        var extension=service.CliExtension("target")!;
        SaveExtension(service,extension with {Block=new("auth_required","The desktop bridge rejected authentication.",DateTimeOffset.UtcNow)});
        Appearance.Save(service.Home,new DesktopPreferences("en",NotificationSound:"none"));
        var window=new MainWindow(service){Owner=owner};window.Show();await Task.Delay(100);
        SelectTask(window,task.TaskId);
        var row=Find<DataGrid>(window,"TasksTable").SelectedItem;
        Check(((string)row.GetType().GetProperty("State")!.GetValue(row)!).Contains("Bridge blocked"),"Blocked bridge is visible in the task row instead of waiting for the Agent");
        Check(!FindAll<Button>(window).Any(b=>Equals(b.Tag,"Send via bridge"))&&FindAll<Button>(window).Any(b=>Equals(b.Tag,"Check Agent bridge")),"Blocked tasks offer bridge diagnostics instead of an ineffective Send button");
        Check(FindAll<TextBlock>(window).Any(t=>t.Text.Contains("rejected authentication")),"The task card preserves the actual bridge failure");
        await Click(window,"Copy bridge diagnostic prompt");
        Check(Clipboard.GetText().Contains(service.Home)&&Clipboard.GetText().Contains("auth_required")&&Clipboard.GetText().Contains("\"agent_id\": \"target\""),"A blocked task copies diagnostics for its receiving Agent without resending");
        await SaveImage(window,Path.Combine(root,"Blocked-task.png"));
        new RelayStore(service.Home).Write(s=>
        {
            var current=s.Task(task.TaskId);current.DeadlineAt=DateTimeOffset.UtcNow.AddMinutes(-1);s.Save(current);return true;
        });
        service.ImportManualReceipts();await Click(window,"Refresh");
        Check(service.Status(task.TaskId).Task.PauseReason=="delivery_failed"&&FindAll<Button>(window).Any(b=>Equals(b.Tag,"Check Agent bridge")),"A paused delivery preserves its bridge failure and diagnostic action");
        await Click(window,"Check Agent bridge");
        Check(FindAll<DataGrid>(window).Any(g=>g.Name=="AgentsTable"),"Task diagnostics open Agent management");
        Check(FindAll<TextBlock>(window).Any(t=>t.Text.Contains("Saved failure at")),"Agent management labels the blocker as a saved failure rather than a new connection result");
        await Click(window,"Check bridge setup (no model call)");
        await Click(window,"Copy bridge diagnostic prompt");
        Check(Clipboard.GetText().Contains("\"agent_id\": \"target\"")&&service.Status(task.TaskId).Task.State==TaskState.Paused&&service.CliExtension("target")!.Block is not null,"Agent diagnostics and copying leave tasks, verification and blockers unchanged");
        var saved=service.CliExtension("target")!;
        SaveExtension(service,saved with {Recipe=saved.Recipe with {OutputFormat="dsh-desktop",Create=new("relay-test-"+Guid.NewGuid().ToString("N")+".exe",[],null,null,new(){["DSH_HOME"]=Path.Combine(root,"missing-desktop")}),Resume=null}});
        await Click(window,"Refresh");
        Check(FindAll<Button>(window).Any(b=>Equals(b.Tag,"Recheck and restore bridge")),"DSH authentication blockers offer an explicit recheck-and-restore action");
        await Click(window,"Recheck and restore bridge");
        Check(((Border)window.FindName("ErrorBanner")).Visibility==Visibility.Visible&&service.CliExtension("target")!.Block is not null&&service.Status(task.TaskId).Task.State==TaskState.Paused,"An unsuccessful restore check keeps the blocker and does not send or continue the task");
        ((Button)window.FindName("DismissErrorButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(((Border)window.FindName("ErrorBanner")).Visibility==Visibility.Collapsed&&service.CliExtension("target")!.Block is not null&&service.Status(task.TaskId).Task.State==TaskState.Paused,"Closing the error banner hides the message without changing the bridge or task");
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        typeof(MainWindow).GetField("errorTaskId",flags)!.SetValue(window,task.TaskId);
        typeof(MainWindow).GetField("errorTaskTurn",flags)!.SetValue(window,task.ManualTurn);
        ((Border)window.FindName("ErrorBanner")).Visibility=Visibility.Visible;
        new RelayStore(service.Home).Write(s=>{var current=s.Task(task.TaskId);current.State=TaskState.Succeeded;s.Save(current);return true;});
        await (Task)typeof(MainWindow).GetMethod("Run",flags)!.Invoke(window,new object[]{(Func<Task>)(()=>Task.CompletedTask),"Ready",true,false,false})!;
        Check(((Border)window.FindName("ErrorBanner")).Visibility==Visibility.Collapsed&&service.CliExtension("target")!.Block is not null,
            "Completed task clears its stale delivery banner while retaining bridge failure diagnostics");
        window.RequestExit();
    }

    private static async Task CheckBatchTasks(MainWindow owner,string root)
    {
        var service=new RelayService(Path.Combine(root,"batch-tasks"));service.Initialize();
        service.RegisterManual("batch-a","fixture","Writer");service.RegisterManual("batch-b","fixture","Reader");
        var files=new[]{"first","second","unselected"}.Select(name=>Path.Combine(root,"batch-work-"+name+".md")).ToArray();
        foreach(var source in files)File.WriteAllText(source,"Batch fixture only.");
        var file=files[0];
        TaskRecord Create(int index)=>service.CreateFileHandoff(new("batch-a","batch-b",files[index])).Task;
        var first=Create(0);var second=Create(1);var unselected=Create(2);
        Check(service.Tasks().Count==3&&new[]{first.TaskId,second.TaskId,unselected.TaskId}.Distinct().Count()==3,
            "Batch fixture creates three distinct handoffs instead of reusing the same active source file");
        Appearance.Save(service.Home,new DesktopPreferences("en",NotificationSound:"none"));
        var window=new MainWindow(service){Owner=owner};window.Show();await Task.Delay(100);
        CheckBox Choice(string id)=>ByAutomation<CheckBox>(window,"SelectTask_"+id);
        void ToggleChoice(string id)=>Choice(id).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left)
            {RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent});
        bool SelectedInModel(string id)
        {
            var row=Find<DataGrid>(window,"TasksTable").Items.Cast<object>().Single(item=>(string)item.GetType().GetProperty("TaskId")!.GetValue(item)! == id);
            return (bool)row.GetType().GetProperty("BatchSelected")!.GetValue(row)!;
        }
        bool ClearShown()=>Find<Button>(window,"ClearTaskSelection").Visibility==Visibility.Visible;
        SelectTask(window,unselected.TaskId);window.UpdateLayout();
        var table=Find<DataGrid>(window,"TasksTable");
        Check(table.Columns[0] is DataGridTemplateColumn,"Task board has a leading checkbox column for batch management");
        ToggleChoice(first.TaskId);
        Check(Choice(first.TaskId).IsChecked==true&&SelectedInModel(first.TaskId)&&!ClearShown(),"Selecting one task updates its model and shows actions without Clear selection");
        Check((string)table.SelectedItem.GetType().GetProperty("TaskId")!.GetValue(table.SelectedItem)! == unselected.TaskId,
            "Checkbox selection does not replace the task currently being inspected");
        ToggleChoice(second.TaskId);await Task.Delay(20);
        Check(SelectedInModel(second.TaskId)&&ClearShown()&&System.Windows.Data.BindingOperations.IsDataBound(Choice(second.TaskId),CheckBox.IsCheckedProperty),
            "Mouse selection updates the second task model, preserves binding and reveals Clear selection");
        await Click(window,"Refresh");
        Check(Choice(first.TaskId).IsChecked==true&&Choice(second.TaskId).IsChecked==true&&Choice(unselected.TaskId).IsChecked==false,
            "Batch selection survives refresh by exact task ID");
        await Click(window,"Clear selection");
        Check(!ClearShown()&&Choice(first.TaskId).IsChecked==false&&Choice(second.TaskId).IsChecked==false,
            "Clear selection unchecks every task");
        Check(Find<Button>(window,"SelectAllTasks").IsEnabled,"Select all is available when no task is checked");
        await Click(window,"Select all tasks");
        Check(new[]{first,second,unselected}.All(task=>SelectedInModel(task.TaskId))&&ClearShown()&&!Find<Button>(window,"SelectAllTasks").IsEnabled,
            "Select all checks every listed task and reveals Clear selection");
        await Click(window,"Clear selection");
        ToggleChoice(first.TaskId);ToggleChoice(second.TaskId);
        await ConfirmAction(window,"Mark selected complete",false);
        Check(service.Tasks().All(view=>view.Task.State==TaskState.AwaitingManual),"Cancelling bulk completion changes no selected or unselected task");
        await ConfirmAction(window,"Mark selected complete",true);
        Check(new[]{first,second}.All(task=>service.Status(task.TaskId).Task.ResumeStatus=="operator_completed")&&
            service.Status(unselected.TaskId).Task.State==TaskState.AwaitingManual,"Bulk completion marks exactly the checked tasks and leaves the other task active");
        ToggleChoice(first.TaskId);ToggleChoice(second.TaskId);
        await SaveImage(window,Path.Combine(root,"Task-batch-selection.png"));
        await ConfirmAction(window,"Delete selected tasks",false);
        Check(service.Tasks().Count==3,"Cancelling bulk deletion retains all task history");
        await ConfirmAction(window,"Delete selected tasks",true);
        Check(service.Tasks().Single().Task.TaskId==unselected.TaskId&&File.Exists(file)&&File.Exists(first.RequestPath),
            "Bulk deletion removes only checked tasks and preserves their files");
        ToggleChoice(unselected.TaskId);service.DeleteTask(unselected.TaskId,true);
        await Click(window,"Refresh");
        Check(Find<WrapPanel>(window,"TaskBatchActions").Visibility==Visibility.Collapsed&&!ClearShown(),
            "Refresh discards selections for tasks removed by another client");
        window.RequestExit();
    }

    private static async Task ConfirmAction(MainWindow owner,string button,bool accept)
    {
        var handle=new System.Windows.Interop.WindowInteropHelper(owner).Handle;
        var thread=GetWindowThreadProcessId(handle,out _);
        var response=Task.Run(async()=>
        {
            for(var attempt=0;attempt<100;attempt++)
            {
                nint dialog=0;
                EnumThreadWindows(thread,(candidate,_)=>
                {
                    var className=new StringBuilder(128);GetClassName(candidate,className,className.Capacity);
                    if(className.ToString()=="#32770"&&GetWindow(candidate,4)==handle){dialog=candidate;return false;}
                    return true;
                },0);
                if(dialog!=0){SendMessage(dialog,0x111,accept?1:2,0);return;}
                await Task.Delay(50);
            }
            throw new TimeoutException("Expected confirmation dialog did not appear.");
        });
        await Click(owner,button);await response;
    }

    private static void CheckGraphCliStates(Window owner)
    {
        var agents=Enumerable.Range(0,3).Select(i=>new AgentRecord("status-"+i,"manual","",null,null,[],"manual_ready",null,"Status "+i)).ToArray();
        var edges=(from a in agents from b in agents where a.AgentId!=b.AgentId select new ConnectionView(a.AgentId,b.AgentId,"default_deny","manual","Unavailable","")).ToArray();
        var command=new CliCommand("never-executed-fixture",["{prompt}"],null,null);
        var recipe=new CliRecipe(1,"Status fixture","fixture",command,null,[],"fixture","local test","MIT","Never executed.");
        var records=new Dictionary<string,CliExtensionRecord>
        {
            ["status-0"]=new("status-0","registered",recipe,null,null),
            ["status-1"]=new("status-1","failed",recipe,new("failed","create","fixture",null,false,false,"fixture failure",DateTimeOffset.UtcNow),null)
        };
        var graph=new ConnectionGraph(agents,edges,_=>{},"en",owner,extensions:records);
        var registered=ByAutomation<System.Windows.Shapes.Ellipse>(graph,"CliIndicator:status-0");
        var failed=ByAutomation<System.Windows.Shapes.Ellipse>(graph,"CliIndicator:status-1");
        Check(registered.Fill==owner.FindResource("AllowedBrush")&&registered.ToolTip.ToString()!.Contains("not yet verified"),"Registered CLI method has a green indicator without claiming a successful probe");
        Check(failed.Fill==owner.FindResource("DeniedBrush")&&failed.ToolTip.ToString()!.Contains("probe failed"),"Failed CLI probe has a red indicator and a test-result explanation");
    }

    private delegate bool EnumThreadWindowCallback(nint window,nint state);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window,out uint process);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool EnumThreadWindows(uint thread,EnumThreadWindowCallback callback,nint state);
    [DllImport("user32.dll",EntryPoint="GetClassNameW",CharSet=CharSet.Unicode)] private static extern int GetClassName(nint window,StringBuilder className,int count);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window,uint command);
    [DllImport("user32.dll",EntryPoint="SendMessageW")] private static extern nint SendMessage(nint window,uint message,nint wParam,nint lParam);
    private static async Task CheckProbeProgress(MainWindow window,RelayService service,string root)
    {
        const string id="progress-agent";
        service.RegisterManual(id,"fixture","进度探针");
        var script=Path.Combine(root,"probe-progress.ps1");var started=Path.Combine(root,"probe-started");
        var release=Path.Combine(root,"probe-release");var fail=Path.Combine(root,"probe-fail");
        File.WriteAllText(script,"""
            $text=[Console]::In.ReadToEnd()
            Set-Content -LiteralPath $args[0] -Value started
            while (-not (Test-Path -LiteralPath $args[1])) { Start-Sleep -Milliseconds 25 }
            $reply=[regex]::Match($text,'RELAY-[A-Z0-9]+').Value
            if (Test-Path -LiteralPath $args[2]) { $reply='wrong-marker' }
            $file=[regex]::Match($text,'Create the file (.+?) containing').Groups[1].Value
            if ($file) { [IO.File]::WriteAllText($file,$reply) }
            @{session_id='progress-session';reply=$reply;accepted=$true}|ConvertTo-Json -Compress
            """);
        var command=new CliCommand("pwsh",["-NoLogo","-NoProfile","-NonInteractive","-File",script,started,release,fail],"{prompt}",null);
        service.ImportCliRecipe(id,RelayJson.Write(new CliRecipe(1,"Progress fixture","fixture",command,null,[],"fixture","local test","MIT","No model calls.")));
        await Click(window,"One-click setup");
        var agents=Find<DataGrid>(window,"AgentsTable");
        agents.SelectedItem=agents.Items.Cast<object>().Single(row=>((AgentRecord)row.GetType().GetProperty("Source")!.GetValue(row)!).AgentId==id);
        await Modal<CliProbeDialog>(window,"Test bridge",async dialog=>
        {
            try
            {
            for(var attempt=0;attempt<2;attempt++)
            {
                File.Delete(started);File.Delete(release);if(attempt==1)File.WriteAllText(fail,"fail");
                Press(dialog,"ProbeCreate");
                await Wait(()=>File.Exists(started),400);
                Check(((TextBlock)window.FindName("StatusText")).Text=="Running CLI probe…" && Find<TextBlock>(dialog,"CliProbeStatus").Text=="Running CLI probe…","Parent and test window show running status while the CLI process is actually blocked");
                await SaveImage(window,Path.Combine(root,"Probe-running-"+attempt+".png"));
                File.WriteAllText(release,"finish");
                var expected=attempt==0?"CLI probe passed · Confirm session visibility":"CLI probe failed · See the test result";
                await Wait(()=>((TextBlock)window.FindName("StatusText")).Text==expected);
                Check(Find<TextBlock>(dialog,"CliProbeStatus").Text==expected,"Probe result remains visible instead of reverting to an idle-window message");
                Check(!service.CliExtension(id)!.CanCreate,"Progress tests do not auto-confirm visibility");
            }
            }
            finally
            {
                File.WriteAllText(release,"finish");
                await Wait(()=>Find<Button>(dialog,"CloseCliProbe").IsEnabled);
            }
            Press(dialog,"CloseCliProbe");
        });
        var navigation=(ListBox)window.FindName("Navigation");
        navigation.SelectedItem=navigation.Items.Cast<ListBoxItem>().Single(item=>(string)item.Tag=="Settings");
        window.UpdateLayout();
    }
    private static async Task CheckGraphPairs(Window owner,string root)
    {
        foreach(var count in new[]{2,3,4,6,8})
        {
            var agents=Enumerable.Range(0,count).Select(i=>new AgentRecord("node-"+i,"manual","",null,null,[],"manual_ready",null,"节点 "+(i+1))).ToArray();
            var edges=(from a in agents from b in agents where a.AgentId!=b.AgentId
                select new ConnectionView(a.AgentId,b.AgentId,"default_deny","manual","Unavailable","")).ToArray();
            var graph=new ConnectionGraph(agents,edges,_=>{},"zh-CN",owner,id=>agents.Single(a=>a.AgentId==id).DisplayName!);
            var fixture=new Window{Owner=owner,Width=1000,Height=740,Resources=owner.Resources,Background=owner.Background,Content=graph,WindowStartupLocation=WindowStartupLocation.CenterOwner};
            fixture.Show();fixture.UpdateLayout();
            Check(FindAll<System.Windows.Shapes.Path>(graph).Count(p=>AutomationProperties.GetAutomationId(p).StartsWith("ConnectionPair:"))==count*(count-1)/2,$"{count} nodes draw exactly one selectable line per pair");
            Check(!FindAll<System.Windows.Shapes.Polygon>(graph).Any(p=>AutomationProperties.GetAutomationId(p).StartsWith("ConnectionArrow:")),$"{count}-node unavailable graph has no route arrowheads");
            Check(!FindAll<Border>(graph).Any(b=>AutomationProperties.GetAutomationId(b).StartsWith("ConnectionLabel:")),$"{count}-node graph avoids repeated labels on lines");
            if(count is 4 or 8)await SaveImage(fixture,Path.Combine(root,$"Connections-{count}-nodes.png"));
            fixture.Close();
        }
        var pairAgents=new[]{new AgentRecord("left","manual","",null,null,[],"manual_ready",null,"左节点"),new AgentRecord("right","manual","",null,null,[],"manual_ready",null,"右节点")};
        foreach(var (forward,reverse,arrows) in new[]{("Unavailable","Unavailable",0),("Allowed","Unavailable",1),("Allowed","Allowed",2),("Allowed","Denied",2)})
        {
            var edges=new[]{new ConnectionView("left","right","allow","CLI",forward,""),new ConnectionView("right","left","deny","CLI",reverse,"")};
            ConnectionView? selected=null;
            var graph=new ConnectionGraph(pairAgents,edges,edge=>selected=edge,"en",owner);
            Check(FindAll<System.Windows.Shapes.Polygon>(graph).Count(p=>AutomationProperties.GetAutomationId(p).StartsWith("ConnectionArrow:"))==arrows,$"{forward}/{reverse} pair uses {arrows} route arrowheads");
            var hit=ByAutomation<System.Windows.Shapes.Path>(graph,"ConnectionPair:left:right");
            hit.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});
            Check(selected?.From=="left"&&selected.To=="right","Clicking a shared pair selects its canonical direction for the two-direction detail pane");
            if(reverse=="Denied")
            {
                var outgoing=ByAutomation<System.Windows.Shapes.Polygon>(graph,"ConnectionArrow:left:right");
                var incoming=ByAutomation<System.Windows.Shapes.Polygon>(graph,"ConnectionArrow:right:left");
                Check(outgoing.Fill==owner.FindResource("AllowedBrush")&&incoming.Fill==owner.FindResource("DeniedBrush"),"Mixed pair preserves independent green and red arrowheads");
            }
        }
    }
    private static void SeedExtension(RelayService service,string id,bool confirmed)
    {
        var command=new CliCommand(Path.Combine(service.Home,"never-executed-fixture.exe"),["{prompt}"],null,null);
        var recipe=new CliRecipe(1,"Smoke fixture","fixture",command,command,[],"fixture","local smoke fixture","MIT","Never executed.");
        SaveExtension(service,new(id,Guid.NewGuid().ToString(),recipe,new("create-fixture-"+id,"create","CREATE-FIXTURE","fixture-session",true,confirmed,null,DateTimeOffset.UtcNow,FileWritten:true),null));
    }
    private static void SaveExtension(RelayService service,CliExtensionRecord record)
    {
        var store=new RelayStore(service.Home);
        store.Write(s=>{s.Execute("INSERT INTO cli_extensions VALUES($id,$json) ON CONFLICT(agent_id) DO UPDATE SET json=excluded.json",("$id",record.AgentId),("$json",RelayJson.Write(record)));return true;});
    }
    private static async Task CheckGlyphs(Window dialog,TextBox box)
    {
        foreach(var size in new[]{13d,18d,24d})
        {
            box.FontSize=size;box.Text="中文 Agjpqy";box.BringIntoView();box.Focus();dialog.UpdateLayout();
            await dialog.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            var content=(ScrollViewer)box.Template.FindName("PART_ContentHost",box);
            var bounds=content.TransformToAncestor(box).TransformBounds(new Rect(0,0,content.ActualWidth,content.ActualHeight));
            foreach(var index in new[]{0,1,4,5,6,7,8})
            {
                var glyph=box.GetRectFromCharacterIndex(index);
                Check(!glyph.IsEmpty&&glyph.Height>0&&glyph.Top>=bounds.Top-1&&glyph.Bottom<=bounds.Bottom+1,$"Text glyph {index} fully fits viewport at {size}px");
            }
        }
    }
    private static async Task ExternalUpdate(RelayService service,string cli)
    {
        var dotnet=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??throw new InvalidOperationException("DOTNET_HOST_PATH required.");
        var start=new ProcessStartInfo(dotnet){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{cli,"--home",service.Home,"permission","set","--from","agent-c","--to","agent-a","--decision","deny"})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        if(process.ExitCode!=0)throw new InvalidOperationException(await stderr);
        await stdout;Check(process.ExitCode==0,"External CLI updated ledger");
    }
    private static async Task Modal<T>(MainWindow owner,string button,Func<T,Task> exercise) where T:Window
    {
        var done=new TaskCompletionSource();
        _=owner.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(async()=>
        {
            var dialog=Application.Current.Windows.OfType<T>().Single();
            try{await exercise(dialog);done.SetResult();}
            catch(Exception error){dialog.Close();done.SetException(error);}
        }));
        await Click(owner,button);await done.Task;
    }
    private static async Task Show<T>(T dialog,Func<T,Task> exercise) where T:Window
    {
        var done=new TaskCompletionSource();
        dialog.ContentRendered+=async(_,_)=>{try{await exercise(dialog);done.SetResult();}catch(Exception error){dialog.Close();done.SetException(error);}};
        dialog.ShowDialog();await done.Task;
    }
    private static void Expand(DependencyObject root){foreach(var expander in FindAll<Expander>(root))expander.IsExpanded=true;if(root is Window window)window.UpdateLayout();}
    private static void Press(DependencyObject root,string name)=>Find<Button>(root,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void SelectTask(MainWindow window,string id)
    {var table=Find<DataGrid>(window,"TasksTable");table.SelectedItem=table.Items.Cast<object>().Single(r=>(string)r.GetType().GetProperty("TaskId")!.GetValue(r)! == id);}
    private static async Task Click(MainWindow window,string label)
    {
        await Wait(()=>((FrameworkElement)window.FindName("PageContent")).IsEnabled);
        var button=FindAll<Button>(window).Single(b=>Equals(b.Tag,label)||Equals(b.Content,label));
        await Wait(()=>button.IsEnabled);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Wait(()=>((FrameworkElement)window.FindName("PageContent")).IsEnabled);
        await Task.Delay(30);
    }
    private static async Task Wait(Func<bool> predicate,int attempts=100)
    {for(var i=0;i<attempts;i++){if(predicate())return;await Task.Delay(50);}throw new TimeoutException("UI condition not reached.");}
    private static T Find<T>(DependencyObject root,string name)where T:FrameworkElement=>FindAll<T>(root).Single(e=>e.Name==name);
    private static T ByAutomation<T>(DependencyObject root,string id)where T:DependencyObject=>FindAll<T>(root).Single(e=>AutomationProperties.GetAutomationId(e)==id);
    private static IEnumerable<T> FindAll<T>(DependencyObject root)where T:DependencyObject
    {
        var seen=new HashSet<DependencyObject>();var pending=new Stack<DependencyObject>();pending.Push(root);
        while(pending.Count>0)
        {
            var item=pending.Pop();if(!seen.Add(item))continue;if(item is T typed)yield return typed;
            foreach(var child in LogicalTreeHelper.GetChildren(item).OfType<DependencyObject>())pending.Push(child);
            if(item is Visual)for(var i=0;i<VisualTreeHelper.GetChildrenCount(item);i++)pending.Push(VisualTreeHelper.GetChild(item,i));
        }
    }
    private static void Check(bool passed,string check){if(!passed)throw new InvalidOperationException(check);checks.Add(check);}
    private static async Task SaveImage(Window window,string path)
    {await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);window.UpdateLayout();var content=(FrameworkElement)window.Content;var bitmap=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);var visual=new DrawingVisual();using(var drawing=visual.RenderOpen()){var bounds=new Rect(0,0,content.ActualWidth,content.ActualHeight);drawing.DrawRectangle(window.Background,null,bounds);drawing.DrawRectangle(new VisualBrush(content){Stretch=Stretch.Fill},null,bounds);}bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(path);encoder.Save(output);}
}
