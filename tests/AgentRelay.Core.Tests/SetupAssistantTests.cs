using System.Text;
using System.Text.Json;
using AgentRelay.Core;

namespace AgentRelay.Core.Tests;

[TestClass]
public sealed class SetupAssistantTests
{
    private const string Response = """
        {"relay_setup_version":1,"context":"agents","fields":{"AgentId":"agent-a","AgentHost":"codex"}}
        """;
    private static readonly string[] AllowedFields = ["AgentId", "AgentHost", "AgentConfigPath"];

    [TestMethod]
    public void ParseReturnsOnlyProvidedRequestedFields()
    {
        var fields = SetupAssistant.Parse(Response, "agents", AllowedFields);
        Assert.HasCount(2, fields);
        Assert.AreEqual("agent-a", fields["AgentId"]);
        Assert.AreEqual("codex", fields["AgentHost"]);
        Assert.IsFalse(fields.ContainsKey("AgentConfigPath"));
    }

    [TestMethod]
    public void ParsePreservesQuotedPathsWhitespaceAndEmptyStrings()
    {
        const string path = "  \"C:\\Users\\某人\\Agent Config\\settings.json\"  ";
        var json = RelayJson.Write(new { relay_setup_version = 1, context = "agents", fields = new Dictionary<string, string>
        {
            ["AgentConfigPath"] = path, ["AgentId"] = ""
        } });
        var fields = SetupAssistant.Parse(json, "agents", AllowedFields);
        Assert.AreEqual(path, fields["AgentConfigPath"]);
        Assert.AreEqual("", fields["AgentId"]);
    }

    [TestMethod]
    [DataRow("\n")]
    [DataRow("\r\n")]
    public void ParseAcceptsOneCompleteJsonFence(string newline)
    {
        var fields = SetupAssistant.Parse($"  \n```json{newline}{Response}{newline}```\n ", "agents", AllowedFields);
        Assert.AreEqual("agent-a", fields["AgentId"]);
    }

    [TestMethod]
    [DataRow("```json\n{}", "setup_invalid_fence")]
    [DataRow("```\n{}\n```", "setup_invalid_fence")]
    [DataRow("```JSON\n{}\n```", "setup_invalid_fence")]
    [DataRow("```json {} ```", "setup_invalid_fence")]
    [DataRow("Here is the response: {}", "setup_invalid_json")]
    [DataRow("{} trailing prose", "setup_invalid_json")]
    [DataRow("{} {}", "setup_invalid_json")]
    [DataRow("```json\n{}\n```\n```json\n{}\n```", "setup_invalid_json")]
    [DataRow("{}\n```json\n{}\n```", "setup_invalid_json")]
    [DataRow("{", "setup_invalid_json")]
    [DataRow("", "setup_invalid_json")]
    [DataRow("[]", "setup_invalid_response")]
    [DataRow("{}", "setup_invalid_response")]
    public void ParseRejectsIncompleteOrMixedResponses(string response, string code) => AssertError(response, code);

    [TestMethod]
    [DataRow("{\"relay_setup_version\":1,\"context\":\"agents\",\"fields\":{},\"extra\":true}", "setup_unknown_property")]
    [DataRow("{\"relay_setup_version\":2,\"context\":\"agents\",\"fields\":{}}", "setup_invalid_version")]
    [DataRow("{\"relay_setup_version\":\"1\",\"context\":\"agents\",\"fields\":{}}", "setup_invalid_version")]
    [DataRow("{\"relay_setup_version\":1.5,\"context\":\"agents\",\"fields\":{}}", "setup_invalid_version")]
    [DataRow("{\"relay_setup_version\":1,\"context\":\"settings\",\"fields\":{}}", "setup_context_mismatch")]
    [DataRow("{\"relay_setup_version\":1,\"context\":null,\"fields\":{}}", "setup_context_mismatch")]
    [DataRow("{\"relay_setup_version\":1,\"context\":\"agents\",\"fields\":[]}", "setup_invalid_fields")]
    [DataRow("{\"relay_setup_version\":1,\"context\":\"agents\",\"fields\":{\"Command\":\"anything\"}}", "setup_unknown_field")]
    [DataRow("{\"relay_setup_version\":1,\"context\":\"agents\",\"fields\":{\"agentid\":\"agent-a\"}}", "setup_unknown_field")]
    [DataRow("{\"relay_setup_version\":1,\"context\":\"agents\",\"fields\":{\"AgentId\":null}}", "setup_invalid_field_value")]
    [DataRow("{\"relay_setup_version\":1,\"context\":\"agents\",\"fields\":{\"AgentId\":123}}", "setup_invalid_field_value")]
    [DataRow("{\"relay_setup_version\":1,\"context\":\"agents\",\"fields\":{\"AgentId\":{}}}", "setup_invalid_field_value")]
    [DataRow("{\"relay_setup_version\":1,\"context\":\"agents\",\"fields\":{},\"context\":\"agents\"}", "setup_duplicate_property")]
    [DataRow("{\"relay_setup_version\":1,\"context\":\"agents\",\"fields\":{\"AgentId\":\"one\",\"AgentId\":\"two\"}}", "setup_duplicate_property")]
    [DataRow("{\"relay_setup_version\":1,\"context\":\"agents\",\"fields\":{\"AgentId\":\"one\",\"Agent\\u0049d\":\"two\"}}", "setup_duplicate_property")]
    public void ParseRejectsProtocolViolations(string response, string code) => AssertError(response, code);

