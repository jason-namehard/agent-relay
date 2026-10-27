using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using AgentRelay.Core;

namespace AgentRelay.Desktop;

public partial class MainWindow
{
    private sealed record AgentRow(AgentRecord Source, string Name, string Bridge, Brush BridgeBrush, string Activity);

    private HashSet<string> ActiveAgentIds() => service.Tasks()
        .Where(view => view.Task.State is not (TaskState.Succeeded or TaskState.Failed or TaskState.TimedOut or TaskState.Cancelled))
        .SelectMany(view => new[] { view.Task.RequesterId, view.Task.RecipientId }).ToHashSet(StringComparer.Ordinal);

    private StackPanel AgentsPage()
    {
        var page = Page();
        Heading(page, "Your Agents", "Use one name per Agent. Configure it directly with a prompt, or enter the fields yourself.");
        var actions = Row(page);
        var table = Table("AgentsTable", ("Name", "Name"), ("Agent bridge", "Bridge"), ("Handoffs", "Activity"));
        table.Columns[1] = BridgeColumn();
        var details = new StackPanel { Margin = new(10) };

        Task Edit(AgentRecord? existing)
        {
            var dialog = new AgentEntryDialog(this, preferences.Language, existing, service);
            StatusText.Text = T("Waiting for Agent details…");
            if (dialog.ShowDialog() != true) { reviewCancelled = true; return Task.CompletedTask; }
            var draft = dialog.Result!;
            var id = existing?.AgentId ?? "agent-" + Guid.NewGuid().ToString("N")[..12];
            var priorRecipe = existing is null ? null : service.CliExtension(id)?.Recipe;
            if (string.IsNullOrWhiteSpace(draft.ConfigPath))
                service.RegisterManual(id, draft.Host, draft.Name, draft.CliPath, draft.HostVersion, draft.InstructionPaths);
            else service.Register(id, draft.Host, draft.ConfigPath, draft.CliPath, draft.HostVersion, draft.InstructionPaths, draft.Name);
            if (dialog.CliResult is { } cli)
            {
                var command = priorRecipe is null
                    ? new CliCommand(cli.Executable, cli.Arguments, cli.Input, cli.WorkingDirectory, cli.Environment)
                    : priorRecipe.Create with { Executable = cli.Executable, Arguments = cli.Arguments, Input = cli.Input, WorkingDirectory = cli.WorkingDirectory, Environment = cli.Environment };
                var recipe = priorRecipe is null
                    ? new CliRecipe(1, draft.Name + " CLI", draft.Host, command, null, [], "", "manual", "", "", cli.OutputFormat)
                    : priorRecipe with { Create = command, Resume = priorRecipe.Resume is { } resume ? resume with { Environment = cli.Environment } : null, OutputFormat = cli.OutputFormat };
                if (priorRecipe is null || RelayJson.Write(recipe) != RelayJson.Write(priorRecipe))
                    service.ImportCliRecipe(id, RelayJson.Write(recipe));
            }
            service.RefreshGuidance(); agentId = id;
            return Task.CompletedTask;
        }

        AgentRecord Selected() => table.SelectedItem is AgentRow row ? row.Source
            : throw new RelayException("selection_required", T("Select an Agent."));

        Add(actions, "New Agent", () => Edit(null));
        Add(actions, "Edit Agent", () => Edit(Selected()));
        Add(actions, "Delete Agent", () =>
        {
            var agent = Selected();
            if (ActiveAgentIds().Contains(agent.AgentId))
                throw new RelayException("agent_busy", T("This Agent has unfinished handoffs. Complete or cancel them before removing it."));
            StatusText.Text = T("Waiting for confirmation…");
            if (MessageBox.Show(this, T("Remove this Agent from Relay? Existing task history is kept."), agent.DisplayName ?? agent.AgentId,
                    MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            { reviewCancelled = true; return Task.CompletedTask; }
            service.RemoveAgent(agent.AgentId); service.RemoveGuidance(agent.AgentId); agentId = null;
            return Task.CompletedTask;
        });
        Add(actions, "Test bridge", () =>
        {
            agentId = Selected().AgentId; ShowCliProbe(agentId); return Task.CompletedTask;
        });
        Add(actions, "One-click setup", () =>
        {
            Clipboard.SetText(service.AgentSetupPrompt(relayCommand: AgentEntryDialog.ResolveRelayCommand()));
            MessageBox.Show(this, T("Setup prompt copied. Paste it into a new conversation with the Agent you want to register. It will inspect its calling method and fill Relay directly. This creates a new node and does not overwrite the selected Agent."),
                T("One-click setup"), MessageBoxButton.OK, MessageBoxImage.Information);
            return Task.CompletedTask;
        }, false);

        var active = ActiveAgentIds();
        var rows = service.Agents().Select(agent =>
        {
            var bridge = service.Bridge(agent.AgentId);
            return new AgentRow(agent, agent.DisplayName ?? agent.AgentId, BridgePresentation.Label(bridge, T), BridgePresentation.Accent(this, bridge),
                T(active.Contains(agent.AgentId) ? "Unfinished handoffs" : "No unfinished handoffs"));
        }).ToArray();
        table.ItemsSource = rows; page.Children.Add(table);
        page.Children.Add(Note("One-click setup is for a new node. To change an existing node, select it and use Edit Agent. An Agent bridge connects Relay to the Agent through ACP or its CLI; ACP is used when the host supports it. Without a bridge you copy prompts by hand."));
        page.Children.Add(new Expander { Header = T("Handoff rules and node details"), Content = details, Margin = new(0, 10, 0, 0) });
        void ShowDetails()
        {
            details.Children.Clear();
            if (table.SelectedItem is not AgentRow row) return;
            agentId = row.Source.AgentId;
            details.Children.Add(Note("Agent ID" + ": " + row.Source.AgentId));
            details.Children.Add(Note("Install the rule in the instruction file your host actually loads. The one-line rule uses a little context; details are read on demand. Existing sessions may need to reread the file or restart."));
            var ruleActions = Row(details);
            Add(ruleActions, "Install handoff rule", () => { InstallRules(Selected().AgentId); return Task.CompletedTask; });
            Add(ruleActions, "Remove handoff rule", () => { service.RemoveGuidance(Selected().AgentId); return Task.CompletedTask; });
            if(service.CliExtension(row.Source.AgentId) is {} extension)
            {
                details.Children.Add(Note(T("Agent bridge")+": "+BridgePresentation.Label(service.Bridge(row.Source.AgentId),T)+" · "+T(extension.CanCreate?"New conversations verified":"New conversations not verified")+" · "+
                    T(extension.CanResume?"Resume verified":"Resume not verified")+
                    (extension.CreateEvidence is not null||extension.ResumeEvidence is not null?" · "+T("Proven by a real handoff"):"")));
                if(extension.Block is {} block)
                {
                    details.Children.Add(Note(block.Message));
                    Add(Row(details),"I have resolved this · Allow automatic sends",()=>{service.ClearCliBlock(row.Source.AgentId);return Task.CompletedTask;});
                }
            }
            if(service.Bridge(row.Source.AgentId).HostNote is {} hostNote)details.Children.Add(Note(hostNote));
            var health=new StackPanel();
            Add(Row(details),"Check bridge setup (no model call)",()=>
            {
                health.Children.Clear();
                foreach(var check in service.CliHealth(row.Source.AgentId))
                    health.Children.Add(Note((check.Status=="pass"?"✓ ":check.Status=="warn"?"! ":"✗ ")+check.Message));
                return Task.CompletedTask;
            },false);
            details.Children.Add(health);
            details.Children.Add(Note("Session discovery reads this Agent's local conversation store. It never selects the latest conversation."));
            var current=service.DiscoverySource(row.Source.AgentId);
            var provider=new ComboBox{Name="SessionDiscoveryProvider",ItemsSource=new[]{"zcode","dsh","hermes","workbuddy"},SelectedItem=current?.Provider,MinHeight=38};
            details.Children.Add(provider);
            var storage=EntryDialogLayout.Input("SessionStorageRoot",current?.StorageRoot??"");details.Children.Add(storage);
            Add(Row(details),"Save session discovery",()=>
            {
                if(provider.SelectedItem is not string selected)throw new RelayException("session_provider_required",T("Select a session provider."));
                service.ConfigureSessionDiscovery(row.Source.AgentId,selected,storage.Text.Trim());return Task.CompletedTask;
            });
        }
        table.SelectionChanged += (_, _) => ShowDetails();
        table.SelectedItem = rows.SingleOrDefault(row => row.Source.AgentId == agentId) ?? rows.FirstOrDefault();
        return page;
    }

    private DataGridTemplateColumn BridgeColumn()
    {
        var row = new FrameworkElementFactory(typeof(StackPanel));
        row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        row.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        var dot = new FrameworkElementFactory(typeof(Ellipse));
        dot.SetValue(WidthProperty, 8.0); dot.SetValue(HeightProperty, 8.0);
        dot.SetValue(MarginProperty, new Thickness(10, 0, 8, 0)); dot.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        dot.SetBinding(Shape.FillProperty, new Binding(nameof(AgentRow.BridgeBrush)));
        var label = new FrameworkElementFactory(typeof(TextBlock));
        label.SetBinding(TextBlock.TextProperty, new Binding(nameof(AgentRow.Bridge)));
        label.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        row.AppendChild(dot); row.AppendChild(label);
        return new DataGridTemplateColumn { Header = T("Agent bridge"), CellTemplate = new DataTemplate { VisualTree = row },
            Width = new DataGridLength(1, DataGridLengthUnitType.Star) };
    }
}
