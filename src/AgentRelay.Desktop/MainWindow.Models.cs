using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using AgentRelay.Core;

namespace AgentRelay.Desktop;

public partial class MainWindow
{
    private sealed record ModelChoice(AgentModelOption Option, string Name);

    /// <summary>Default model and reasoning level for conversations Relay starts; continued conversations keep their own.</summary>
    private void AddModelDefaults(Panel details, string agentId)
    {
        if (service.CliExtension(agentId) is null) return;
        var panel = new StackPanel();
        details.Children.Add(Note("New conversations Relay starts for this Agent use the model below. Continued conversations keep their own model and reasoning level."));
        Add(Row(details), "Default model for new conversations", async () =>
        {
            panel.Children.Clear();
            ShowModelDefaults(panel, await service.AgentModelsAsync(agentId));
        }, false);
        details.Children.Add(panel);
    }

    private void ShowModelDefaults(StackPanel panel, AgentModelCatalog catalog)
    {
        panel.Children.Clear();
        if (!catalog.Supported) { panel.Children.Add(Note(catalog.Message!)); return; }
        string Describe(AgentModelSelection? s) => s is null ? T("Not set") : s.ProviderId + " · " + s.ModelId + (s.ReasoningLevel is null ? "" : " · " + s.ReasoningLevel);
        panel.Children.Add(Note(T("Current default") + ": " + Describe(catalog.Current) + "\n" + T("Saved in the host's own configuration") + ": " + catalog.Source));
        var choices = catalog.Options.Where(o => o.Usable).Select(o => new ModelChoice(o, o.ProviderName + " · " + o.ModelId)).ToArray();
        var model = new ComboBox { Name = "AgentDefaultModel", ItemsSource = choices, DisplayMemberPath = nameof(ModelChoice.Name), MinHeight = 38, Margin = new(0, 4, 0, 6) };
        var level = new ComboBox { Name = "AgentDefaultReasoning", MinHeight = 38, Margin = new(0, 0, 0, 6) };
        AutomationProperties.SetAutomationId(model, model.Name); AutomationProperties.SetAutomationId(level, level.Name);
        model.SelectionChanged += (_, _) =>
        {
            var levels = (model.SelectedItem as ModelChoice)?.Option.ReasoningLevels ?? [];
            level.ItemsSource = levels; level.IsEnabled = levels.Length > 0;
            var keep = catalog.Current is { } c && model.SelectedItem is ModelChoice m && m.Option.ProviderId == c.ProviderId && m.Option.ModelId == c.ModelId
                ? c.ReasoningLevel : null;
            level.SelectedItem = keep is not null && levels.Contains(keep) ? keep : RelayService.MiddleReasoningLevel(levels);
        };
        model.SelectedItem = choices.FirstOrDefault(c => catalog.Current is { } s && c.Option.ProviderId == s.ProviderId && c.Option.ModelId == s.ModelId) ?? choices.FirstOrDefault();
        panel.Children.Add(new Label { Content = T("Model"), Target = model, Padding = new(0, 4, 0, 2) }); panel.Children.Add(model);
        panel.Children.Add(new Label { Content = T("Reasoning level"), Target = level, Padding = new(0, 4, 0, 2) }); panel.Children.Add(level);
        var save = Add(Row(panel), "Save default model", async () =>
        {
            if (model.SelectedItem is not ModelChoice chosen) throw new RelayException("agent_model_required", T("Select a model."));
            ShowModelDefaults(panel, await service.SetAgentModelAsync(catalog.AgentId,
                new(chosen.Option.ProviderId, chosen.Option.ModelId, level.SelectedItem as string)));
        }, false);
        AutomationProperties.SetAutomationId(save, "SaveAgentDefaultModel");
        if (catalog.Options.Any(o => !o.Usable))
            panel.Children.Add(Note("Account plans the host's command line has not signed in to are hidden; they work only inside the desktop window."));
    }
}
