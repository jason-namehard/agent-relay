using AgentRelay.Core;

namespace AgentRelay.Core.Tests;

[TestClass]
public sealed class CliExtensionTests
{
    private string root = null!;
    private RelayService service = null!;

    [TestInitialize]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "AgentRelay.Tests", Guid.NewGuid().ToString());
        service = new RelayService(Path.Combine(root, "relay"));
        service.Initialize();
        service.Register("agent-a", "fixture", Path.Combine(root, "absent-a.toml"));
        service.Register("agent-b", "fixture", Path.Combine(root, "absent-b.toml"));
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(root, true);

    private static CliCommand Command(string session = "fixture-session", bool resume = false, bool wrongReply = false) => new("pwsh",
        ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command",
            "[Console]::InputEncoding=[Text.UTF8Encoding]::new($false);[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false);" +
            "$p=[Console]::In.ReadToEnd();$m=[regex]::Match($p,'RELAY-[A-Z0-9]+').Value;" +
            "@{session_id='" + session + "';reply=" + (wrongReply ? "'wrong'" : "$m") + ";accepted=$true}|ConvertTo-Json -Compress"],
        resume ? "{session_id}\n{prompt}" : "{prompt}", null);

    private static CliRecipe Recipe(CliCommand? create = null, CliCommand? resume = null) =>
        new(1, "Local fixture", "fixture", create ?? Command(), resume, [], "fixture", "tests", "MIT", "Fixture only; no model requests.");

    private CliExtensionRecord Import(CliRecipe? recipe = null) => service.ImportCliRecipe("agent-b", RelayJson.Write(recipe ?? Recipe()));

    [TestMethod]
    public void ImportDoesNotLaunchAProcessAndIsNotVerified()
    {
        var recipe = Recipe() with { Create = Command() with { Executable = "not-an-installed-program" } };
        var imported = Import(recipe);
        Assert.IsFalse(imported.CanCreate);
        Assert.IsNull(imported.CreateProbe);
        Assert.AreEqual(imported.Revision, new RelayService(service.Home).CliExtension("agent-b")!.Revision);
        Assert.HasCount(1, service.CliExtensions());
    }

    [TestMethod]
    public void InvalidRecipeAndWrongHostAreRejectedBeforeSaving()
    {
        Assert.ThrowsExactly<RelayException>(() => service.ImportCliRecipe("agent-b", "{}"));
        Assert.ThrowsExactly<RelayException>(() => service.ImportCliRecipe("agent-b", "[]"));
        Assert.ThrowsExactly<RelayException>(() => Import(Recipe() with { Host = "another-host" }));
        Assert.ThrowsExactly<RelayException>(() => Import(Recipe() with { Create = Command() with { Input = null } }));
        Assert.ThrowsExactly<RelayException>(() => Import(Recipe(resume: Command())));
        Assert.ThrowsExactly<RelayException>(() => Import(Recipe() with { Create = Command() with { Arguments = null! } }));
        Assert.IsNull(service.CliExtension("agent-b"));
    }

    [TestMethod]
    public async Task ReceiptAndVisibleSessionConfirmationAreSeparateAndPersisted()
    {
        Import(Recipe(resume: Command(resume: true)));
        var created = await service.RunCliProbeAsync("agent-b", "create");
        Assert.IsTrue(created.MachinePassed, created.Error);
        Assert.IsFalse(created.UserConfirmed);
        Assert.IsFalse(service.CliExtension("agent-b")!.CanCreate);
        await Assert.ThrowsExactlyAsync<RelayException>(() => service.RunCliProbeAsync("agent-b", "resume"));
        Assert.IsTrue(service.ConfirmCliProbe("agent-b", created.ProbeId, true).CanCreate);
        var resumed = await service.RunCliProbeAsync("agent-b", "resume");
        Assert.IsTrue(resumed.MachinePassed, resumed.Error);
        Assert.AreEqual(created.SessionId, resumed.SessionId);
        Assert.AreNotEqual(created.Marker, resumed.Marker);
        Assert.IsFalse(service.CliExtension("agent-b")!.CanResume);
        service.ConfirmCliProbe("agent-b", resumed.ProbeId, true);
        Assert.IsTrue(new RelayService(service.Home).CliExtension("agent-b")!.CanResume);
        Assert.IsFalse(service.ConfirmCliProbe("agent-b", created.ProbeId, false).CanResume);
    }

    [TestMethod]
    public async Task ModifiedRecipeInvalidatesBothCapabilities()
    {
        Import();
        var probe = await service.RunCliProbeAsync("agent-b", "create");
        service.ConfirmCliProbe("agent-b", probe.ProbeId, true);
        var replacement = Import(Recipe() with { Name = "Updated recipe" });
        Assert.IsFalse(replacement.CanCreate);
        Assert.IsFalse(replacement.CanResume);
        Assert.ThrowsExactly<RelayException>(() => service.ConfirmCliProbe("agent-b", probe.ProbeId, true));
    }

    [TestMethod]
    public async Task ResumeThatCreatesAnotherSessionCannotPass()
    {
        Import(Recipe(resume: Command("other-session", resume: true)));
        var created = await service.RunCliProbeAsync("agent-b", "create");
        service.ConfirmCliProbe("agent-b", created.ProbeId, true);
        var resumed = await service.RunCliProbeAsync("agent-b", "resume");
        Assert.IsFalse(resumed.MachinePassed);
        Assert.ThrowsExactly<RelayException>(() => service.ConfirmCliProbe("agent-b", resumed.ProbeId, true));
        Assert.IsTrue(service.CliExtension("agent-b")!.CanCreate);
        Assert.IsFalse(service.CliExtension("agent-b")!.CanResume);
    }

    [TestMethod]
    public async Task IncorrectEchoAndMissingExecutableRemainFailed()
    {
        Import(Recipe(create: Command(wrongReply: true)));
        var incorrect = await service.RunCliProbeAsync("agent-b", "create");
        Assert.IsFalse(incorrect.MachinePassed);
        Assert.ThrowsExactly<RelayException>(() => service.ConfirmCliProbe("agent-b", incorrect.ProbeId, true));
        Import(Recipe(create: Command() with { Executable = "missing-relay-fixture-program" }));
        var missing = await service.RunCliProbeAsync("agent-b", "create");
        Assert.IsFalse(missing.MachinePassed);
        Assert.IsNotNull(missing.Error);
        Assert.AreEqual(missing.ProbeId, service.CliExtension("agent-b")!.CreateProbe!.ProbeId);
    }

    [TestMethod]
    public async Task VerifiedTargetReceivesAutomaticHandoffWithoutUnrelatedConfigPaths()
    {
        Import();
        var probe = await service.RunCliProbeAsync("agent-b", "create");
        service.ConfirmCliProbe("agent-b", probe.ProbeId, true);
        service.SetPermission("agent-a", "agent-b", "allow");
        var view = await service.DispatchAsync(new("agent-a", "agent-b", "检查此文件", "auto", "cli:agent-b", "delivery"));
        Assert.AreEqual(TaskState.Dispatched, view.Task.State);
        Assert.IsNotNull(service.DeliveryEvidence(view.Task.TaskId)!.Receipt);
        Assert.AreEqual("task.dispatched", service.Audit(view.Task.TaskId).Last().Event);
        Assert.IsFalse(service.Tasks().Any(t => t.Task.State == TaskState.Succeeded));
        service.SetPermission("agent-a", "agent-b", "deny");
        await Assert.ThrowsExactlyAsync<RelayException>(() => service.DispatchAsync(new("agent-a", "agent-b", "暂停后不可自动", "auto", "cli:agent-b", "paused")));
    }

    [TestMethod]
    public async Task ManualHandoffWorksWithoutCliPathsOrAutomaticRoutePermission()
    {
        service.SetPermission("agent-a", "agent-b", "deny");
        var view = await service.DispatchAsync(new("agent-a", "agent-b", "手动交接", "manual", "manual", "manual"));
        Assert.AreEqual(TaskState.AwaitingManual, view.Task.State);
        service.Acknowledge(view.Task.TaskId, view.Task.AttemptId, "agent-b", "received");
        File.WriteAllText(view.Task.ResultPath, "已完成");
        var completed = service.Complete(view.Task.TaskId, view.Task.AttemptId, "agent-b", "done", view.Task.ResultPath);
        Assert.AreEqual(TaskState.Succeeded, completed.Task.State);
    }

    [TestMethod]
    public async Task UnconfirmedRecipeFallsBackToManualAndWrongRecipientCannotRun()
    {
        Import();
        service.SetPermission("agent-a", "agent-b", "allow");
        var view = await service.DispatchAsync(new("agent-a", "agent-b", "待验证方案", "auto", "cli:agent-b", "unverified"));
        Assert.AreEqual(TaskState.AwaitingManual, view.Task.State);
        await Assert.ThrowsExactlyAsync<RelayException>(() => service.DispatchAsync(new("agent-a", "agent-b", "目标不匹配", "auto", "cli:agent-a", "wrong")));
    }

    [TestMethod]
    public async Task NewProbeRevokesEarlierConfirmationUntilTheNewMarkerIsConfirmed()
    {
        Import();
        var first = await service.RunCliProbeAsync("agent-b", "create");
        service.ConfirmCliProbe("agent-b", first.ProbeId, true);
        var second = await service.RunCliProbeAsync("agent-b", "create");
        Assert.IsTrue(second.MachinePassed);
        Assert.IsFalse(service.CliExtension("agent-b")!.CanCreate);
        Assert.ThrowsExactly<RelayException>(() => service.ConfirmCliProbe("agent-b", first.ProbeId, true));
    }
}
