using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using AgentRelay.Core;
using Microsoft.Win32;

namespace AgentRelay.Desktop;

public partial class MainWindow : Window
{
    private readonly RelayService service;
    private DesktopPreferences preferences;
    private bool languageReady;
    private readonly Dictionary<string, TextBox> fields = new(StringComparer.Ordinal);
    private bool busy;
    private bool reviewCancelled;
    private bool refreshPending;
    private uint ledgerMessage;
    private HwndSource? source;
    private string? taskId;
    private string? migrationId;
    private string? agentId;
    private string? connectionFrom;
    private string? connectionTo;
    private int adapterTab;
    private const string LedgerHelp = "Refresh 只读取账本。Recover 会处理待投递/继续操作，可能调用已验证适配器。";
    private static readonly JsonSerializerOptions ManifestJson = new(RelayJson.Options)
    {
        RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public MainWindow(RelayService service, bool demo = false)
    {
        this.service = service;
        preferences = Appearance.Load(service.Home);
        InitializeComponent();
        Appearance.Apply(this, preferences);
        QuickLanguage.ItemsSource = Appearance.Languages;
        QuickLanguage.DisplayMemberPath = "Label"; QuickLanguage.SelectedValuePath = "Id";
        QuickLanguage.SelectedValue = preferences.Language;
        languageReady = true;
        UpdateChrome();
        HomePath.Text = service.Home;
        VersionLabel.Text = "v" + RelayVersion.Current;
        if (demo)
        {
            Title = "Agent Relay — Demo";
            ModeDescription.Tag = "DEMO · 虚构 Agent 与预设结果 · 未调用模型，未读取现用配置";
            ModeDescription.Text = T((string)ModeDescription.Tag);
        }
        SourceInitialized += (_, _) =>
        {
            ledgerMessage = WindowsLedgerSignal.Register();
            if (ledgerMessage == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(ReceiveLedgerMessage);
        };
        Closed += (_, _) => source?.RemoveHook(ReceiveLedgerMessage);
        Loaded += async (_, _) =>
        {
            Navigation.SelectedIndex = 0;
            await Run(async () => { await service.RecoverAsync(); }, "启动恢复完成");
        };
    }

    private nint ReceiveLedgerMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        // Local actions already render their committed changes and keep their own success message.
        if (message == ledgerMessage && wParam != Environment.ProcessId) QueueRefresh();
        return 0;
    }

    private void QueueRefresh()
    {
        if (refreshPending) return;
        refreshPending = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(async () =>
        {
            if (busy) return; // Run drains a queued hint after its operation completes.
            refreshPending = false;
            if (Navigation.SelectedItem is ListBoxItem item && (string)item.Tag is not ("Home" or "Inbox"))
            {
                LedgerHint.Text = T("Ledger changed · Refresh");
                return; // Preserve edits and migration previews until the user explicitly refreshes.
            }
            await Run(() => Task.CompletedTask, "已读取事件更新", clearError: false, reportStatus: false);
            LedgerHint.Text = T("已读取事件更新");
        }));
    }

    private async Task Run(Func<Task> action, string success, bool refresh = true, bool clearError = true, bool reportStatus = true)
    {
        if (busy) return;
        reviewCancelled = false;
        busy = true; PageContent.IsEnabled = false; Navigation.IsEnabled = false; RefreshButton.IsEnabled = false; QuickLanguage.IsEnabled = false;
        if (clearError) ErrorBanner.Visibility = Visibility.Collapsed;
        if (reportStatus) StatusText.Text = T("Working…");
        try
        {
            await action();
            if (refresh && !reviewCancelled) { RenderPage(); LedgerHint.Text = ""; }
            if (reportStatus) StatusText.Text = ErrorBanner.Visibility == Visibility.Visible ? T("Action failed") :
                reviewCancelled ? T("Action cancelled · No changes") : T(success);
        }
        // Expected external input, filesystem, host-command and clipboard failures stay visible at the UI boundary.
        catch (Exception error) when (error is RelayException or IOException or UnauthorizedAccessException or
            JsonException or Microsoft.Data.Sqlite.SqliteException or ArgumentException or Win32Exception or ExternalException)
        {
            ErrorText.Text = error is RelayException relay ? DesktopLocalizedContent.SetupError(relay, preferences.Language) : error.Message;
            if (error is RelayException setup && setup.Code.StartsWith("setup_", StringComparison.Ordinal))
                ErrorText.Text = T("The Agent reply format is invalid. Check version, context and requested fields.") + "\n" + ErrorText.Text;
            ErrorBanner.Visibility = Visibility.Visible; StatusText.Text = T("Action failed");
        }
        finally
        {
            busy = false; PageContent.IsEnabled = true; Navigation.IsEnabled = true; RefreshButton.IsEnabled = true; QuickLanguage.IsEnabled = true;
            if (refreshPending) { refreshPending = false; QueueRefresh(); }
        }
    }

