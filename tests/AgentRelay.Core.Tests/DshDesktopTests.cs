using System.Security.Cryptography;
using System.Text;
using AgentRelay.Core;

namespace AgentRelay.Core.Tests;

[TestClass]
public sealed class DshDesktopTests
{
    private string root = null!;

    [TestInitialize]
    public void Setup() => root = Path.Combine(Path.GetTempPath(), "AgentRelay.Tests", Guid.NewGuid().ToString());

    [TestCleanup]
    public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private string Credentials(string secret)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, ".credentials.yaml");
        File.WriteAllText(path, $"""
            version: 1
            refs:
              SOME_API_KEY: not-read
            records:
              client-connection/browser-session:
                kind: grant
                payload:
                  version: 1
                  secret: {secret}
              other/record:
                kind: grant
                payload:
                  secret: not-this-one
            """);
        return path;
    }

    [TestMethod]
    public void OnlyTheBrowserSessionSecretIsReadFromTheCredentialFile()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        CollectionAssert.AreEqual(secret, DshDesktopClient.BrowserSessionSecret(Credentials(Base64Url(secret))));
        Assert.IsNotNull(DshDesktopClient.SecretProblem(root.Replace("AgentRelay.Tests", "missing")));
        Credentials("too-short");
        Assert.IsNotNull(DshDesktopClient.SecretProblem(root));
    }

    [TestMethod]
    public void TheCookieIsTheDesktopsOwnV1BrowserCookieForThatPort()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        var now = DateTimeOffset.FromUnixTimeMilliseconds(1793082855509);
        var cookie = DshDesktopClient.Cookie(5643, secret, now);
        var (name, value) = (cookie[..cookie.IndexOf('=')], cookie[(cookie.IndexOf('=') + 1)..]);
        Assert.AreEqual("dsh-auth-" + Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes("127.0.0.1:5643"))), name);
        var parts = value.Split('.');
        Assert.AreEqual("v1", parts[0]);
        var body = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1].Replace('-', '+').Replace('_', '/') + new string('=', (4 - parts[1].Length % 4) % 4)));
        // Field order matters: DSH signs the base64url text of exactly this JSON.
        Assert.AreEqual("""{"version":1,"authority":"127.0.0.1:5643","issuedAt":1793082855509,"expiresAt":1793086455509}""", body);
        Assert.AreEqual(Base64Url(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(parts[1]))), parts[2]);
    }

    [TestMethod]
    public void DesktopRecipesNameTheAppAndPassPromptsOverItsApi()
    {
        var service = new RelayService(Path.Combine(root, "relay")); service.Initialize();
        service.RegisterManual("dsh", "DeepSeek Harness", "DSH");
        var command = new CliCommand(@"D:\DeepSeekHarness\DeepSeek Harness.exe", [], null, @"E:\DSHspace", new() { ["DSH_HOME"] = root });
        var recipe = new CliRecipe(1, "DSH desktop", "DeepSeek Harness", command, command, [], "", "", "", "", "dsh-desktop");
        service.ImportCliRecipe("dsh", RelayJson.Write(recipe));
        Assert.AreEqual("desktop", service.Bridge("dsh").Kind);
        Assert.AreEqual("dsh", service.DiscoverySource("dsh")!.Provider);
        Assert.AreEqual("fail", service.CliHealth("dsh").Single(c => c.Id == "host.desktop.auth").Status);
        Assert.ThrowsExactly<RelayException>(() => service.ImportCliRecipe("dsh",
            RelayJson.Write(recipe with { Create = command with { Arguments = ["{prompt}"] } })));
    }
}
