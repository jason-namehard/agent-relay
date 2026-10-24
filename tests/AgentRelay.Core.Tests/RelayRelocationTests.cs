using AgentRelay.Core;

namespace AgentRelay.Core.Tests;

[TestClass]
public sealed class RelayRelocationTests
{
    private string root = null!;
    private string home = null!;
    private string target = null!;
    private RelayService service = null!;

    [TestInitialize]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "AgentRelay.RelocationTests", Guid.NewGuid().ToString());
        home = Path.Combine(root, "source");
        target = Path.Combine(root, "target");
        service = new RelayService(home);
        service.Initialize();
        var config = Path.Combine(home, "profiles", "agent.json");
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        File.WriteAllText(config, "{}");
        service.Register("a", "fixture", config);
        service.RegisterManual("b", "fixture", "My chosen name");
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(root, true);

    [TestMethod]
    public async Task ActiveTaskBlocksMigrationWithoutCreatingDestination()
    {
        await service.DispatchAsync(new("a", "b", "Review this", "manual", "manual", "active"));
        var error = Assert.ThrowsExactly<RelayException>(() => service.Relocate(target));
        Assert.AreEqual("relocation_active_tasks", error.Code);
        Assert.IsFalse(Directory.Exists(target));
        Assert.HasCount(2, service.Agents());
    }

    [TestMethod]
    public async Task CompletedTaskAndManagedPathsMoveWhileOriginalSourceIsRetained()
    {
        var request = new DispatchRequest("a", "b", "Review this", "manual", "manual", "completed");
        var task = (await service.DispatchAsync(request)).Task;
        File.WriteAllText(task.ResultPath, "## Result\nDone.\n## Verification\nChecked.");
        service.Complete(task.TaskId, task.AttemptId, "b", "done", task.ResultPath);

        var moved = service.Relocate(target);
        var relocated = moved.Status(task.TaskId).Task;
        Assert.AreEqual(target, moved.Home);
        Assert.IsTrue(relocated.RequestPath.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        Assert.IsTrue(File.Exists(relocated.RequestPath));
        Assert.IsTrue(File.Exists(relocated.ResultPath));
        Assert.IsTrue(File.Exists(task.RequestPath));
        Assert.AreEqual(Path.Combine(target, "profiles", "agent.json"), moved.Agents().Single(a => a.AgentId == "a").ConfigPath);
        Assert.AreEqual("My chosen name", moved.Agents().Single(a => a.AgentId == "b").DisplayName);
        Assert.AreEqual(relocated.ResultPath, (await moved.DispatchAsync(request)).Task.ResultPath);
        var error = Assert.ThrowsExactly<RelayException>(() => service.Settings());
        Assert.AreEqual("relocation_moved", error.Code);
        StringAssert.Contains(error.Message, target);
    }

    [TestMethod]
    public void FailedCopyKeepsSourceUsableAndVerificationRetriesCurrentSource()
    {
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "relay.relocation.incomplete.json"), RelayJson.Write(new RelayRelocationRequest(home, target)));
        // A file occupies a destination directory needed by the copy.
        var obstruction = Path.Combine(target, "profiles");
        File.WriteAllText(obstruction, "occupied");
        var error = Assert.ThrowsExactly<RelayException>(() => service.Relocate(target));
        Assert.AreEqual("relocation_failed", error.Code);
        Assert.HasCount(2, service.Agents());
        Assert.AreEqual("relocation_incomplete", Assert.ThrowsExactly<RelayException>(() => new RelayService(target).Initialize()).Code);

        File.Delete(obstruction);
        File.WriteAllText(Path.Combine(target, "obsolete.txt"), "left by previous copy");
        File.WriteAllText(Path.Combine(home, "new.txt"), "current source");
        var moved = service.VerifyRelocation();
        Assert.AreEqual(target, moved.Home);
        Assert.IsFalse(File.Exists(Path.Combine(target, "obsolete.txt")));
        Assert.AreEqual("current source", File.ReadAllText(Path.Combine(target, "new.txt")));
        Assert.HasCount(2, moved.Agents());
    }

    [TestMethod]
    public void NonemptyAndNestedDestinationsAreRejected()
    {
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "user.txt"), "keep");
        Assert.AreEqual("relocation_target_not_empty", Assert.ThrowsExactly<RelayException>(() => service.Relocate(target)).Code);
        Assert.AreEqual("keep", File.ReadAllText(Path.Combine(target, "user.txt")));
        Assert.AreEqual("relocation_target_invalid", Assert.ThrowsExactly<RelayException>(() => service.Relocate(Path.Combine(home, "nested"))).Code);
        Assert.AreSame(service, service.VerifyRelocation());
    }
}