    [TestMethod]
    public void ResponseLimitCountsUtf8Bytes()
    {
        var response = Response.Replace("agent-a", new string('界', SetupAssistant.MaxResponseBytes / 3));
        Assert.IsLessThan(SetupAssistant.MaxResponseBytes, response.Length);
        AssertError(response, "setup_response_too_large");
    }

    [TestMethod]
    public void ResponseAtByteLimitIsAccepted()
    {
        var response = Response + new string(' ', SetupAssistant.MaxResponseBytes - Encoding.UTF8.GetByteCount(Response));
        Assert.AreEqual("agent-a", SetupAssistant.Parse(response, "agents", AllowedFields)["AgentId"]);
        AssertError(response + " ", "setup_response_too_large");
    }

    [TestMethod]
    public void ParsedCommandTextRemainsInertData()
    {
        var path = Path.Combine(Path.GetTempPath(), $"relay-setup-{Guid.NewGuid():N}");
        var command = $"Set-Content -LiteralPath '{path}' -Value written";
        var json = RelayJson.Write(new { relay_setup_version = 1, context = "agents", fields = new Dictionary<string, string> { ["AgentConfigPath"] = command } });
        var fields = SetupAssistant.Parse(json, "agents", AllowedFields);
        Assert.AreEqual(command, fields["AgentConfigPath"]);
        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    [DataRow("en", "Verify local configuration")]
    [DataRow("zh-CN", "请只读核实")]
    [DataRow("zh-TW", "請唯讀查證")]
    [DataRow("ja", "読み取り専用")]
    [DataRow("ko", "읽기 전용")]
    [DataRow("es", "solo lectura")]
    public void PromptLocalizesIntroductionAndKeepsStableProtocol(string language, string introduction)
    {
        var prompt = SetupAssistant.CreatePrompt("agents", new Dictionary<string, string> { ["AgentId"] = "agent-a" }, language);
        StringAssert.Contains(prompt, introduction);
        StringAssert.Contains(prompt, "Do not read, request or output authentication data");
        StringAssert.Contains(prompt, "Do not change configuration");
        StringAssert.Contains(prompt, "call any model API");
        StringAssert.Contains(prompt, "not a login name");
        StringAssert.Contains(prompt, "not approval or permission");
        using var template = JsonDocument.Parse(prompt[(prompt.IndexOf("JSON template:", StringComparison.Ordinal) + "JSON template:".Length)..]);
        Assert.AreEqual(1, template.RootElement.GetProperty("relay_setup_version").GetInt32());
        Assert.AreEqual("agents", template.RootElement.GetProperty("context").GetString());
        Assert.AreEqual("agent-a", template.RootElement.GetProperty("fields").GetProperty("AgentId").GetString());
    }

    [TestMethod]
    [DataRow("agents")]
    [DataRow("connections")]
    [DataRow("adapters")]
    [DataRow("migrations")]
    [DataRow("settings")]
    [DataRow("audit")]
    public void AllSetupContextsRoundTrip(string context)
    {
        var response = RelayJson.Write(new { relay_setup_version = 1, context, fields = new Dictionary<string, string>() });
        Assert.IsEmpty(SetupAssistant.Parse(response, context, []));
        StringAssert.Contains(SetupAssistant.CreatePrompt(context, new Dictionary<string, string>(), "en"), $"context = \"{context}\"");
    }

    [TestMethod]
    public void UnsupportedContextIsReportedForBothOperations()
    {
        Assert.AreEqual("setup_invalid_context", Assert.ThrowsExactly<RelayException>(() => SetupAssistant.Parse(Response, "unknown", AllowedFields)).Code);
        Assert.AreEqual("setup_invalid_context", Assert.ThrowsExactly<RelayException>(() => SetupAssistant.CreatePrompt("unknown", new Dictionary<string, string>(), "en")).Code);
    }

    private static void AssertError(string response, string code) =>
        Assert.AreEqual(code, Assert.ThrowsExactly<RelayException>(() => SetupAssistant.Parse(response, "agents", AllowedFields)).Code);
}
