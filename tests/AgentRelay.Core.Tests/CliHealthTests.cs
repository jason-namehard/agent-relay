using AgentRelay.Core;

namespace AgentRelay.Core.Tests;

[TestClass]
public sealed class CliHealthTests
{
    private string root = null!;
    private RelayService service = null!;

    [TestInitialize]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "AgentRelay.Tests", Guid.NewGuid().ToString());
        service = new RelayService(Path.Combine(root, "relay")); service.Initialize();
        service.RegisterManual("agent-b", "fixture", "乙");
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(root, true);

    private string Status(string id) => service.CliHealth("agent-b").Single(c => c.Id == id).Status;
    private void Import(CliCommand create, string format, CliCommand? resume = null) => service.ImportCliRecipe("agent-b",
        RelayJson.Write(new CliRecipe(1, "fixture", "fixture", create, resume, [], "", "", "", "", format)));

    [TestMethod]
    public void MissingRecipeProgramAndFolderAreReportedWithoutRunningAnything()
    {
        Assert.AreEqual("fail", service.CliHealth("agent-b").Single().Status);
        Import(new(Path.Combine(root, "missing.exe"), ["{prompt}"], null, Path.Combine(root, "no-folder")), "text");
        Assert.AreEqual("fail", Status("create.executable"));
        Assert.AreEqual("fail", Status("create.cwd"));
        Assert.AreEqual("warn", Status("resume"));
        Assert.AreEqual("warn", Status("verified.create"));
    }

    [TestMethod]
    public void CodeBuddyRecipesNeedTheStandaloneCliAndANonInteractivePermissionFlag()
    {
        Import(new("pwsh", [@"D:\workbuddy\resources\app.asar.unpacked\cli\dist\codebuddy-lite-wb.mjs", "-p", "{prompt}", "--output-format", "json"], null, null), "claude-json");
        Assert.AreEqual("fail", Status("host.login"));
        Assert.AreEqual("fail", Status("host.permission"));
        Import(new("pwsh", ["codebuddy", "-p", "{prompt}", "--output-format", "json", "-y"], null, null), "claude-json");
        Assert.IsFalse(service.CliHealth("agent-b").Any(c => c.Id.StartsWith("host.", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ZcodeNeedsAnAccountPlanRegisteredForTheCommandLine()
    {
        var config = Path.Combine(root, "zcode", "provider_config.json"); Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        File.WriteAllText(config, """{"schemaVersion":1,"config":{"providerConfigRules":{},"modelConfigRules":{}}}""");
        File.WriteAllText(Path.Combine(root, "zcode", "credentials.json"), """{"oauth:zai:access_token":"secret"}""");
        Import(new("pwsh", ["zcode.cjs", "-p", "{prompt}", "--json"], null, null,
            new() { ["ZCODE_PERSONAL_PROVIDER_CONFIG_FILE"] = config }), "zcode-json");
        Assert.AreEqual("fail", Status("host.account"));
        Assert.AreEqual("warn", Status("host.model"));
        File.WriteAllText(Path.Combine(root, "zcode", "credentials.json"), """{"account-provider:account:zai-start-plan:identity":"x"}""");
        File.WriteAllText(config, """{"schemaVersion":1,"config":{"defaultModelSelection":{"providerId":"p","modelId":"m"}}}""");
        Assert.AreEqual("pass", Status("host.account"));
        Assert.AreEqual("pass", Status("host.model"));
    }

    [TestMethod]
    public void SetupPromptAsksBeforeInstallingBridgesAndPrefersAcp()
    {
        var prompt = service.AgentSetupPrompt();
        StringAssert.Contains(prompt, "Agent Client Protocol");
        StringAssert.Contains(prompt, "不要自行安装");
        StringAssert.Contains(prompt, "cli-recipe doctor");
    }
}
