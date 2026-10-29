namespace AgentRelay.Core;

/// <summary>Uninstall only removes Relay's marked rules and explicitly named data files.</summary>
public static class UninstallCleanup
{
    private sealed record Location(string Home);

    public static string ResolveHome(string locationFile, string? overrideHome)
    {
        var home = overrideHome;
        if (home is null && File.Exists(locationFile)) home = RelayJson.Read<Location>(File.ReadAllText(locationFile)).Home;
        if (home is null)
            throw new RelayException("uninstall_location_missing", "找不到中转站位置记录；为保护数据，未清理任何内容。请重新打开软件确认站点位置后再试。", 3);
        if (string.IsNullOrWhiteSpace(home) || !Path.IsPathFullyQualified(home))
            throw new RelayException("invalid_relay_location", "中转站位置无效；为保护数据，未清理任何内容。");
        return Path.GetFullPath(home);
    }

    public static int Apply(string locationFile, string? overrideHome, string expectedHome,
        bool removeRules, bool removeData)
    {
        var home = ResolveHome(locationFile, overrideHome);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(home, Path.GetFullPath(expectedHome), comparison))
            throw new RelayException("uninstall_location_changed", "中转站位置已经改变；未清理数据，请重新卸载并确认。");

        var removeLocation = removeData && File.Exists(locationFile) &&
            string.Equals(ResolveHome(locationFile, null), home, comparison);

        // The guidance state is the only record of inserted rules, so remove rules before deleting it.
        var removed = removeRules || removeData ? new RelayService(home).RemoveAllGuidance() : 0;
        if (removeData)
        {
            // Delete the database before its WAL; if the database is locked, its recoverable WAL stays intact.
            foreach (var name in new[] { "relay.db", "relay.db-wal", "relay.db-shm", "desktop.preferences.json", "agent-guidance.json" })
                File.Delete(Path.Combine(home, name));
            // A RELAY_HOME override does not necessarily match the saved location. Preserve a different location.
            if (removeLocation) File.Delete(locationFile);
        }
        return removed;
    }
}
