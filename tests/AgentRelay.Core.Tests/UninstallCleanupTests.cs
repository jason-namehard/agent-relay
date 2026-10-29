using AgentRelay.Core;

namespace AgentRelay.Core.Tests;

[TestClass]
public sealed class UninstallCleanupTests
{
    private string root = null!, home = null!, location = null!, instructions = null!, stable = null!;
    private RelayService relay = null!;

    [TestInitialize]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "AgentRelay.Tests", Guid.NewGuid().ToString());
        home = Path.Combine(root, "自选中转站"); location = Path.Combine(root, "relay.location.json");
        Directory.CreateDirectory(root);
        File.WriteAllText(location, RelayJson.Write(new { home }));
        relay = new RelayService(home); relay.Initialize(); relay.RegisterManual("agent-a", "manual", "A");
        instructions = Path.Combine(root, "AGENTS.md"); File.WriteAllText(instructions, "# User instructions\nKeep this.\n");
        relay.InstallGuidance("agent-a", instructions); stable = relay.StableInstructionPath("agent-a");
        Directory.CreateDirectory(Path.Combine(home, "workspace"));
        File.WriteAllText(Path.Combine(home, "workspace", "user-file.md"), "Keep this file");
        File.WriteAllText(Path.Combine(home, "desktop.preferences.json"), "{}");
        File.WriteAllText(Path.Combine(home, "user-file.txt"), "Keep this too");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (File.Exists(stable)) File.Delete(stable);
        if (Directory.Exists(Path.GetDirectoryName(stable))) Directory.Delete(Path.GetDirectoryName(stable)!);
        Directory.Delete(root, true);
    }

    [TestMethod]
    public void ConfirmedDataCleanupRemovesRulesAndLedgerButPreservesUserFiles()
    {
        Assert.AreEqual(home, UninstallCleanup.ResolveHome(location, null, root));
        Assert.AreEqual(1, UninstallCleanup.Apply(location, null, root, home, false, true));
        Assert.AreEqual("# User instructions\nKeep this.\n", File.ReadAllText(instructions));
        Assert.IsFalse(File.Exists(stable)); Assert.IsFalse(File.Exists(Path.Combine(home, "relay.db")));
        Assert.IsFalse(File.Exists(Path.Combine(home, "desktop.preferences.json"))); Assert.IsFalse(File.Exists(location));
        Assert.AreEqual("Keep this file", File.ReadAllText(Path.Combine(home, "workspace", "user-file.md")));
        Assert.AreEqual("Keep this too", File.ReadAllText(Path.Combine(home, "user-file.txt")));
    }

    [TestMethod]
    public void KeepDataCanRemoveOnlyRules()
    {
        Assert.AreEqual(1, UninstallCleanup.Apply(location, null, root, home, true, false));
        Assert.IsTrue(File.Exists(Path.Combine(home, "relay.db"))); Assert.IsTrue(File.Exists(location));
        Assert.IsTrue(File.Exists(Path.Combine(home, "desktop.preferences.json")));
        Assert.AreEqual("# User instructions\nKeep this.\n", File.ReadAllText(instructions));
    }

    [TestMethod]
    public void DamagedRulePreventsDatabaseRemoval()
    {
        File.WriteAllText(instructions, File.ReadAllText(instructions).Replace(":end -->", ":broken -->"));
        Assert.ThrowsExactly<RelayException>(() => UninstallCleanup.Apply(location, null, root, home, true, true));
        Assert.IsTrue(File.Exists(Path.Combine(home, "relay.db"))); Assert.IsTrue(File.Exists(location));
    }

    [TestMethod]
    public void ChangedLocationDoesNotDeleteAnyData()
    {
        Assert.ThrowsExactly<RelayException>(() => UninstallCleanup.Apply(location, null, root, Path.Combine(root, "other"), true, true));
        Assert.IsTrue(File.Exists(Path.Combine(home, "relay.db"))); Assert.IsTrue(File.Exists(location));
    }
}
