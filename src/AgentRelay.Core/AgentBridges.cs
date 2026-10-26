namespace AgentRelay.Core;

/// <param name="Kind">acp, cli, or none when only manual copy works.</param>
/// <param name="State">verified, unverified, blocked or manual.</param>
public sealed record AgentBridge(string AgentId, string Kind, string State, bool CanCreate, bool CanResume, string? BlockMessage);

public sealed partial class RelayService
{
    /// <summary>How turns reach this Agent: the registered bridge and how far it has been proven.</summary>
    public AgentBridge Bridge(string agentId)
    {
        if (CliExtension(agentId) is not { } extension) return new(agentId, "none", "manual", false, false, null);
        var kind = CliRecipes.OutputFormatFor(extension.Recipe) == "acp" ? "acp" : "cli";
        var state = extension.Block is not null ? "blocked" : extension.CanCreate || extension.CanResume ? "verified" : "unverified";
        return new(agentId, kind, state, extension.CanCreate, extension.CanResume, extension.Block?.Message);
    }
}
