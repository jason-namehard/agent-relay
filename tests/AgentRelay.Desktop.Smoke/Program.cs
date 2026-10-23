using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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
        foreach(var id in new[]{"agent-a","agent-b","agent-c"})service.Register(id,"fixture",config);
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
        Check(FindAll<TextBlock>(window).Any(t=>t.Text=="Needs Attention"),"Home summary rendered");
        await SaveImage(window,Path.Combine(root,"Home.png"));
        Navigate("Connections");
        var connections=Find<DataGrid>(window,"ConnectionsTable");
        Check(connections.Items.Count==6,"All directed pairs represented");
        Check(service.Connections().Select(e=>e.Display).Distinct().Count()==4,"Allowed, denied, default deny and unavailable remain distinct");
        await SaveImage(window,Path.Combine(root,"Connections.png"));
        Navigate("Agents");var agents=Find<DataGrid>(window,"AgentsTable");agents.SelectedIndex=0;
        Check(Find<TextBox>(window,"AgentConfig").Text==config,"Agent selection retains exact path");
        Find<TextBox>(window,"AgentConfig").Text=Path.Combine(root,"missing.toml");
        await ClickWithApproval(window,"Register / Correct Path");
        await Click(window,"Validate");
        Check(((Border)window.FindName("ErrorBanner")).Visibility==Visibility.Visible,"Invalid path is visible");
        Find<TextBox>(window,"AgentConfig").Text=config;
        await ClickWithApproval(window,"Register / Correct Path");await Click(window,"Validate");
        Check(service.Agents().Single(a=>a.AgentId=="agent-a").ValidationStatus=="path_verified","Corrected path requires explicit revalidation");
        Navigate("Inbox");SelectTask(window,task.TaskId);await Click(window,"Copy Prompt");
        Check(Clipboard.GetText().Contains(task.TaskId) && Clipboard.GetText().Contains(service.Status(task.TaskId).Task.ResultPath),"Prompt copied from current task");
        var notifications=Find<DataGrid>(window,"NotificationsTable");notifications.SelectedIndex=0;await Click(window,"Mark Read");
        Check(service.Inbox().First().IsRead,"Read acknowledgement persisted");
        SelectTask(window,waiting.TaskId);await ClickWithApproval(window,"Cancel");
        Check(service.Status(waiting.TaskId).Task.State==TaskState.Cancelled,"Cancel only changes Relay state");
        await SaveImage(window,Path.Combine(root,"Inbox.png"));
        Navigate("Adapters");var tabs=FindAll<TabControl>(window).Single();tabs.SelectedIndex=1;window.UpdateLayout();
        var target=Path.Combine(root,"host.json");File.WriteAllText(target,"{\"mcpServers\":{}}\n");
        var manifest=Path.Combine(root,"manifest.json");
        var request=new MigrationRequest(new("fixture-adapter","1","https://github.com/jason-namehard/agent-relay","fixture","MIT",["stdio_mcp"],[],[]),target,"json","{}","{\"fixture\":{\"command\":\"fixture\"}}");
        File.WriteAllText(manifest,RelayJson.Write(request));
        Find<TextBox>(window,"MigrationPath").Text=manifest;await Click(window,"Plan Manifest");
        Check(File.ReadAllText(target)=="{\"mcpServers\":{}}\n","Plan leaves host config untouched");
        var migration=service.Migrations().Single();
        await ClickWithApproval(window,"Apply",approve:false);
        Check(service.Migration(migration.MigrationId).State=="planned","Cancelled approval cannot apply");
        await ClickWithApproval(window,"Apply");Check(service.Migration(migration.MigrationId).State=="applied","Reviewed apply persists and backs up");
        await SaveImage(window,Path.Combine(root,"Migrations.png"));
        await ClickWithApproval(window,"Rollback");Check(File.ReadAllText(target)=="{\"mcpServers\":{}}\n","Reviewed rollback restores original bytes");
        Navigate("Settings");Find<TextBox>(window,"TtlDays").Text="5";await Click(window,"Save Settings");
        Check(service.Settings().ArtifactTtlDays==5,"Settings persist");
        Find<TextBox>(window,"TtlDays").Text="0";await Click(window,"Save Settings");
        Check(service.Settings().ArtifactTtlDays==5 && ((Border)window.FindName("ErrorBanner")).Visibility==Visibility.Visible,"Invalid settings do not overwrite valid data");
        Navigate("Audit");Check(Find<DataGrid>(window,"AuditTable").Items.Count>0,"Audit rendered");
        Navigate("Home");
        var before=((TextBlock)window.FindName("StatusText")).Text;
        var dotnet=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? throw new InvalidOperationException("DOTNET_HOST_PATH required for CLI signal probe.");
        var start=new ProcessStartInfo(dotnet){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var argument in new[]{cli,"--home",service.Home,"permission","set","--from","agent-c","--to","agent-a","--decision","deny"})start.ArgumentList.Add(argument);
        using var process=Process.Start(start)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        if(process.ExitCode!=0)throw new InvalidOperationException(await stderr);
        Check(process.ExitCode==0,"External CLI updated ledger");await stdout;
        await Wait(()=>((TextBlock)window.FindName("StatusText")).Text==UiText.Translate("已读取事件更新","en"));
        Check(before!=UiText.Translate("已读取事件更新","en"),"Cross-process window message refreshed Home without polling");
        Navigate("Connections");await SaveImage(window,Path.Combine(root,"Connections.png"));
        foreach(var page in new[]{"Home","Agents","Connections","Inbox","Adapters","Audit","Settings"}){Navigate(page);window.UpdateLayout();Check(((ContentControl)window.FindName("PageContent")).Content is not null,$"{page} accessible");}
        var demoService=await DemoWorkspace.CreateAsync(Path.Combine(root,"demo"));
        Appearance.Save(demoService.Home,new DesktopPreferences("en"));
        var demoWindow=new MainWindow(demoService,demo:true);demoWindow.Show();
        await Wait(()=>((TextBlock)demoWindow.FindName("StatusText")).Text!="Working…");
        demoWindow.UpdateLayout();
        Check(demoWindow.Title.Contains("Demo") && ((TextBlock)demoWindow.FindName("ModeDescription")).Text.Contains("DEMO"),"Demo remains visibly labelled");
        Check(((TextBlock)demoWindow.FindName("VersionLabel")).Text=="v"+RelayVersion.Current,"GUI shows source preview version");
        Check(Find<DataGrid>(demoWindow,"TasksTable").Items.Count==2,"Demo Home shows ready and failed sample tasks");
        await SaveImage(demoWindow,Path.Combine(root,"Demo.png"));demoWindow.Close();
        var emptyService=new RelayService(Path.Combine(root,"empty"));emptyService.Initialize();
        Appearance.Save(emptyService.Home,new DesktopPreferences("en"));
        var emptyWindow=new MainWindow(emptyService);emptyWindow.Show();
        await Wait(()=>((TextBlock)emptyWindow.FindName("StatusText")).Text!="Working…");emptyWindow.UpdateLayout();
        Check(FindAll<Button>(emptyWindow).Any(b=>Equals(b.Content,"Open Demo")),"Empty registry offers isolated demo");
        await Click(emptyWindow,"Register Agent");emptyWindow.UpdateLayout();
        Check(((TextBlock)emptyWindow.FindName("PageTitle")).Text=="Agents","First-use registration action navigates to Agents");
        Check(emptyService.Agents().Count==0,"First-use navigation does not auto-register agents");emptyWindow.Close();
        await ExerciseUsability(window,service,root,config);
    }

    private static async Task ExerciseUsability(MainWindow window,RelayService service,string root,string config)
    {
        void Navigate(string page){var nav=(ListBox)window.FindName("Navigation");nav.SelectedItem=nav.Items.Cast<ListBoxItem>().Single(i=>(string)i.Tag==page);window.UpdateLayout();}
        Navigate("Agents");Find<DataGrid>(window,"AgentsTable").SelectedIndex=0;
        Find<Button>(window,"AgentConfigCopySetupPrompt").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(Clipboard.GetText().Contains("\"AgentConfig\"") && Clipboard.GetText().Contains("not a login name"),"Each field copies a constrained setup prompt");
        var assistant=FindAll<FormAssistant>(window).Single();assistant.IsExpanded=true;window.UpdateLayout();
        await Click(window,"Copy Setup Prompt");
        Check(Clipboard.GetText().Contains("\"context\": \"agents\"") && Clipboard.GetText().Contains("\"AgentInstructions\""),"Group prompt carries stable field names");
        var count=service.Agents().Count;
        Find<TextBox>(window,"AgentCli").Text="keep-existing";
        var reply=RelayJson.Write(new{relay_setup_version=1,context="agents",fields=new Dictionary<string,string>{["AgentId"]="agent-new",["AgentHost"]="fixture",["AgentConfig"]=config,["AgentCli"]=""}});
        Clipboard.SetText(reply);await Click(window,"Paste Agent Reply");await Click(window,"Parse and Fill");
        Check(Find<TextBox>(window,"AgentId").Text=="agent-new" && Find<TextBox>(window,"AgentConfig").Text==config,"Agent reply fills the active form");
        Check(Find<TextBox>(window,"AgentCli").Text=="keep-existing" && service.Agents().Count==count,"Blank fields preserve input and parse does not register");
        Find<TextBox>(window,"agentsSetupReply").Text="{\"relay_setup_version\":1,\"context\":\"agents\",\"fields\":{\"AgentId\":\"wrong\",\"AgentHost\":123}}";
        await Click(window,"Parse and Fill");
        Check(Find<TextBox>(window,"AgentId").Text=="agent-new" && ((Border)window.FindName("ErrorBanner")).Visibility==Visibility.Visible,"Invalid reply cannot partially fill the form");
        Find<TextBox>(window,"AgentCli").Clear();await Click(window,"Register / Correct Path");await Click(window,"Validate");
        Check(service.Agents().Count==count+1 && service.Agents().Single(a=>a.AgentId=="agent-new").ValidationStatus=="path_verified","Parsed registration still requires explicit save and validation");

        Find<TextBox>(window,"AgentVersion").Text="unsaved-version";
        ((ComboBox)window.FindName("QuickLanguage")).SelectedValue="zh-CN";window.UpdateLayout();
        Check(Find<TextBox>(window,"AgentVersion").Text=="unsaved-version","Language switching preserves unsaved form input");

        foreach(var option in Appearance.Languages)
        {
            ((ComboBox)window.FindName("QuickLanguage")).SelectedValue=option.Id;
            await Wait(()=>Appearance.Load(service.Home).Language==option.Id);
            Navigate("Settings");
            Check(((TextBlock)window.FindName("PageTitle")).Text==UiText.Translate("Settings",option.Id),$"Navigation localizes in {option.Id}");
            Check(FindAll<Button>(window).Any(b=>Equals(b.Content,UiText.Translate("Apply Appearance",option.Id))),$"Settings actions localize in {option.Id}");
            Check(Find<TextBox>(window,"TtlDays").Text=="5",$"Language switch preserves stored settings in {option.Id}");
        }
        ((ComboBox)window.FindName("QuickLanguage")).SelectedValue="zh-CN";Navigate("Settings");
        var accents=new HashSet<string>();
        foreach(var palette in Appearance.Palettes)
        {
            Find<ComboBox>(window,"PaletteChoice").SelectedValue=palette.Id;await Click(window,"Apply Appearance");
            accents.Add(((SolidColorBrush)window.FindResource("AccentBrush")).Color.ToString());
            Check(Appearance.Load(service.Home).Palette==palette.Id,$"Palette {palette.Id} persists");
        }
        Check(accents.Count==4,"Morandi palettes visibly differ");
        Find<TextBox>(window,"TtlDays").Text="7";
        Find<TextBox>(window,"CustomAccent").Text="#637A68";await Click(window,"Apply Appearance");
        Check(Appearance.Load(service.Home).Accent=="#637A68","Custom accent persists");
        Check(Find<TextBox>(window,"TtlDays").Text=="7" && service.Settings().ArtifactTtlDays==5,"Appearance changes preserve drafts without saving business settings");
        var approvedColor=((SolidColorBrush)window.FindResource("AccentBrush")).Color;
        Find<TextBox>(window,"CustomAccent").Text="#NOTHEX";await Click(window,"Apply Appearance");
        Check(Appearance.Load(service.Home).Accent=="#637A68" && ((SolidColorBrush)window.FindResource("AccentBrush")).Color==approvedColor,"Invalid color does not replace valid appearance");
        Find<TextBox>(window,"CustomAccent").Clear();Find<ComboBox>(window,"PaletteChoice").SelectedValue="sage";await Click(window,"Apply Appearance");
        await SaveImage(window,Path.Combine(root,"Settings-zh-CN.png"));
        Navigate("Agents");FindAll<FormAssistant>(window).Single().IsExpanded=true;window.UpdateLayout();
        await SaveImage(window,Path.Combine(root,"Assistant-zh-CN.png"));
        Navigate("Connections");await SaveImage(window,Path.Combine(root,"Connections-zh-CN.png"));
        var reopened=new MainWindow(new RelayService(service.Home));reopened.Show();await Task.Delay(100);reopened.UpdateLayout();
        Check(((ComboBox)reopened.FindName("QuickLanguage")).SelectedValue!.Equals("zh-CN") && ((TextBlock)reopened.FindName("PageTitle")).Text=="首页","Language preference survives reopening");
        reopened.Close();
        var bad=Path.Combine(root,"bad-preferences");Directory.CreateDirectory(bad);File.WriteAllText(Path.Combine(bad,"desktop.preferences.json"),"{broken");
        try{Appearance.Load(bad);throw new InvalidOperationException("Invalid preferences were accepted");}
        catch(RelayException error){Check(error.Code=="invalid_desktop_preferences","Malformed preference file reports an explicit error");}
    }

    private static void SelectTask(MainWindow window,string id)
    {var table=Find<DataGrid>(window,"TasksTable");table.SelectedItem=table.Items.Cast<object>().Single(r=>(string)r.GetType().GetProperty("TaskId")!.GetValue(r)! == id);}
    private static async Task Click(MainWindow window,string content)
    {FindAll<Button>(window).Single(b=>Equals(b.Tag,content) || Equals(b.Content,content)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Wait(()=>((TextBlock)window.FindName("StatusText")).Text!="Working…");await Task.Delay(50);}
    private static async Task ClickWithApproval(MainWindow window,string content,bool approve=true)
    {
        _ = window.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>
        {
            var dialog=Application.Current.Windows.OfType<ReviewDialog>().Single();
            Check(!Find<Button>(dialog,"ApproveButton").IsEnabled,"Approval starts disabled");
            if(!approve){dialog.Close();return;}
            Find<TextBox>(dialog,"ApprovalReason").Text="Reviewed smoke fixture";
            Find<CheckBox>(dialog,"ApprovalCheck").IsChecked=true;
            Find<Button>(dialog,"ApproveButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }));
        await Click(window,content);
    }
    private static async Task Wait(Func<bool> predicate)
    {for(var i=0;i<100;i++){if(predicate())return;await Task.Delay(50);}throw new TimeoutException("UI condition not reached.");}
    private static T Find<T>(DependencyObject root,string name) where T:FrameworkElement=>FindAll<T>(root).Single(e=>e.Name==name);
    private static IEnumerable<T> FindAll<T>(DependencyObject root) where T:DependencyObject
    {if(root is T typed)yield return typed;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in FindAll<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private static void Check(bool passed,string check)
    {if(!passed)throw new InvalidOperationException(check);checks.Add(check);}
    private static async Task SaveImage(Window window,string path)
    {await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);window.UpdateLayout();var content=(FrameworkElement)window.Content;var bitmap=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(path);encoder.Save(output);}
}