    private async void Navigate(object sender, SelectionChangedEventArgs e) => await Run(() => Task.CompletedTask, "Ready");
    private async void RefreshClick(object sender, RoutedEventArgs e) => await Run(() => Task.CompletedTask, "已刷新");
    private async void QuickLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!languageReady || QuickLanguage.SelectedValue is not string language || language == preferences.Language) return;
        await ChangeLanguage(language);
    }

    private async Task ChangeLanguage(string language)
        => await ChangeAppearance(preferences with { Language = language });

    private async Task ChangeAppearance(DesktopPreferences next)
    {
        var drafts=CaptureInputs();
        await Run(() =>
        {
            Appearance.Save(service.Home, next); preferences = next; Appearance.Apply(this,next); UpdateChrome();
            return Task.CompletedTask;
        }, "Language and appearance saved");
        RestoreInputs(drafts);
        languageReady=false;QuickLanguage.SelectedValue=preferences.Language;languageReady=true;
    }

    private void RenderPage()
    {
        if (Navigation.SelectedItem is not ListBoxItem item) return;
        var page = (string)item.Tag;
        fields.Clear();
        PageTitle.Text = T(page);
        PageContent.Content = page switch
        {
            "Home" => HomePage(), "Agents" => AgentsPage(), "Connections" => ConnectionsPage(),
            "Inbox" => InboxPage(), "Adapters" => AdaptersPage(), "Audit" => AuditPage(), "Settings" => SettingsPage(),
            _ => throw new InvalidOperationException(page)
        };
        Localize(PageContent.Content as DependencyObject);
    }

    private StackPanel HomePage()
    {
        var page = Page(); var summary = service.Summary();
        if (summary.RegisteredAgents == 0)
        {
            Heading(page, "Start your first handoff", "先用隔离演示查看连接与结果；正式接入从 Agents 登记和验证真实路径开始。" );
            var actions = Row(page);
            Add(actions, "Open Demo", async () =>
            {
                var demoService = await DemoWorkspace.CreateAsync(Program.NewDemoHome());
                Appearance.Save(demoService.Home, preferences);
                new MainWindow(demoService, demo: true).Show();
            }, false);
            Add(actions, "Register Agent", () => { Navigation.SelectedIndex = 1; return Task.CompletedTask; });
        }
        var cards = new UniformGrid { Columns = 4, Margin = new(0,0,0,18) };
        foreach (var (label,value) in new[] { ("Registered Agents", summary.RegisteredAgents.ToString()),
            ("Pending Tasks", summary.PendingTasks.ToString()), ("Needs Attention", summary.NeedsAttention.ToString()),
            ("Retention", $"{summary.RetentionDays} {T("days")}") })
        {
            var stack = new StackPanel(); stack.Children.Add(new TextBlock { Text = label, Foreground = Brushes.SlateGray });
            stack.Children.Add(new TextBlock { Text = value, FontSize = 28, FontWeight = FontWeights.SemiBold, Margin = new(0,8,0,0) });
            cards.Children.Add(new Border { Child = stack, Background = Theme("SurfaceBrush"), BorderBrush = Theme("BorderBrush"), BorderThickness = new(1), CornerRadius = new(14), Padding = new(14), Margin = new(0,0,10,0) });
        }
        page.Children.Add(cards); Heading(page, "Needs Attention", "失败、超时、投递未知，以及需要人工继续的结果。" );
        TaskPane(page, service.Tasks().Where(RelayService.NeedsAttention)); return page;
    }

    private StackPanel AgentsPage()
    {
        var page = Page(); Heading(page, "Agent Registry", "登记和路径校验只读取宿主文件；纠正路径后需重新 Validate。" );
        var agents = service.Agents(); var table = Table("AgentsTable", ("Agent ID","AgentId"),("Host","Host"),("Validation","ValidationStatus"),("Config path","ConfigPath"));
        table.ItemsSource = agents; page.Children.Add(table);
        var form = new WrapPanel(); page.Children.Add(form);
        var id = Field(form,"Agent ID","AgentId",180); var host = Field(form,"Host","AgentHost",140);
        var config = Field(form,"Config path","AgentConfig",440); var cli = Field(form,"CLI path (optional)","AgentCli",440);
        var version = Field(form,"Host version (optional)","AgentVersion",180);
        var instructions = Field(form,"Instruction paths (one per line)","AgentInstructions",440); instructions.AcceptsReturn = true; instructions.Height=60;
        var detail = Output("AgentDetail", 120); var buttons = Row(page);
        Add(buttons,"Discover",() => { detail.Text = RelayJson.Write(service.Discover()); return Task.CompletedTask; },false);
        Add(buttons,"Browse config",() => { var dialog = new OpenFileDialog(); if (dialog.ShowDialog(this) == true) config.Text=dialog.FileName; return Task.CompletedTask; },false);
        Add(buttons,"Register / Correct Path",() =>
        {
            var prior = service.Agents().SingleOrDefault(a => a.AgentId == id.Text.Trim());
            if (prior is not null && Review("Correct registry entry", $"仅修改 Relay 登记，不写宿主文件。\n当前：\n{RelayJson.Write(prior)}\n新配置：{config.Text}\n新 CLI：{cli.Text}\n新指令：{instructions.Text}") is null) return Task.CompletedTask;
            service.Register(id.Text.Trim(),host.Text.Trim(),Required(config,"Config path"),Optional(cli),Optional(version),
                instructions.Text.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            agentId=id.Text.Trim(); return Task.CompletedTask;
        });
        Add(buttons,"Validate",() => { var result=service.ValidateAgent(Required(id,"Agent ID")); agentId=result.AgentId;
            if (result.ValidationStatus != "path_verified") throw new RelayException("agent_invalid","登记路径无效；请检查配置、CLI 与指令文件。"); return Task.CompletedTask; });
        page.Children.Add(detail);
        table.SelectionChanged += (_,_) => { if (table.SelectedItem is AgentRecord a) { agentId=a.AgentId; id.Text=a.AgentId; host.Text=a.Host;
            config.Text=a.ConfigPath; cli.Text=a.CliPath ?? ""; version.Text=a.HostVersion ?? ""; instructions.Text=string.Join(Environment.NewLine,a.InstructionPaths); detail.Text=RelayJson.Write(a); } };
        table.SelectedItem=agents.SingleOrDefault(a=>a.AgentId==agentId);
        page.Children.Insert(2,new TextBlock{Text=T("Agent ID is a local Relay label, not an account or login."),TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,10)});
        AddAssistant(page,"agents"); return page;
    }

    private StackPanel ConnectionsPage()
    {
        var page = Page(); Heading(page,"Directed Connections","Green: Allowed · Red: Explicit deny · Gray: Unavailable · Dashed: Default deny" );
        var edges=service.Connections(); var table=Table("ConnectionsTable",("From","From"),("To","To"),("Permission","Permission"),("Capability","Capability"),("Status","Display"));
        var graph=new ConnectionGraph(service.Agents(),edges,e=>table.SelectedItem=e, preferences.Language, this); page.Children.Add(graph);
        table.ItemsSource=edges; page.Children.Add(table);
        var form=new WrapPanel(); page.Children.Add(form); var from=Field(form,"From","ConnectionFrom",220); var to=Field(form,"To","ConnectionTo",220);
        var detail=Output("ConnectionDetail",95); var actions=Row(page);
        foreach(var decision in new[]{"allow","deny"}) Add(actions, decision=="allow" ? "Allow A → B" : "Deny A → B",() =>
        {
            var sender=Required(from,"From"); var recipient=Required(to,"To");
            var edge=service.Connections().SingleOrDefault(e=>e.From==sender && e.To==recipient);
            if(Review("Change directed permission",$"{sender} → {recipient}\nDecision: {decision}\nCapability: {edge?.Capability ?? "Unknown"}\n{edge?.Reason}\n仅影响该方向的 Relay 路由，不改变宿主原生权限。") is not null)
                service.SetPermission(sender,recipient,decision);
            connectionFrom=sender; connectionTo=recipient; return Task.CompletedTask;
        });
        page.Children.Add(detail);
        table.SelectionChanged+=(_,_)=>{if(table.SelectedItem is ConnectionView e) {from.Text=e.From;to.Text=e.To;connectionFrom=e.From;connectionTo=e.To;detail.Text=RelayJson.Write(e);}};
        table.SelectedItem=edges.SingleOrDefault(e=>e.From==connectionFrom && e.To==connectionTo); AddAssistant(page,"connections"); return page;
    }

    private sealed record TaskRow(string TaskId,string Route,string State,string NextAction,DateTimeOffset Deadline);
    private sealed record NotificationRow(InboxItem Source,string Event,string Message,string IsRead,DateTimeOffset CreatedAt)
    {
        public string Detail => RelayJson.Write(Source);
    }
    private StackPanel InboxPage()
    {
        var page=Page(); Heading(page,"Tasks & Inbox",LedgerHelp); TaskPane(page,service.Tasks());
        Heading(page,"Notifications","已读只改变 Inbox，不改变任务状态。" );
        var table=Table("NotificationsTable",("Event","Event"),("Message","Message"),("Created","CreatedAt"),("Read","IsRead"));
        table.ItemsSource=service.Inbox().Select(n=>new NotificationRow(n,DesktopLocalizedContent.NotificationEvent(n.Event,preferences.Language),
            DesktopLocalizedContent.NotificationMessage(n,preferences.Language),T(n.IsRead ? "Read" : "Unread"),n.CreatedAt)).ToArray();
        ((DataGridTextColumn)table.Columns[0]).ElementStyle=new Style(typeof(TextBlock),(Style)FindResource("TableTextStyle")){Setters={new Setter(ToolTipProperty,new Binding("Detail"))}};
        table.RowStyle=new Style(typeof(DataGridRow)){Setters={new Setter(ToolTipProperty,new Binding("Detail"))}};
        page.Children.Add(table); var actions=Row(page);
        Add(actions,"Mark Read",()=>{if(table.SelectedItem is not NotificationRow n) throw new RelayException("selection_required","请选择通知。"); service.MarkNotificationRead(n.Source.NotificationId);return Task.CompletedTask;});
        return page;
    }

    private void TaskPane(StackPanel page,IEnumerable<TaskView> views)
    {
        var rows=views.Select(v=>new TaskRow(v.Task.TaskId,$"{v.Task.RequesterId} → {v.Task.RecipientId}",v.Task.State.ToString(),v.NextAction,v.Task.DeadlineAt)).ToList();
        var table=Table("TasksTable",("Route","Route"),("State","State"),("Next action","NextAction"),("Deadline (UTC)","Deadline")); table.ItemsSource=rows; page.Children.Add(table);
        var actions=Row(page); var detail=Output("TaskDetail",215);
        string Selected()=> table.SelectedItem is TaskRow row ? row.TaskId : throw new RelayException("selection_required","请选择任务。");
        Add(actions,"Copy Prompt",()=>{var view=service.Status(Selected());Clipboard.SetText(DesktopLocalizedContent.TaskPrompt(view,service.Artifacts(view.Task.TaskId),preferences.Language));return Task.CompletedTask;},false);
        Add(actions,"Open Request",()=>{OpenFile(service.Status(Selected()).Task.RequestPath);return Task.CompletedTask;},false);
        Add(actions,"Open Result",()=>{var v=service.Status(Selected()); var result=service.Artifacts(v.Task.TaskId).SingleOrDefault(a=>a.Path==v.Task.ResultPath && a.State=="available" && a.ExpiresAt>DateTimeOffset.UtcNow);
            if(result is null) throw new RelayException("artifact_unavailable","结果尚未登记、已过期或缺失，请检查任务证据。"); OpenFile(result.Path); return Task.CompletedTask;},false);
        Add(actions,"Recover",async()=>{var v=service.Status(Selected());if(Review("Recover task",$"{RelayJson.Write(v)}\n恢复会处理已持久化的运输和继续 outbox；可能调用已登记适配器。") is not null) await service.RecoverAsync(v.Task.TaskId);});
        Add(actions,"Approve Dispatch",async()=>{var v=service.Status(Selected());if(Review("Approve dispatch",RelayJson.Write(v)+"\n仅批准投递；不启动新的模型执行 worker。") is not null) await service.ApproveAsync(v.Task.TaskId);});
        Add(actions,"Retry",async()=>{var v=service.Status(Selected()); var reason=Review("Retry task",RelayJson.Write(v)+"\n将创建新尝试，可能重复副作用；不会自动启动执行 worker。"); if(reason is not null) await service.RetryAsync(v.Task.TaskId,reason,true);});
        Add(actions,"Cancel",()=>{var v=service.Status(Selected());var reason=Review("Cancel Relay task",RelayJson.Write(v)+"\n停止 Relay 后续派发，不终止宿主中正在运行的 Agent。"); if(reason is not null)service.Cancel(v.Task.TaskId,reason);return Task.CompletedTask;});
        page.Children.Add(detail);
        table.SelectionChanged+=(_,_)=>{if(table.SelectedItem is TaskRow r){taskId=r.TaskId;var v=service.Status(taskId);detail.Text=$"{DesktopLocalizedContent.TaskPrompt(v,service.Artifacts(taskId),preferences.Language)}\n\n{RelayJson.Write(v)}\n\nDelivery evidence:\n{RelayJson.Write(service.DeliveryEvidence(taskId))}\n\nAudit:\n{RelayJson.Write(service.Audit(taskId))}";}};
        table.SelectedItem=rows.SingleOrDefault(r=>r.TaskId==taskId) ?? rows.FirstOrDefault();
    }

    private StackPanel AuditPage()
    {
        var page=Page(); Heading(page,"Event Timeline","按账本事件顺序展示；任务正文不在审计列表中。" );
        var form=new WrapPanel(); page.Children.Add(form); var filter=Field(form,"Task ID filter (empty = all)","AuditFilter",440); filter.Text=taskId ?? "";
        var table=Table("AuditTable",("Time (UTC)","RecordedAt"),("Task","TaskId"),("Event","Event"),("Actor","SenderId"),("Detail","Detail")); table.Height=390;
        table.ItemsSource=service.Audit(Optional(filter)); var actions=Row(page);
        Add(actions,"Apply Filter",()=>{table.ItemsSource=service.Audit(Optional(filter));return Task.CompletedTask;},false);
        var detail=Output("AuditDetail",120);table.SelectionChanged+=(_,_)=>{if(table.SelectedItem is AuditEvent e)detail.Text=RelayJson.Write(e);};
        page.Children.Add(table);page.Children.Add(detail);AddAssistant(page,"audit");return page;
    }

    private StackPanel AdaptersPage()
    {
        var page=Page();Heading(page,"Adapters & Migrations","Test 只运行明确指定的 CLI；来源记录不自动授予执行权限。" );
        var tabs=new TabControl();page.Children.Add(tabs);
        tabs.Items.Add(new TabItem{Header="Validation",Content=ValidationPanel()});
        tabs.Items.Add(new TabItem{Header="Migrations",Content=MigrationPanel()});
        tabs.SelectedIndex=adapterTab;tabs.SelectionChanged+=(_,e)=>{if(e.Source==tabs)adapterTab=tabs.SelectedIndex;};return page;
    }

    private StackPanel ValidationPanel()
    {
        var page=Page(); var table=Table("AdaptersTable",("Adapter","AdapterId"),("Version","Version"),("Verification","VerificationStatus"),("Executable","Executable")); table.ItemsSource=service.Adapters();page.Children.Add(table);
        var form=new WrapPanel();page.Children.Add(form);var amq=Field(form,"AMQ executable","AmqPath",500);var agent=Field(form,"Agent ID","AdapterAgent",220);var kind=Field(form,"Resumer kind","ResumerKind",220);kind.Text="codex-queue";
        var actions=Row(page);
        Add(actions,"Test AMQ",async()=>{var path=Required(amq,"AMQ executable");if(Review("Verify AMQ",$"将执行：{path}\n在 Relay 自身目录创建验证队列，不启动模型。") is not null)await service.VerifyAmqAsync(path);});
        Add(actions,"Test Executor",async()=>{var id=Required(agent,"Agent ID");if(Review("Verify executor",$"Agent: {id}\n执行已登记 CLI 的版本/能力探测，不启动模型。") is not null)await service.VerifyExecutorAsync(id);});
        Add(actions,"Test Resumer",async()=>{var id=Required(agent,"Agent ID");if(Review("Verify resumer",$"Agent: {id}\nKind: {kind.Text}\n执行已登记 CLI 的帮助/版本探测，不启动模型。") is not null)await service.VerifyResumerAsync(id,Required(kind,"Resumer kind"));});
        var detail=Output("AdapterSources",260);detail.Text=RelayJson.Write(new{sources=service.AdapterSources(),executors=service.Executors(),resumers=service.Resumers()});page.Children.Add(detail);AddAssistant(page,"adapters");return page;
    }

    private StackPanel MigrationPanel()
    {
        var page=Page();var form=new WrapPanel();page.Children.Add(form);
        var path=Field(form,"Config / manifest path","MigrationPath",500);var format=Field(form,"Format: json / toml","MigrationFormat",150);format.Text="toml";
        var proof=Field(form,"Proof task ID","MigrationProof",440);var detail=Output("MigrationDetail",280);
        var table=Table("MigrationsTable",("ID","MigrationId"),("State","State"),("Purpose","Request.Purpose"),("Target","Request.TargetPath")); var plans=service.Migrations();table.ItemsSource=plans;
        var actions=Row(page);
        Add(actions,"Browse",()=>{var d=new OpenFileDialog();if(d.ShowDialog(this)==true)path.Text=d.FileName;return Task.CompletedTask;},false);
        Add(actions,"Inventory",()=>{detail.Text=RelayJson.Write(service.InspectConfiguration(Required(path,"Config path"),Required(format,"Format")));return Task.CompletedTask;},false);
        Add(actions,"Plan Manifest",()=>{var request=JsonSerializer.Deserialize<MigrationRequest>(File.ReadAllText(Required(path,"Manifest path")),ManifestJson) ?? throw new JsonException("Expected migration manifest.");migrationId=service.PlanMigration(request).MigrationId;return Task.CompletedTask;});
        string Selected()=>table.SelectedItem is MigrationPlan p ? p.MigrationId : throw new RelayException("selection_required","请选择迁移提案。");
        string Preview(MigrationPlan p)=>File.ReadAllText(p.PreviewPath)+$"\n\nCurrent state: {p.State}\nBackup: {p.BackupPath}\n";
        Add(actions,"Apply",()=>{var p=service.Migration(Selected());var reason=Review("Apply migration",Preview(p));if(reason is not null)service.ApplyMigration(p.MigrationId,true,reason);return Task.CompletedTask;});
        Add(actions,"Validate Proof",()=>{service.ValidateMigration(Selected(),Required(proof,"Proof task ID"));return Task.CompletedTask;});
        Add(actions,"Rollback",()=>{var p=service.Migration(Selected());var reason=Review("Rollback migration",Preview(p)+"\n恢复原始备份；后续变更将阻止覆盖。先回滚旧入口停用提案，再回滚新入口。");if(reason is not null)service.RollbackMigration(p.MigrationId,true,reason);return Task.CompletedTask;});
        Add(actions,"Recover State",()=>{service.RecoverMigration(Selected());return Task.CompletedTask;});
        page.Children.Add(table);page.Children.Add(detail);
        table.SelectionChanged+=(_,_)=>{if(table.SelectedItem is MigrationPlan p){migrationId=p.MigrationId;proof.Text=p.ProofTaskId ?? "";detail.Text=Preview(p)+"\nAudit:\n"+RelayJson.Write(service.MigrationAudit(p.MigrationId));}};
        table.SelectedItem=plans.SingleOrDefault(p=>p.MigrationId==migrationId);AddAssistant(page,"migrations");return page;
    }

    private StackPanel SettingsPage()
    {
        var page=Page(); Heading(page,"Appearance","Choose language and palette");
        var appearanceForm = new WrapPanel(); page.Children.Add(appearanceForm);
        var language = Choice(appearanceForm, "Language", "LanguageChoice", Appearance.Languages, preferences.Language);
        var palette = Choice(appearanceForm, "Color palette", "PaletteChoice", Appearance.Palettes, preferences.Palette);
        var accent = Field(appearanceForm, "Custom accent (#RRGGBB)", "CustomAccent", 230); accent.Text = preferences.Accent ?? "";
        var accentPanel=(StackPanel)accent.Parent; accentPanel.Width=268;
        accentPanel.Children.Remove(accent);
        var accentRow=new DockPanel(); accentPanel.Children.Add(accentRow);
        var swatch=new Border{Name="AccentPreview",Width=24,Height=24,CornerRadius=new(6),BorderThickness=new(1),BorderBrush=Theme("BorderBrush"),Margin=new(8,3,0,10),VerticalAlignment=VerticalAlignment.Center,ToolTip=T("Accent preview")};
        DockPanel.SetDock(swatch,Dock.Right);accentRow.Children.Add(swatch);accentRow.Children.Add(accent);
        var accentError=new TextBlock{Name="AccentError",Foreground=Theme("DeniedBrush"),TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed};accentPanel.Children.Add(accentError);
        var appearanceActions = Row(page);
        var saveAccent=Add(appearanceActions,"Save Accent",() =>
        {
            var next = preferences with { Accent = Optional(accent) };
            Appearance.Save(service.Home,next); preferences=next; Appearance.Apply(this,next); UpdateChrome();
            return Task.CompletedTask;
        });
        void PreviewAccent()
        {
            saveAccent.IsEnabled=Optional(accent)!=preferences.Accent;
            try
            {
                swatch.Background=string.IsNullOrWhiteSpace(accent.Text) ? Theme("AccentBrush") : new SolidColorBrush(Appearance.ParseAccent(accent.Text.Trim()));
                accentError.Text="";accentError.Visibility=Visibility.Collapsed;
            }
            catch(RelayException error)
            {
                swatch.Background=Brushes.Transparent;accentError.Text=T(error.Message);accentError.Visibility=Visibility.Visible;
            }
        }
        accent.TextChanged+=(_,_)=>PreviewAccent();PreviewAccent();
        language.SelectionChanged+=async(_,_)=>{if(language.SelectedValue is string selected && selected!=preferences.Language)await ChangeLanguage(selected);};
        palette.SelectionChanged+=async(_,_)=>{if(palette.SelectedValue is string selected && selected!=preferences.Palette)await ChangeAppearance(preferences with{Palette=selected});};
        Heading(page,"Local Settings","Deadline Handling: On Next Command · 无计划任务、文件 watcher 或常驻服务。" );
        var settings=service.Settings();var form=new WrapPanel();page.Children.Add(form);
        var ttl=Field(form,"Artifact TTL (days)","TtlDays",180);ttl.Text=settings.ArtifactTtlDays.ToString(CultureInfo.InvariantCulture);
        var timeout=Field(form,"Task timeout (minutes)","TaskTimeout",180);timeout.Text=settings.TaskTimeoutMinutes.ToString(CultureInfo.InvariantCulture);
        var delivery=Field(form,"Delivery timeout (seconds)","DeliveryTimeout",180);delivery.Text=settings.DeliveryTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        var retries=Field(form,"Delivery attempts (max 3)","DeliveryAttempts",180);retries.Text=settings.MaxDeliveryAttempts.ToString(CultureInfo.InvariantCulture);
        var actions=Row(page);Add(actions,"Save Settings",()=>{service.Configure(new(Number(ttl),Number(timeout),Number(delivery),Number(retries)));return Task.CompletedTask;});
        var preview=Output("CleanupPreview",140);CleanupResult? proposed=null;
        Add(actions,"Preview Cleanup",()=>{proposed=service.Cleanup(false);preview.Text=RelayJson.Write(proposed);return Task.CompletedTask;},false);
        Add(actions,"Apply Cleanup",()=>{if(proposed is null)throw new RelayException("preview_required","请先 Preview Cleanup。");var current=service.Cleanup(false);if(!current.Paths.SequenceEqual(proposed.Paths))throw new RelayException("cleanup_changed","清理候选已变化，请重新预览。");if(Review("Delete expired managed artifacts",RelayJson.Write(current)) is not null)service.Cleanup(true);return Task.CompletedTask;});page.Children.Add(preview);
        Heading(page,"Desktop Notifications","仅管理 Relay 通知辅助程序，不改宿主配置。" );
        var notificationForm=new WrapPanel();page.Children.Add(notificationForm);var executable=Field(notificationForm,"Notification helper path","NotificationPath",620);executable.Text=service.DesktopExecutable() ?? "";
        var notificationActions=Row(page);
        Add(notificationActions,"Enable Notifications",async()=>{var path=Required(executable,"Notification helper");if(Review("Enable Relay notifications",$"将探测并登记通知程序：\n{path}\n后续完成事件可启动该程序显示 Windows 通知。") is not null)await service.ConfigureDesktopAsync(path);});
        Add(notificationActions,"Disable Notifications",()=>{service.DisableDesktop();return Task.CompletedTask;});AddAssistant(page,"settings",page.Children.Count);return page;
    }

    private string? Review(string title,string preview)
    {
        var dialog=new ReviewDialog(this,T(title),preview,preferences.Language);
        reviewCancelled=dialog.ShowDialog()!=true;
        return reviewCancelled ? null : dialog.Reason;
    }
    private Button Add(Panel row,string label,Func<Task> action,bool refresh=true)
    {
        var button=new Button{Content=T(label),Tag=label,Name=label.Replace(" ","").Replace("/","").Replace("→","To")};
        if(label is "Save Accent" or "Save Settings" or "Register / Correct Path")button.Style=(Style)FindResource("PrimaryButtonStyle");
        AutomationProperties.SetAutomationId(button,button.Name);row.Children.Add(button);
        button.Click+=async(_,_)=>
        {
            var drafts=label=="Save Accent" ? CaptureInputs() : null;
            await Run(action,T(label)+": "+T("Ready"),refresh);
            if(drafts is not null)RestoreInputs(drafts);
        };
        return button;
    }
    private static StackPanel Page()=>new(){Margin=new Thickness(0,0,0,8)};
    private void Heading(Panel page,string title,string subtitle)
    {page.Children.Add(new TextBlock{Text=T(title),FontSize=17,FontWeight=FontWeights.SemiBold,Margin=new(0,6,0,7)});page.Children.Add(new TextBlock{Text=T(subtitle),TextWrapping=TextWrapping.Wrap,Foreground=Theme("MutedBrush"),Margin=new(0,0,0,14)});}
    private static WrapPanel Row(Panel page){var row=new WrapPanel{Margin=new(0,10,0,6)};page.Children.Add(row);return row;}
    private TextBox Field(Panel parent,string label,string name,double width)
    {
        var p=new StackPanel{Width=width,Margin=new(0,10,12,0)};
        var header=new DockPanel{MinHeight=36};p.Children.Add(header);
        var box=new TextBox{Name=name}; AutomationProperties.SetAutomationId(box,name); fields.Add(name,box);
        var copy=new Button{Name=name+"CopySetupPrompt",ToolTip=T("Copy this field prompt"),Padding=new(5),Margin=new(4,0,0,0),MinHeight=22,Width=26,Height=24,
            Content=new System.Windows.Shapes.Path{Data=Geometry.Parse("M3,5 L3,14 L11,14 M6,2 L14,2 L14,11 L6,11 Z"),Stroke=Theme("AccentBrush"),StrokeThickness=1.5,Width=16,Height=16}};
        AutomationProperties.SetAutomationId(copy,copy.Name);AutomationProperties.SetName(copy,T("Copy this field prompt")+": "+T(label));
        DockPanel.SetDock(copy,Dock.Right);header.Children.Add(copy);
        header.Children.Add(new TextBlock{Text=T(label),Foreground=Theme("MutedBrush"),VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap});
        copy.Click+=async(_,_)=>await Run(()=>{Clipboard.SetText(SetupAssistant.CreatePrompt(FieldContext(name),new Dictionary<string,string>{{name,box.Text}},preferences.Language));return Task.CompletedTask;},"Copied setup prompt. Paste it into the Agent you want to register.",false);
        p.Children.Add(box);parent.Children.Add(p);return box;
    }
    private static TextBox Output(string name,double height)=>new(){Name=name,IsReadOnly=true,Height=height,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontFamily=new("Consolas"),FontSize=12};
    private DataGrid Table(string name,params(string Label,string Path)[] columns)
    {var table=new DataGrid{Name=name,Height=200};AutomationProperties.SetAutomationId(table,name);foreach(var c in columns){var binding=new Binding(c.Path);
        if(c.Path is "State" or "Display" or "Permission" or "Capability" or "ValidationStatus" or "VerificationStatus" or "NextAction" or "Request.Purpose")binding.Converter=new UiValueConverter(preferences.Language);
        table.Columns.Add(new DataGridTextColumn{Header=T(c.Label),Binding=binding,ElementStyle=(Style)FindResource("TableTextStyle"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});}return table;}
    private string Required(TextBox box,string label)=>!string.IsNullOrWhiteSpace(box.Text) ? box.Text.Trim() : throw new RelayException("input_required",string.Format(CultureInfo.CurrentUICulture,T("Please fill in {0}."),T(label)));
    private static string? Optional(TextBox box)=>string.IsNullOrWhiteSpace(box.Text) ? null : box.Text.Trim();
    private int Number(TextBox box)=>int.TryParse(box.Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var value) ? value : throw new RelayException("invalid_number",T("Settings must be integers."));
    private static Brush Brush(string color)=>(Brush)new BrushConverter().ConvertFromString(color)!;
    private static void OpenFile(string path)
    {if(!File.Exists(path))throw new RelayException("artifact_missing","文件已过期或缺失。");Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}

    private string T(string source)=>UiText.Translate(source,preferences.Language);
    private Dictionary<string,string> CaptureInputs()=>fields.Values.Concat(SetupReplies(PageContent)).ToDictionary(b=>b.Name,b=>b.Text);
    private void RestoreInputs(IReadOnlyDictionary<string,string> drafts)
    {foreach(var box in fields.Values.Concat(SetupReplies(PageContent)))if(drafts.TryGetValue(box.Name,out var value))box.Text=value;}
    private static IEnumerable<TextBox> SetupReplies(DependencyObject root)
    {
        if(root is TextBox box && box.Name.EndsWith("SetupReply",StringComparison.Ordinal))yield return box;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var reply in SetupReplies(child))yield return reply;
    }
    private Brush Theme(string key)=>(Brush)FindResource(key);
    private static string FieldContext(string name)=>name switch
    {
        "AgentId" or "AgentHost" or "AgentConfig" or "AgentCli" or "AgentVersion" or "AgentInstructions"=>"agents",
        "ConnectionFrom" or "ConnectionTo"=>"connections",
        "AmqPath" or "AdapterAgent" or "ResumerKind"=>"adapters",
        "MigrationPath" or "MigrationFormat" or "MigrationProof"=>"migrations",
        "AuditFilter"=>"audit",
        "CustomAccent" or "TtlDays" or "TaskTimeout" or "DeliveryTimeout" or "DeliveryAttempts" or "NotificationPath"=>"settings",
        _=>throw new InvalidOperationException("Unknown configuration field: "+name)
    };
    private void AddAssistant(StackPanel page,string context,int index=0)
    {
        var group=fields.Where(f=>FieldContext(f.Key)==context).ToDictionary(f=>f.Key,f=>f.Value);
        if(group.Count>0)page.Children.Insert(index,new FormAssistant(context,group,preferences.Language,(action,message)=>Run(action,message,false)));
    }
    private ComboBox Choice(Panel parent,string label,string name,IReadOnlyList<AppearanceOption> options,string selected)
    {
        var panel=new StackPanel{Width=200,Margin=new(0,10,12,0)};parent.Children.Add(panel);
        var header=new Grid{Height=36};header.Children.Add(new TextBlock{Text=T(label),Foreground=Theme("MutedBrush"),VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap});panel.Children.Add(header);
        var choice=new ComboBox{Name=name,ItemsSource=options.Select(o=>new AppearanceOption(o.Id,T(o.Label))).ToArray(),DisplayMemberPath="Label",SelectedValuePath="Id",SelectedValue=selected,Margin=new(0,3,0,10)};
        AutomationProperties.SetAutomationId(choice,name);panel.Children.Add(choice);return choice;
    }
    private void UpdateChrome()
    {
        Language=System.Windows.Markup.XmlLanguage.GetLanguage(preferences.Language);
        languageReady=false;QuickLanguage.SelectedValue=preferences.Language;languageReady=true;
        RefreshButton.Content=T("Refresh");
        foreach(ListBoxItem item in Navigation.Items)
        {
            var page=(string)item.Tag;
            var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new(20)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new(1,GridUnitType.Star)});
            row.Children.Add(NavigationIcon.Create(page,Theme("NavTextBrush")));
            var label=new TextBlock{Text=T(page),Margin=new(10,0,0,0),VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,ToolTip=T(page)};
            Grid.SetColumn(label,1);row.Children.Add(label);item.Content=row;item.ToolTip=T(page);
            AutomationProperties.SetName(item,T(page));AutomationProperties.SetAutomationId(item,page);
        }
        Localize(ModeDescription);
        Localize(BrandSubtitle);Localize(BrandTagline);HomePath.ToolTip=T("Relay data directory");
    }
    private void Localize(DependencyObject? root)
    {
        if(root is null)return;
        if(root is TextBlock text){text.Tag??=text.Text;text.Text=T((string)text.Tag);}
        if(root is TabItem tab && tab.Header is string header){tab.Tag??=header;tab.Header=T((string)tab.Tag);}
        if(root is ContentControl control && control.Content is string content){control.Tag??=content;control.Content=T((string)control.Tag);}
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())Localize(child);
    }
}

internal sealed class UiValueConverter(string language):IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>UiText.Translate(value.ToString()!,language);
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
