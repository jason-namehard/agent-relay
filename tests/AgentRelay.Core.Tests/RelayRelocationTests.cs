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
        Assert.AreEqual("relocation_no_pending", Assert.ThrowsExactly<RelayException>(() => service.VerifyRelocation()).Code);
    }

    [TestMethod]
    public async Task VerificationWithoutPendingMigrationDoesNotReportSuccessForActiveTasks()
    {
        var task = (await service.DispatchAsync(new("a", "b", "Still waiting", "manual", "manual", "pending"))).Task;
        Assert.AreEqual(TaskState.AwaitingManual, task.State);
        var error = Assert.ThrowsExactly<RelayException>(() => service.VerifyRelocation());
        Assert.AreEqual("relocation_no_pending", error.Code);
        StringAssert.Contains(error.Message, home);
        Assert.AreEqual("relocation_same_directory", Assert.ThrowsExactly<RelayException>(() => service.Relocate(home)).Code);
        Assert.AreEqual(TaskState.AwaitingManual, service.Status(task.TaskId).Task.State);
    }

    [TestMethod]
    public async Task CompletedHistoryExpiresOnlyInTheCurrentRelayAndPreservedBackupIsNotCleaned()
    {
        var clock = new RelocationClock();
        service = new RelayService(home, clock);
        var task = (await service.DispatchAsync(new("a", "b", "Archive this", "manual", "manual", "retention"))).Task;
        File.WriteAllText(task.ResultPath, "Done.");
        var completed = service.Complete(task.TaskId, task.AttemptId, "b", "completed", task.ResultPath).Task;
        var moved = service.Relocate(target);
        var relocated = moved.Status(task.TaskId).Task;
        clock.UtcNow = clock.UtcNow.AddDays(4);
        Assert.IsGreaterThan(0, moved.Cleanup(true).Deleted);
        Assert.IsFalse(File.Exists(relocated.RequestPath));
        Assert.IsFalse(File.Exists(relocated.ResultPath));
        Assert.IsTrue(File.Exists(completed.RequestPath));
        Assert.IsTrue(File.Exists(completed.ResultPath));
    }

    private sealed class RelocationClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    [TestMethod]
    public void ManualFileFollowupBlocksMigrationAndCompletedHistoryRetainsExternalSource()
    {
        var original = Path.Combine(root, "handoff.zip");
        File.WriteAllBytes(original, [1, 2, 3, 4]);
        var task = service.CreateFileHandoff(new("a", "b", original, "Review file")).Task;
        Assert.AreEqual("relocation_active_tasks", Assert.ThrowsExactly<RelayException>(() => service.Relocate(target)).Code);
        var reply = Path.Combine(root, "reply.md"); File.WriteAllText(reply, "Choose option 2.");
        File.WriteAllText(service.ManualReceiptPath(task), RelayJson.Write(new ManualReceipt(task.TaskId, task.AttemptId, "b", 0, "done", true, reply, "确认第二个选项")));
        Assert.IsEmpty(service.ImportManualReceipts().Errors);
        Assert.AreEqual(TaskState.AwaitingReply, service.Status(task.TaskId).Task.State);
        Assert.AreEqual("relocation_active_tasks", Assert.ThrowsExactly<RelayException>(() => service.Relocate(target)).Code);
        File.WriteAllText(service.ManualReceiptPath(task), RelayJson.Write(new ManualReceipt(task.TaskId, task.AttemptId, "a", 1, "done", false)));
        Assert.IsEmpty(service.ImportManualReceipts().Errors);

        var moved = service.Relocate(target);
        var history = moved.Status(task.TaskId).Task;
        Assert.AreEqual(TaskState.Succeeded, history.State);
        Assert.AreEqual(original, history.ManualSourceFile);
        Assert.IsTrue(history.ManualCurrentFile!.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        Assert.IsTrue(File.Exists(history.ManualCurrentFile));
        Assert.IsTrue(File.Exists(original));
        Assert.IsTrue(File.Exists(reply));
    }
}
