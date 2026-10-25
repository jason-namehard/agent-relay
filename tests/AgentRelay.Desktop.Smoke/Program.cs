using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
        Check(navigation.Items.Cast<ListBoxItem>().Select(i=>(string)i.Tag).SequenceEqual(new[]{"Home","Agents","Connections","Settings"}),"Exactly four main pages");
        Check(Find<DataGrid>(window,"TasksTable").Items.Count==2,"Board combines all handoffs");
        SelectTask(window,task.TaskId);
        await Click(window,"Copy prompt for 自选节点甲");
        Check(Clipboard.GetText().Contains(task.TaskId)&&Clipboard.GetText().Contains(service.Status(task.TaskId).Task.ResultPath)&&Clipboard.GetText().Contains(service.Home),"Copied completed handoff includes identity, reply path and relay location");
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
        await Click(window,"Remove Agent");
        Check(service.Agents().Any(a=>a.AgentId=="agent-b") && ((Border)window.FindName("ErrorBanner")).Visibility==Visibility.Visible && ((TextBox)window.FindName("ErrorText")).Text.Contains("未完成交接"),"Removing an active Agent is blocked before any confirmation dialog");
        Check(!FindAll<TextBox>(window).Any(b=>b.Name.StartsWith("AgentEntry")||b.Name.EndsWith("SetupReply")),"Agent page has no competing setup form");
        Check(FindAll<Button>(window).Any(b=>Equals(b.Tag,"Install handoff rule"))&&FindAll<Button>(window).Any(b=>Equals(b.Tag,"Remove handoff rule")),"Agent page exposes explicit installation and removal of handoff rules");
        var count=service.Agents().Count;
        await Modal<AgentEntryDialog>(window,"Add Agent",async dialog=>
        {
            Check(((TextBlock)window.FindName("StatusText")).Text!=UiText.Translate("Working…","zh-CN"),"Agent form reports a waiting state instead of processing during modal input");
            Expand(dialog);
            Find<TextBox>(dialog,"AgentEntryReply").Text=Reply("agents",new(){["AgentName"]="自由命名甲",["AgentHost"]="manual"});
            Press(dialog,"AgentEntryParseReply");
            Check(Find<TextBox>(dialog,"AgentEntryName").Text=="自由命名甲"&&service.Agents().Count==count,"Parsing Agent reply fills draft without registration");
            Find<TextBox>(dialog,"AgentEntryReply").Text="{bad";
            Press(dialog,"AgentEntryParseReply");
            Check(Find<TextBox>(dialog,"AgentEntryName").Text=="自由命名甲","Invalid Agent reply preserves draft");
            await CheckGlyphs(dialog,Find<TextBox>(dialog,"AgentEntryName"));
            await SaveImage(dialog,Path.Combine(root,"AgentEntry-zh.png"));
            Press(dialog,"CancelAgentEntry");
        });
        Check(service.Agents().Count==count,"Cancelled Agent entry writes nothing");
        await Modal<AgentEntryDialog>(window,"Add Agent",dialog=>
        {
            Expand(dialog);
            Find<TextBox>(dialog,"AgentEntryReply").Text=Reply("agents",new(){["AgentName"]="任意节点甲"});
            Press(dialog,"AgentEntryParseReply");Press(dialog,"SaveAgentEntry");return Task.CompletedTask;
        });
        var manual=service.Agents().Single(a=>a.DisplayName=="任意节点甲");
        Check(manual.ConfigPath==""&&manual.Host=="manual","Name alone registers a manual Agent without configuration paths");
        await SaveImage(window,Path.Combine(root,"Agents.png"));
        Navigate("Home");
        ((ComboBox)window.FindName("QuickLanguage")).SelectedValue="en";await Task.Delay(30);
        var taskCount=service.Tasks().Count;
        var source=Path.Combine(root,"handoff.zip");File.WriteAllBytes(source,[1,2,3,4]);
        await Modal<FileHandoffDialog>(window,"New handoff",async dialog=>
        {
            Check(((TextBlock)window.FindName("StatusText")).Text!=UiText.Translate("Working…","en"),"Handoff form reports a waiting state instead of processing during modal input");
            Expand(dialog);
            Press(dialog,"CopyFileHandoffFormat");
            Check(Clipboard.GetText().Contains("file")&&Clipboard.GetText().Contains("任意节点甲"),"File handoff instructions explain the compact JSON and registered node names");
            Find<TextBox>(dialog,"FileHandoffReply").Text="{bad";Press(dialog,"ParseFileHandoff");
            Check(dialog.Result is null&&service.Tasks().Count==taskCount,"Malformed handoff JSON cannot create a task");
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
        Check(Clipboard.GetText().Length<500&&Clipboard.GetText().Contains(created.TaskId)&&Clipboard.GetText().Contains("context.json"),"Receiver prompt is a short pointer to task context instead of a long protocol transcript");
        var context=RelayJson.Read<System.Text.Json.JsonElement>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(service.ManualReceiptPath(created))!,"context.json")));
        Check(context.GetProperty("current_file").GetString()==created.RequestPath && context.GetProperty("receipt_template").GetProperty("sender").GetString()=="agent-c","Task context agrees with the installed rule and exact receipt identity");
        var replyFile=Path.Combine(root,"b-reply.md");File.WriteAllText(replyFile,"Select option 2 before finalizing.");
        File.WriteAllText(service.ManualReceiptPath(created),RelayJson.Write(new ManualReceipt(created.TaskId,created.AttemptId,"agent-c",0,"done",true,replyFile,"选择第二个选项并确认最终方案")));
        await Wait(()=>service.Status(created.TaskId).Task.State==TaskState.AwaitingReply);
        Check(service.Status(created.TaskId).Task.AwaitingAgentId==manual.AgentId,"Agent receipt automatically keeps the task ongoing and selects the requester for necessary followup");
        await Wait(()=>FindAll<Button>(window).Any(b=>Equals(b.Tag,"Copy prompt for 任意节点甲")));
        await Click(window,"Copy prompt for 任意节点甲");
        Check(Clipboard.GetText().Length<500,"Followup prompt remains short and changes its target Agent");
        Check(Directory.GetFiles(Path.GetDirectoryName(created.RequestPath)!).Length==2,"Reply adds one file without creating duplicate result snapshots");
        File.WriteAllText(service.ManualReceiptPath(created),RelayJson.Write(new ManualReceipt(created.TaskId,created.AttemptId,manual.AgentId,1,"done",false)));
        await Wait(()=>service.Status(created.TaskId).Task.State==TaskState.Succeeded);
        Check(service.Status(created.TaskId).Task.State==TaskState.Succeeded,"Requester final receipt automatically completes the existing task without another paste or import");
        await SaveImage(window,Path.Combine(root,"Manual-completed.png"));

        var directSource=Path.Combine(root,"direct.md");File.WriteAllText(directSource,"No followup is needed after review.");
        var beforeDirect=service.Tasks().Count;
        await Modal<FileHandoffDialog>(window,"New handoff",dialog=>
        {
            Find<TextBox>(dialog,"FileHandoffReply").Text=RelayJson.Write(new{from=manual.AgentId,to="agent-c",file=directSource});
            Press(dialog,"ParseFileHandoff");return Task.CompletedTask;
        });
        await Wait(()=>service.Tasks().Count==beforeDirect+1);
        var direct=service.Tasks().Single(v=>v.Task.ManualSourceFile==directSource).Task;
        File.WriteAllText(service.ManualReceiptPath(direct),RelayJson.Write(new ManualReceipt(direct.TaskId,direct.AttemptId,"agent-c",0,"done",false)));
        await Wait(()=>service.Status(direct.TaskId).Task.State==TaskState.Succeeded);
        Check(service.Status(direct.TaskId).Task.ManualTurn==0,"Receipt that needs no reply ends the handoff immediately without returning to its sender");

        Navigate("Connections");
        Check(FindAll<System.Windows.Shapes.Path>(window).Count(p=>AutomationProperties.GetAutomationId(p).StartsWith("ConnectionPair:"))==6,"Four nodes use six shared connection lines instead of twelve directed lines");
        Check(new[]{"ConnectionLegendSingle","ConnectionLegendDouble","ConnectionLegendNone"}.All(id=>FindAll<FrameworkElement>(window).Any(e=>AutomationProperties.GetAutomationId(e)==id)),"Connection legend contains visible single-arrow, double-arrow and unavailable-line examples");
        Check(FindAll<System.Windows.Shapes.Path>(window).Where(p=>AutomationProperties.GetAutomationId(p).StartsWith("ConnectionStroke:")).All(p=>p.StrokeDashArray.Count>0),"Unconfigured routes use dashed gray lines");
        Check(!FindAll<System.Windows.Shapes.Polygon>(window).Any(p=>AutomationProperties.GetAutomationId(p).StartsWith("ConnectionArrow:")),"Unavailable routes have no arrowheads");
        SeedExtension(service,"agent-a",true);SeedExtension(service,"agent-b",true);
        Navigate("Home");Navigate("Connections");
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

        var probe=new CliProbeDialog(window,service,"agent-c","en");
        await Show(probe,dialog=>
        {
            Check(!FindAll<Button>(dialog).Any(b=>b.Name=="ProbeCreate"),"No recipe provides explanation without a misleading run button");
            Check(FindAll<TextBlock>(dialog).Any(t=>t.Text.Contains("No CLI recipe")),"No recipe points to advanced extensions");
            Press(dialog,"CloseCliProbe");return Task.CompletedTask;
        });
        SeedExtension(service,"agent-c",false);
        await Show(new CliProbeDialog(window,service,"agent-c","en"),async dialog=>
        {
            Check(!service.CliExtension("agent-c")!.CanCreate,"Passing machine receipt alone does not enable CLI");
            Check(!Find<CheckBox>(dialog,"ConfirmCreateVisible").IsChecked!.Value,"Visibility is never preconfirmed");
            Find<CheckBox>(dialog,"ConfirmCreateVisible").IsChecked=true;Press(dialog,"SaveCreateConfirmation");
            await Wait(()=>service.CliExtension("agent-c")!.CanCreate);
            Check(service.CliExtension("agent-c")!.CanCreate&&!service.CliExtension("agent-c")!.CanResume,"Visibility confirmation enables only verified new-session ability");
            Check(FindAll<Button>(dialog).Any(b=>b.Name=="ProbeResume"),"Same-session verification is a separate next step");
            Press(dialog,"CloseCliProbe");
        });
        var ext=service.CliExtension("agent-c")!;
        SaveExtension(service,ext with{ResumeProbe=new("resume-fixture","resume","RESUME-FIXTURE","fixture-session",true,false,null,DateTimeOffset.UtcNow)});
        await Show(new CliProbeDialog(window,service,"agent-c","en"),async dialog=>
        {
            Check(!service.CliExtension("agent-c")!.CanResume,"Resume machine receipt still needs same-session visibility confirmation");
            Find<CheckBox>(dialog,"ConfirmResumeVisible").IsChecked=true;Press(dialog,"SaveResumeConfirmation");
            await Wait(()=>service.CliExtension("agent-c")!.CanResume);
            Check(service.CliExtension("agent-c")!.CanResume,"Separate same-session confirmation enables resume");
            Press(dialog,"CloseCliProbe");
        });

        Navigate("Settings");
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
        await Click(empty,"New handoff");
        Check(((Border)empty.FindName("ErrorBanner")).Visibility==Visibility.Visible&&emptyService.Tasks().Count==0,"New handoff explains missing Agents without creating work");
        empty.Close();
    }

    private static string Reply(string context,Dictionary<string,string> fields)=>RelayJson.Write(new{relay_setup_version=1,context,fields});
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
            @{session_id='progress-session';reply=$reply;accepted=$true}|ConvertTo-Json -Compress
            """);
        var command=new CliCommand("pwsh",["-NoLogo","-NoProfile","-NonInteractive","-File",script,started,release,fail],"{prompt}",null);
        service.ImportCliRecipe(id,RelayJson.Write(new CliRecipe(1,"Progress fixture","fixture",command,null,[],"fixture","local test","MIT","No model calls.")));
        // Refresh the Agent choice after adding the isolated progress-test node.
        await Click(window,"Refresh");
        Find<ComboBox>(window,"ExtensionAgent").SelectedValue=id;
        await Modal<CliProbeDialog>(window,"Test CLI",async dialog=>
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
        SaveExtension(service,new(id,Guid.NewGuid().ToString(),recipe,new("create-fixture-"+id,"create","CREATE-FIXTURE","fixture-session",true,confirmed,null,DateTimeOffset.UtcNow),null));
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
    {FindAll<Button>(window).Single(b=>Equals(b.Tag,label)||Equals(b.Content,label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Wait(()=>((TextBlock)window.FindName("StatusText")).Text!="Working…");await Task.Delay(30);}
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
