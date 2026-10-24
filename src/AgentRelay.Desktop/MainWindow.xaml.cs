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
    private RelayService service;
    private readonly bool demo;
    private DesktopPreferences preferences;
    private bool languageReady;
    private readonly Dictionary<string, TextBox> fields = new(StringComparer.Ordinal);
    private bool busy;
    private bool reviewCancelled;
    private bool refreshPending;
    private uint ledgerMessage;
    private HwndSource? source;
    private string? taskId;
    private string? agentId;
    private string? connectionFrom;
    private string? connectionTo;
    public MainWindow(RelayService service, bool demo = false)
    {
        this.service = service;
        this.demo = demo;
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
            await Run(() => Task.CompletedTask, "Ready");
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
            "Settings" => SettingsPage(),
            _ => throw new InvalidOperationException(page)
        };
        Localize(PageContent.Content as DependencyObject);
    }

    private string AgentName(string id) => service.Agents().SingleOrDefault(a=>a.AgentId==id)?.DisplayName ?? id;
    private string HandoffPrompt(TaskView view)
    {
        var task=view.Task;
        var text=DesktopLocalizedContent.TaskPrompt(view,service.Artifacts(task.TaskId),preferences.Language);
        text+="\n\nRelay home: "+service.Home+"\nRequest: "+task.RequestPath+"\nResult: "+task.ResultPath;
        if(task.State!=TaskState.AwaitingManual)return text;
        var executable=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","relay.exe"));
        var command=File.Exists(executable)?"& '"+executable.Replace("'","''")+"'":"relay";
        var common=" --home '"+service.Home.Replace("'","''")+"' task ";
        var identity=$" --task {task.TaskId} --attempt {task.AttemptId} --sender {task.RecipientId}";
        return text+"\n\nPowerShell callback commands (use the installed Relay CLI or equivalent MCP tools):\n"+
            command+common+"ack"+identity+" --idempotency-key received\n"+
            command+common+"start"+identity+" --idempotency-key started\n"+
            command+common+"complete"+identity+" --idempotency-key completed --status succeeded --result '"+task.ResultPath.Replace("'","''")+"'\n"+
            "On failure, use task fail with --status failed --error-code and --message plus the failure report path. Report only actual outcomes. If CLI/MCP is unavailable, return your reply file to the user; they can import it in the task details. File changes alone do not mark a task completed.";
    }
    private TextBlock Note(string text) => new(){Text=T(text),TextWrapping=TextWrapping.Wrap,Foreground=Theme("MutedBrush"),Margin=new(0,6,0,12)};
    private void Go(string page)
    {
        Navigation.SelectedItem=Navigation.Items.Cast<ListBoxItem>().Single(i=>(string)i.Tag==page);
    }

    private StackPanel HomePage()
    {
        var page=Page();
        page.Children.Add(Note("Relay stores collaboration messages, requests and temporary replies. Keep final deliverables and long-term references in your project folder; relay files expire under the retention policy."));
        var summary=service.Summary();
        var cards=Row(page);
        Add(cards,T("Registered Agents")+": "+summary.RegisteredAgents,()=>{Go("Agents");return Task.CompletedTask;});
        cards.Children.Add(Note(T("Pending Tasks")+": "+summary.PendingTasks+"  ·  "+T("Retention")+": "+summary.RetentionDays+" "+T("days")));
        var actions=Row(page);
        Add(actions,"New handoff",async()=>
        {
            var agents=service.Agents().Select(a=>new HandoffAgentOption(a.AgentId,a.DisplayName??a.AgentId)).ToArray();
            if(agents.Length<2)throw new RelayException("agents_required",T("Add at least two Agents first."));
            var dialog=new HandoffDialog(this,preferences.Language,agents,(from,to)=>service.Connections().Any(e=>e.From==from&&e.To==to&&e.Display=="Allowed") && service.CliExtension(to)?.CanCreate==true);
            if(dialog.ShowDialog()!=true){reviewCancelled=true;return;}
            var draft=dialog.Result!;
            var result=await service.DispatchAsync(new(draft.From,draft.To,draft.Content,draft.Automatic?"auto":"manual",draft.Automatic?"cli:"+draft.To:"manual",Guid.NewGuid().ToString()));
            taskId=result.Task.TaskId;
        });
        Add(actions,"Open relay folder",()=>{Process.Start(new ProcessStartInfo(service.Home){UseShellExecute=true});return Task.CompletedTask;},false);
        TaskPane(page,service.Tasks());return page;
    }

    private sealed record AgentRow(AgentRecord Source,string Name,string Host,string Capability);
    private StackPanel AgentsPage()
    {
        var page=Page();Heading(page,"Your Agents","Choose your own Agent names. Relay only needs to know who sends and who receives.");
        var actions=Row(page);
        var table=Table("AgentsTable",("Name","Name"),("Host","Host"),("CLI capability","Capability"));
        async Task Edit(AgentRecord? existing)
        {
            var dialog=new AgentEntryDialog(this,preferences.Language,existing);
            if(dialog.ShowDialog()!=true){reviewCancelled=true;return;}
            var draft=dialog.Result!;
            var id=existing?.AgentId ?? "agent-"+Guid.NewGuid().ToString("N")[..12];
            if(string.IsNullOrWhiteSpace(draft.ConfigPath)) service.RegisterManual(id,draft.Host,draft.Name,draft.CliPath,draft.HostVersion,draft.InstructionPaths);
            else service.Register(id,draft.Host,draft.ConfigPath,draft.CliPath,draft.HostVersion,draft.InstructionPaths,draft.Name);
            agentId=id;await Task.CompletedTask;
        }
        Add(actions,"Add Agent",()=>Edit(null));
        AgentRecord Selected()=>table.SelectedItem is AgentRow a?a.Source:throw new RelayException("selection_required",T("Select an Agent."));
        Add(actions,"Edit Agent",()=>Edit(Selected()));
        Add(actions,"Remove Agent",()=>
        {
            var agent=Selected();
            if(MessageBox.Show(this,T("Remove this Agent from Relay? Existing task history is kept."),agent.DisplayName??agent.AgentId,MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK){reviewCancelled=true;return Task.CompletedTask;}
            service.RemoveAgent(agent.AgentId);agentId=null;return Task.CompletedTask;
        });
        Add(actions,"Test CLI",()=>
        {
            agentId=Selected().AgentId;new CliProbeDialog(this,service,agentId,preferences.Language,demo).ShowDialog();return Task.CompletedTask;
        });
        var rows=service.Agents().Select(a=>new AgentRow(a,a.DisplayName??a.AgentId,a.Host,
            T(service.CliExtension(a.AgentId) is {CanResume:true}?"New session and resume verified":service.CliExtension(a.AgentId) is {CanCreate:true}?"New session verified":"Manual handoff"))).ToArray();
        table.ItemsSource=rows;page.Children.Add(table);
        table.SelectionChanged+=(_,_)=>{if(table.SelectedItem is AgentRow a)agentId=a.Source.AgentId;};
        table.SelectedItem=rows.SingleOrDefault(a=>a.Source.AgentId==agentId)??rows.FirstOrDefault();
        page.Children.Add(Note("Add or edit an Agent to copy one setup prompt and parse its reply. CLI testing is optional; manual handoff is always available."));
        return page;
    }

    private StackPanel ConnectionsPage()
    {
        var page=Page();Heading(page,"Directed connections","Green: enabled · Red: paused or not enabled · Gray dashed: no calling recipe");
        page.Children.Add(Note("A → B means A can deliver files through Relay and notify B by CLI. The reverse direction is independent: a red or gray B → A route cannot automatically notify A. Manual copy remains available. These switches only control Relay routes."));
        var edges=service.Connections();
        var detail=Page();
        void Select(ConnectionView edge)
        {
            connectionFrom=edge.From;connectionTo=edge.To;detail.Children.Clear();
            Heading(detail,AgentName(edge.From)+" → "+AgentName(edge.To),edge.Display=="Unavailable"?"No calling recipe. Copy a handoff prompt, or try an advanced CLI extension.":edge.Display=="Allowed"?"Automatic notification is enabled for this direction.":"A calling recipe is available; this direction is paused or not enabled.");
            var actions=Row(detail);
            if(edge.Display!="Unavailable") Add(actions,edge.Display=="Allowed"?"Pause connection":"Enable connection",()=>{service.SetPermission(edge.From,edge.To,edge.Display=="Allowed"?"deny":"allow");return Task.CompletedTask;});
            else Add(actions,"CLI interface extensions",()=>{agentId=edge.To;Go("Settings");return Task.CompletedTask;});
            Add(actions,"Manual handoff",()=>{Go("Home");return Task.CompletedTask;});
        }
        page.Children.Add(new ConnectionGraph(service.Agents(),edges,Select,preferences.Language,this,AgentName));
        page.Children.Add(detail);
        var selected=edges.SingleOrDefault(e=>e.From==connectionFrom&&e.To==connectionTo)??edges.FirstOrDefault();
        if(selected is not null)Select(selected);
        return page;
    }

    private sealed record TaskRow(string TaskId,string Route,string Mode,string State,DateTimeOffset Created);
    private void TaskPane(StackPanel page,IEnumerable<TaskView> views)
    {
        var rows=views.OrderByDescending(v=>v.Task.CreatedAt).Select(v=>new TaskRow(v.Task.TaskId,AgentName(v.Task.RequesterId)+" → "+AgentName(v.Task.RecipientId),T(v.Task.Mode=="manual"?"Manual handoff":"CLI"),v.Task.State.ToString(),v.Task.CreatedAt.ToLocalTime())).ToArray();
        var table=Table("TasksTable",("Route","Route"),("Delivery","Mode"),("State","State"),("Created","Created"));table.Height=230;
        var stateStyle=new Style(typeof(TextBlock),(Style)FindResource("TableTextStyle"));
        foreach(var state in new[]{"Succeeded","Failed","TimedOut","DeliveryUnknown","Running","Acknowledged"})
        {
            var trigger=new DataTrigger{Binding=new Binding("State"),Value=state};
            trigger.Setters.Add(new Setter(TextBlock.ForegroundProperty,state=="Succeeded"?Theme("AllowedBrush"):state is "Running" or "Acknowledged"?Brush("#8C691F"):Theme("DeniedBrush")));
            stateStyle.Triggers.Add(trigger);
        }
        ((DataGridTextColumn)table.Columns[2]).ElementStyle=stateStyle;
        table.ItemsSource=rows;page.Children.Add(table);
        var pane=Page();page.Children.Add(pane);
        table.SelectionChanged+=(_,_)=>
        {
            pane.Children.Clear();if(table.SelectedItem is not TaskRow row)return;
            taskId=row.TaskId;var view=service.Status(taskId);var task=view.Task;
            var success=task.State==TaskState.Succeeded;
            Heading(pane,row.Route,T(task.State.ToString())+(task.ErrorMessage is null?"":" · "+task.ErrorMessage));
            var actions=Row(pane);
            var recipient=task.State is TaskState.AwaitingManual or TaskState.Dispatched or TaskState.Acknowledged or TaskState.Running?task.RecipientId:task.RequesterId;
            Add(actions,string.Format(CultureInfo.CurrentUICulture,T("Copy prompt for {0}"),AgentName(recipient)),()=>{Clipboard.SetText(HandoffPrompt(view));return Task.CompletedTask;},false);
            if(task.State==TaskState.AwaitingConfirmation)Add(actions,"Approve Dispatch",async()=>{await service.ApproveAsync(task.TaskId);});
            Add(actions,"Open task folder",()=>{Process.Start(new ProcessStartInfo(Path.GetDirectoryName(task.RequestPath)!){UseShellExecute=true});return Task.CompletedTask;},false);
            if(task.State is TaskState.AwaitingManual or TaskState.Dispatched or TaskState.Acknowledged or TaskState.Running)
                Add(actions,"Import completed reply",async()=>
                {
                    var picker=new OpenFileDialog{Title=T("Import completed reply"),InitialDirectory=Path.GetDirectoryName(task.ResultPath),Filter="Markdown / Text|*.md;*.txt|All files|*.*"};
                    if(picker.ShowDialog(this)!=true){reviewCancelled=true;return;}
                    if(MessageBox.Show(this,T("Confirm that the recipient finished this handoff and this file is its reply."),T("Import completed reply"),MessageBoxButton.OKCancel)!=MessageBoxResult.OK){reviewCancelled=true;return;}
                    var artifact=service.RegisterArtifact(task.TaskId,task.AttemptId,"result",picker.FileName);
                    service.Complete(task.TaskId,task.AttemptId,task.RecipientId,Guid.NewGuid().ToString(),artifact.Path);
                    await service.ProcessContinuationsAsync(task.TaskId);
                });
            var request=Output("TaskRequest",130);request.Text=File.Exists(task.RequestPath)?File.ReadAllText(task.RequestPath):T("File expired or missing.");
            pane.Children.Add(new Expander{Header=T("Request"),Content=request,IsExpanded=true,Margin=new(0,8,0,8)});
            if(success||task.State==TaskState.Failed)
            {
                var reply=Output("TaskResult",130);reply.Text=File.Exists(task.ResultPath)?File.ReadAllText(task.ResultPath):T("File expired or missing.");
                pane.Children.Add(new Expander{Header=T("Reply"),Content=reply,IsExpanded=true,Margin=new(0,8,0,8)});
            }
            var history=Page();
            foreach(var e in service.Audit(task.TaskId))history.Children.Add(Note(e.RecordedAt.ToLocalTime().ToString("g")+" · "+DesktopLocalizedContent.NotificationEvent(e.Event,preferences.Language)));
            var more=Row(history);
            if(task.State is not (TaskState.Succeeded or TaskState.Failed or TaskState.Cancelled or TaskState.TimedOut))
                Add(more,"Cancel",()=>{if(MessageBox.Show(this,T("Cancel this handoff? This does not stop an already running Agent."),T("Cancel"),MessageBoxButton.OKCancel)==MessageBoxResult.OK)service.Cancel(task.TaskId,"Cancelled by user");else reviewCancelled=true;return Task.CompletedTask;});
            if(task.State is TaskState.Failed or TaskState.TimedOut or TaskState.DeliveryUnknown)
                Add(more,"Retry",async()=>{if(MessageBox.Show(this,T("Retry may repeat a delivery or model call. Continue?"),T("Retry"),MessageBoxButton.OKCancel)==MessageBoxResult.OK)await service.RetryAsync(task.TaskId,"Retry requested by user",true);else reviewCancelled=true;});
            Add(more,"Copy diagnostics",()=>{Clipboard.SetText(RelayJson.Write(new{view,events=service.Audit(task.TaskId),delivery=service.DeliveryEvidence(task.TaskId)}));return Task.CompletedTask;},false);
            pane.Children.Add(new Expander{Header=T("Processing history"),Content=history,Margin=new(0,8,0,8)});
        };
        table.SelectedItem=rows.SingleOrDefault(r=>r.TaskId==taskId)??rows.FirstOrDefault();
        if(rows.Length==0)page.Children.Add(Note("No handoffs yet. Add two Agents, then create your first handoff."));
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
        Heading(page,"Relay storage","This folder holds temporary handoff files and the local ledger.");
        var location=Output("RelayLocation",60);location.Text=service.Home;page.Children.Add(location);
        var storageActions=Row(page);
        Add(storageActions,"Change relay folder",()=>
        {
            var picker=new OpenFolderDialog{Title=T("Choose relay folder")};
            if(picker.ShowDialog(this)!=true){reviewCancelled=true;return Task.CompletedTask;}
            var next=service.Relocate(picker.FolderName);
            service=next;HomePath.Text=service.Home;
            if(!demo)RelayLocation.Save(service.Home);
            return Task.CompletedTask;
        });
        Add(storageActions,"Check migration",()=>{service=service.VerifyRelocation();if(!demo)RelayLocation.Save(service.Home);HomePath.Text=service.Home;return Task.CompletedTask;});
        var settings=service.Settings();var form=new WrapPanel();page.Children.Add(form);
        var ttl=Field(form,"Artifact TTL (days)","TtlDays",180);ttl.Text=settings.ArtifactTtlDays.ToString(CultureInfo.InvariantCulture);
        var advanced=Page();var advancedForm=new WrapPanel();advanced.Children.Add(advancedForm);
        var timeout=Field(advancedForm,"Task timeout (minutes)","TaskTimeout",180);timeout.Text=settings.TaskTimeoutMinutes.ToString(CultureInfo.InvariantCulture);
        var delivery=Field(advancedForm,"Delivery timeout (seconds)","DeliveryTimeout",180);delivery.Text=settings.DeliveryTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        var retries=Field(advancedForm,"Delivery attempts (max 3)","DeliveryAttempts",180);retries.Text=settings.MaxDeliveryAttempts.ToString(CultureInfo.InvariantCulture);
        page.Children.Add(new Expander{Header=T("Advanced task settings"),Content=advanced});
        var actions=Row(page);Add(actions,"Save Settings",()=>{service.Configure(new(Number(ttl),Number(timeout),Number(delivery),Number(retries)));return Task.CompletedTask;});
        Add(actions,"Clean expired files",()=>{var proposed=service.Cleanup(false);if(MessageBox.Show(this,string.Format(CultureInfo.CurrentUICulture,T("Delete {0} expired relay files?"),proposed.Eligible),T("Clean expired files"),MessageBoxButton.OKCancel)==MessageBoxResult.OK)service.Cleanup(true);else reviewCancelled=true;return Task.CompletedTask;});
        Heading(page,"Desktop Notifications","Notify when Relay processes a completed handoff. Closing this app does not start a background monitor.");
        var notify=new CheckBox{Content=T("Enable Notifications"),IsChecked=service.DesktopExecutable() is not null,Margin=new(0,4,0,16)};
        page.Children.Add(notify);
        notify.Click+=async(_,_)=>await Run(async()=>
        {
            if(notify.IsChecked==true)await service.ConfigureDesktopAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","desktop","AgentRelay.Notify.exe")));
            else service.DisableDesktop();
        },"Ready");
        var extensions=Page();
        extensions.Children.Add(Note("CLI extensions connect verified command-line tools or bridges. Availability depends on the target software, version, environment and session support. Not every Agent supports automatic calls or resuming a session. Test before enabling; manual handoff remains available."));
        var choice=Choice(extensions,"Agent","ExtensionAgent",service.Agents().Select(a=>new AppearanceOption(a.AgentId,a.DisplayName??a.AgentId)).ToArray(),agentId??service.Agents().FirstOrDefault()?.AgentId??"");
        choice.SelectionChanged+=(_,_)=>{agentId=choice.SelectedValue as string;};
        string Selected()=>choice.SelectedValue is string id?id:throw new RelayException("selection_required",T("Select an Agent."));
        var recipe=new TextBox{Name="CliRecipeReply",AcceptsReturn=true,Height=150,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};extensions.Children.Add(recipe);
        fields.Add(recipe.Name,recipe);
        var extensionActions=Row(extensions);
        Add(extensionActions,"Copy extension prompt",()=>{Clipboard.SetText(service.CliRecipePrompt(Selected()));return Task.CompletedTask;},false);
        Add(extensionActions,"Paste Agent Reply",()=>{recipe.Text=Clipboard.GetText();return Task.CompletedTask;},false);
        Add(extensionActions,"Import calling recipe",()=>{service.ImportCliRecipe(Selected(),recipe.Text);return Task.CompletedTask;});
        Add(extensionActions,"Test CLI",()=>{new CliProbeDialog(this,service,Selected(),preferences.Language,demo).ShowDialog();return Task.CompletedTask;});
        extensions.Children.Add(Note("Import saves a recipe without running it. Review the executable and arguments in the test dialog before making a real call."));
        page.Children.Add(new Expander{Header=T("Advanced: CLI interface extensions"),Content=extensions,IsExpanded=agentId is not null,Margin=new(0,16,0,8)});
        return page;
    }


    private string? Review(string title,string preview)
    {
        var dialog=new ReviewDialog(this,T(title),preview,preferences.Language);
        reviewCancelled=dialog.ShowDialog()!=true;
        return reviewCancelled ? null : dialog.Reason;
    }
    private Button Add(Panel row,string label,Func<Task> action,bool refresh=true)
    {
        var name=System.Text.RegularExpressions.Regex.Replace(label,"[^a-zA-Z0-9_]","");
        if(name.Length==0 || !char.IsLetter(name[0]))name="Action"+Guid.NewGuid().ToString("N");
        var button=new Button{Content=T(label),Tag=label,Name=name};
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
        header.Children.Add(new TextBlock{Text=T(label),Foreground=Theme("MutedBrush"),VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap});
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
