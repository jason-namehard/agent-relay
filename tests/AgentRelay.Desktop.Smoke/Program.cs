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
        Check(navigation.Items.Cast<ListBoxItem>().Select(i=>(string)i.Tag).SequenceEqual(new[]{"Home","Agents","Connections","Settings"}),"Exactly four main pages");
        Check(Find<DataGrid>(window,"TasksTable").Items.Count==2,"Board combines all handoffs");
        SelectTask(window,task.TaskId);
        await Click(window,"Copy prompt for agent-a");
        Check(Clipboard.GetText().Contains(task.TaskId)&&Clipboard.GetText().Contains(service.Status(task.TaskId).Task.ResultPath)&&Clipboard.GetText().Contains(service.Home),"Copied completed handoff includes identity, reply path and relay location");
        await SaveImage(window,Path.Combine(root,"Board.png"));

        ((ComboBox)window.FindName("QuickLanguage")).SelectedValue="zh-CN";await Task.Delay(30);
        Navigate("Agents");
        Check(!FindAll<TextBox>(window).Any(b=>b.Name.StartsWith("AgentEntry")||b.Name.EndsWith("SetupReply")),"Agent page has no competing setup form");
        var count=service.Agents().Count;
        await Modal<AgentEntryDialog>(window,"Add Agent",async dialog=>
        {
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
        await Modal<HandoffDialog>(window,"New handoff",async dialog=>
        {
            Expand(dialog);
            Find<ComboBox>(dialog,"HandoffFrom").SelectedValue=manual.AgentId;
            Press(dialog,"HandoffCopyPrompt");
            Check(Clipboard.GetText().Contains("任意节点甲")&&Clipboard.GetText().Contains(manual.AgentId),"Handoff prompt includes user names and stable node IDs");
            Find<TextBox>(dialog,"HandoffReply").Text=Reply("handoff",new(){["From"]=manual.AgentId,["To"]="agent-c",["Content"]="请读取交接内容，不预设角色。"});
            Press(dialog,"HandoffParseReply");
            Check((string)Find<ComboBox>(dialog,"HandoffFrom").SelectedValue==manual.AgentId&&Find<TextBox>(dialog,"HandoffContent").Text.Contains("不预设角色"),"Handoff reply selects sender, recipient and message");
            Check(service.Tasks().Count==taskCount&&dialog.Result is null,"Handoff parse does not send or create a task");
            Check(!Find<RadioButton>(dialog,"HandoffAutomatic").IsEnabled&&Find<RadioButton>(dialog,"HandoffManual").IsChecked==true,"Missing CLI route defaults to manual");
            Find<TextBox>(dialog,"HandoffReply").Text=Reply("handoff",new(){["From"]=manual.AgentId,["To"]=manual.AgentId,["Content"]="invalid"});
            Press(dialog,"HandoffParseReply");
            Check((string)Find<ComboBox>(dialog,"HandoffTo").SelectedValue=="agent-c","Invalid same-node reply does not partially overwrite draft");
            await SaveImage(dialog,Path.Combine(root,"Handoff.png"));
            Press(dialog,"CreateHandoff");
        });
        Check(service.Tasks().Count==taskCount+1,"Explicit create adds one manual handoff");
        var created=service.Tasks().Single(v=>v.Task.RequesterId==manual.AgentId).Task;
        Check(created.Mode=="manual"&&File.Exists(created.RequestPath),"Manual Agent can send a stored handoff");
        await Click(window,"Copy prompt for agent-c");
        Check(Clipboard.GetText().Contains(created.RequestPath)&&Clipboard.GetText().Contains(service.Home),"Manual prompt carries request path and relay location");

        Navigate("Connections");
        Check(FindAll<System.Windows.Shapes.Path>(window).Count(p=>AutomationProperties.GetAutomationId(p).StartsWith("ConnectionStroke:"))==12,"Every direction remains visible with four nodes");
        Check(FindAll<System.Windows.Shapes.Path>(window).Where(p=>AutomationProperties.GetAutomationId(p).StartsWith("ConnectionStroke:")).All(p=>p.StrokeDashArray.Count>0),"Unconfigured routes use dashed gray lines");
        Check(!FindAll<System.Windows.Shapes.Polygon>(window).Any(),"Unavailable routes have no arrowheads");
        SeedExtension(service,"agent-a",true);SeedExtension(service,"agent-b",true);
        Navigate("Home");Navigate("Connections");
        var forward=ByAutomation<System.Windows.Shapes.Path>(window,"ConnectionEdge:agent-a:agent-b");
        forward.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});
        await Click(window,"Pause connection");
        Check(service.Connections().Single(e=>e.From=="agent-a"&&e.To=="agent-b").Display=="Denied","Green arrow can pause its own direction");
        Check(service.Connections().Single(e=>e.From=="agent-b"&&e.To=="agent-a").Display=="Denied","Changing forward route leaves reverse route independent");
        await Click(window,"Enable connection");
        Check(service.Connections().Single(e=>e.From=="agent-a"&&e.To=="agent-b").Display=="Allowed","Red arrow can enable its own direction");
        Check(ByAutomation<System.Windows.Shapes.Path>(window,"ConnectionStroke:agent-a:agent-b").StrokeDashArray.Count==0,"Available enabled route is a solid stroke");
        Check(ByAutomation<System.Windows.Shapes.Polygon>(window,"ConnectionArrow:agent-a:agent-b").Points.Count==3,"Available route has a geometric arrowhead");
        Check(FindAll<Border>(window).Where(b=>AutomationProperties.GetAutomationId(b).StartsWith("ConnectionLabel:")).All(b=>b.Child is TextBlock text&&!text.Text.Contains('→')),"Connection labels contain state text without duplicate textual arrows");
        await SaveImage(window,Path.Combine(root,"Connections.png"));

        var probe=new CliProbeDialog(window,service,"agent-c","en");
        await Show(probe,dialog=>
        {
            Check(!FindAll<Button>(dialog).Any(b=>b.Name=="ProbeCreate"),"No recipe provides explanation without a misleading run button");
            Check(FindAll<TextBlock>(dialog).Any(t=>t.Text.Contains("No CLI recipe")),"No recipe points to advanced extensions");
            Press(dialog,"CloseCliProbe");return Task.CompletedTask;
        });
        SeedExtension(service,"agent-c",false);
        await Show(new CliProbeDialog(window,service,"agent-c","en"),dialog=>
        {
            Check(!service.CliExtension("agent-c")!.CanCreate,"Passing machine receipt alone does not enable CLI");
            Check(!Find<CheckBox>(dialog,"ConfirmCreateVisible").IsChecked!.Value,"Visibility is never preconfirmed");
            Find<CheckBox>(dialog,"ConfirmCreateVisible").IsChecked=true;Press(dialog,"SaveCreateConfirmation");
            Check(service.CliExtension("agent-c")!.CanCreate&&!service.CliExtension("agent-c")!.CanResume,"Visibility confirmation enables only verified new-session ability");
            Check(FindAll<Button>(dialog).Any(b=>b.Name=="ProbeResume"),"Same-session verification is a separate next step");
            Press(dialog,"CloseCliProbe");return Task.CompletedTask;
        });
        var ext=service.CliExtension("agent-c")!;
        SaveExtension(service,ext with{ResumeProbe=new("resume-fixture","resume","RESUME-FIXTURE","fixture-session",true,false,null,DateTimeOffset.UtcNow)});
        await Show(new CliProbeDialog(window,service,"agent-c","en"),dialog=>
        {
            Check(!service.CliExtension("agent-c")!.CanResume,"Resume machine receipt still needs same-session visibility confirmation");
            Find<CheckBox>(dialog,"ConfirmResumeVisible").IsChecked=true;Press(dialog,"SaveResumeConfirmation");
            Check(service.CliExtension("agent-c")!.CanResume,"Separate same-session confirmation enables resume");
            Press(dialog,"CloseCliProbe");return Task.CompletedTask;
        });

        Navigate("Settings");
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
    private static async Task Wait(Func<bool> predicate)
    {for(var i=0;i<100;i++){if(predicate())return;await Task.Delay(50);}throw new TimeoutException("UI condition not reached.");}
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
