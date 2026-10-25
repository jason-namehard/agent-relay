using System.IO;
using System.Windows;
using System.Windows.Controls;
using AgentRelay.Core;

namespace AgentRelay.Desktop;

public sealed record AgentEntryDraft(string Name, string Host, string ConfigPath, string? CliPath,
    string? HostVersion, string[] InstructionPaths);

public sealed class AgentEntryDialog : Window
{
    public AgentEntryDraft? Result { get; private set; }

    public AgentEntryDialog(Window owner, string language, AgentRecord? existing = null, RelayService? service = null)
    {
        var ui = new EntryDialogLayout(this, owner, EntryText.Get(existing is null ? "Add Agent" : "Edit Agent", language), language);
        ui.Body.Children.Add(ui.Note("Name is required; all other information is optional."));
        var name = ui.Field(ui.Body, "Agent name", "AgentEntryName", existing?.DisplayName ?? existing?.AgentId ?? "");
        ui.Body.Children.Add(ui.Note("Choose any name. Roles are defined by you."));
        var host = ui.Field(ui.Body, "Host / application", "AgentEntryHost", existing?.Host ?? "");
        var advancedPanel = new StackPanel { Margin = new(10) };
        var advanced = new Expander { Header = ui.T("Advanced connection details"), Content = advancedPanel, Margin = new(0, 4, 0, 12) };
        ui.Body.Children.Add(advanced);
        advancedPanel.Children.Add(ui.Note("Manual handoff works without configuration paths. CLI capabilities are tested separately."));
        var config = ui.Field(advancedPanel, "Configuration path", "AgentEntryConfig", existing?.ConfigPath ?? "");
        var cli = ui.Field(advancedPanel, "CLI executable path", "AgentEntryCli", existing?.CliPath ?? "");
        var version = ui.Field(advancedPanel, "Host version", "AgentEntryVersion", existing?.HostVersion ?? "");
        var instructions = ui.Field(advancedPanel, "Instruction files (one per line)", "AgentEntryInstructions",
            existing is null ? "" : string.Join(Environment.NewLine, existing.InstructionPaths), true);
        var fields = new Dictionary<string, TextBox>
        {
            ["AgentName"] = name, ["AgentHost"] = host, ["AgentConfig"] = config,
            ["AgentCli"] = cli, ["AgentVersion"] = version, ["AgentInstructions"] = instructions
        };
        ui.Assistant(ui.Body, "Fill with an Agent", "Copy the prompt, send it to your Agent, then paste its complete reply here.", "AgentEntry",
            () => service is null ? SetupAssistant.CreatePrompt("agents", fields.ToDictionary(f => f.Key, f => f.Value.Text), language)
                : service.AgentSetupPrompt(existing?.AgentId, ResolveRelayCommand()), reply =>
            {
                if (reply.Contains("\"relay_setup_version\"", StringComparison.Ordinal))
                {
                    var values = SetupAssistant.Parse(reply, "agents", fields.Keys.ToArray());
                    if (values.Count == 0) throw new RelayException("setup_invalid_fields", "The setup response fields must contain at least one requested field.");
                    foreach (var (key, value) in values.Where(pair => !string.IsNullOrWhiteSpace(pair.Value))) fields[key].Text = value;
                }
                else
                {
                    var manifest = RelayService.ParseAgentSetupManifest(reply);
                    name.Text = manifest.Name; host.Text = manifest.Host; config.Text = manifest.ConfigPath ?? "";
                    cli.Text = manifest.CliPath ?? ""; version.Text = manifest.HostVersion ?? "";
                    instructions.Text = string.Join(Environment.NewLine, manifest.InstructionPaths ?? []);
                }
                advanced.IsExpanded = new[] { config.Text, cli.Text, version.Text, instructions.Text }.Any(value => !string.IsNullOrWhiteSpace(value));
                ui.Status.Text = ui.T("Reply parsed. Review the filled fields before saving.");
            });
        ui.Button(ui.Actions, "Save", "SaveAgentEntry", () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) throw new RelayException("agent_name_required", "Enter an Agent name.");
            var configPath = config.Text.Trim(); var cliPath = cli.Text.Trim();
            var instructionPaths = instructions.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if ((configPath.Length > 0 && !Path.IsPathFullyQualified(configPath)) ||
                (cliPath.Length > 0 && !Path.IsPathFullyQualified(cliPath)) || instructionPaths.Any(p => !Path.IsPathFullyQualified(p)))
                throw new RelayException("agent_path_required", "Use a full path for optional configuration and executable files.");
            Result = new(name.Text.Trim(), string.IsNullOrWhiteSpace(host.Text) ? "manual" : host.Text.Trim(), configPath,
                cliPath.Length == 0 ? null : cliPath, string.IsNullOrWhiteSpace(version.Text) ? null : version.Text.Trim(), instructionPaths);
            DialogResult = true;
        }, true);
        var cancel = ui.Button(ui.Actions, "Cancel", "CancelAgentEntry", () => DialogResult = false); cancel.IsCancel = true;
    }

    internal static string? ResolveRelayCommand()
    {
        static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
        var published = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "relay.exe"));
        if (File.Exists(published)) return "& " + Quote(published);
        // Source runs use an actual built CLI; never fabricate an executable path.
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (!File.Exists(Path.Combine(directory.FullName, "AgentRelay.slnx"))) continue;
            var cli = Path.Combine(directory.FullName, "src", "AgentRelay.Cli", "bin", "Release", "net10.0", "relay.dll");
            if (!File.Exists(cli)) return null;
            var runtime = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            return runtime is not null && File.Exists(runtime) ? "& " + Quote(runtime) + " " + Quote(cli) : "dotnet " + Quote(cli);
        }
        return null;
    }
}
