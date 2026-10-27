using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AgentRelay.Core;

/// <summary>
/// Runs a turn inside the running DSH desktop instead of a separate headless process. The desktop composes sessions under
/// an agent preset with its own plugins, so only a turn built by the desktop process sends the same tools and system
/// prompt as the user's own turns, and switching between them keeps the provider's prompt cache.
/// The desktop host serves a loopback HTTP API on a random port. Its browser cookie is an HMAC over a small JSON body,
/// signed with the secret the desktop keeps in DSH_HOME\.credentials.yaml; Relay signs one in memory for each turn, with the
/// user's consent, and never stores or prints the secret.
/// </summary>
public static partial class DshDesktopClient
{
    private static readonly TimeSpan Poll = TimeSpan.FromSeconds(1.5);

    /// <summary>Returns the turn as relay-json on stdout, like the ACP client.</summary>
    internal static async Task<ProcessResult> RunAsync(CliCommand command, string prompt, string? sessionId, string cwd,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            var dshHome = command.Environment?.GetValueOrDefault("DSH_HOME")
                ?? throw new DshDesktopException("调用方案缺少 DSH_HOME 环境变量，找不到 DSH 的数据目录。");
            using var host = await Connect(command.Executable, dshHome, deadline.Token);
            sessionId ??= (await host.Call("session/create", new JsonObject { ["cwd"] = cwd, ["agentPreset"] = "standard" }, deadline.Token))
                ?["sessionId"]?.GetValue<string>() ?? throw new DshDesktopException("DSH 没有返回新会话 ID。");
            var baseline = await host.LatestSeq(sessionId, deadline.Token);
            await host.Call("session/prompt", new JsonObject
            {
                ["requestId"] = Guid.NewGuid().ToString(), ["sessionId"] = sessionId, ["mode"] = "queue",
                ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = prompt }),
                // The desktop UI sends the browser's IANA zone; the same value keeps its time-context message identical.
                ["clientTimeZone"] = TimeZoneInfo.TryConvertWindowsIdToIanaId(TimeZoneInfo.Local.Id, out var zone) ? zone : TimeZoneInfo.Local.Id
            }, deadline.Token);
            var reply = await host.WaitForTurn(sessionId, baseline, deadline.Token);
            return new ProcessResult(0, RelayJson.Write(new { session_id = sessionId, reply, accepted = true }), "", false);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new ProcessResult(-1, "", "DSH desktop turn did not finish before the deadline.", true);
        }
        catch (Exception error) when (error is DshDesktopException or HttpRequestException or JsonException or IOException)
        {
            return new ProcessResult(1, "", error.Message, false);
        }
    }

    /// <summary>Per-request usage of one session; DSH counts inputTokens without the cached part.</summary>
    internal static async Task<List<CacheRequest>> UsageAsync(string executable, string dshHome, string sessionId, CancellationToken token)
    {
        try
        {
            using var host = await Connect(executable, dshHome, token);
            var events = await host.EventsAfter(sessionId, 0, await host.LatestSeq(sessionId, token), token);
            return [.. events.Where(e => e.Type == "assistant/message" && e.Data?["usage"] is JsonObject).Select(e =>
            {
                var usage = e.Data!["usage"]!;
                long Read(string name) => usage[name]?.GetValue<long>() ?? 0;
                return new CacheRequest(DateTimeOffset.FromUnixTimeMilliseconds(e.Time), e.Data?["model"]?.GetValue<string>(),
                    Read("inputTokens") + Read("cacheReadTokens") + Read("cacheWriteTokens"), Read("cacheReadTokens"));
            })];
        }
        catch (DshDesktopException error) { throw new RelayException("cache_usage_unreadable", error.Message); }
    }

    private static async Task<Host> Connect(string executable, string dshHome, CancellationToken token)
    {
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)).Select(p => p.Id).ToHashSet();
        if (processes.Count == 0) throw new DshDesktopException("DSH 桌面没有运行。请先打开 DSH，Relay 会在桌面里执行这一轮。");
        var secret = BrowserSessionSecret(Path.Combine(dshHome, ".credentials.yaml"));
        foreach (var port in await LoopbackPorts(processes, token))
        {
            var host = new Host(port, secret);
            if (await host.IsDesktopHost(token)) return host;
            host.Dispose();
        }
        throw new DshDesktopException("找到了 DSH 进程，但没有找到它的本机接口。DSH 可能还在启动，请稍后重试。");
    }

    /// <summary>Loopback listeners owned by the given processes, from netstat (the owning process is not exposed by .NET).</summary>
    private static async Task<IReadOnlyList<int>> LoopbackPorts(HashSet<int> processes, CancellationToken token)
    {
        var result = await RelayProcess.RunAsync("netstat", ["-ano", "-p", "TCP"], null, TimeSpan.FromSeconds(15), token);
        return [.. result.Output.Split('\n').Select(line => NetstatListener().Match(line))
            .Where(m => m.Success && processes.Contains(int.Parse(m.Groups["pid"].Value)))
            .Select(m => int.Parse(m.Groups["port"].Value)).Distinct()];
    }

    [GeneratedRegex(@"^\s*TCP\s+127\.0\.0\.1:(?<port>\d+)\s+\S+\s+LISTENING\s+(?<pid>\d+)")]
    private static partial Regex NetstatListener();

    /// <summary>Reads only records → client-connection/browser-session → payload → secret; nothing else is parsed or kept.</summary>
    public static byte[] BrowserSessionSecret(string path)
    {
        if (!File.Exists(path)) throw new DshDesktopException("找不到 DSH 的凭据文件 " + path + "。");
        var lines = File.ReadAllLines(path);
        var record = Array.FindIndex(lines, l => l.TrimEnd() == "  client-connection/browser-session:");
        for (var i = record + 1; record >= 0 && i < lines.Length && (lines[i].Length == 0 || lines[i].StartsWith("   ", StringComparison.Ordinal)); i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith("secret:", StringComparison.Ordinal)) continue;
            var value = line["secret:".Length..].Trim().Trim('"', '\'');
            var bytes = Base64UrlDecode(value);
            if (bytes is { Length: 32 }) return bytes;
            break;
        }
        throw new DshDesktopException("DSH 的凭据文件里没有可用的本机界面签名密钥，DSH 版本可能已改变登录方式。");
    }

    /// <summary>Why the desktop cannot be reached from Relay, or null when its signing secret is readable; for doctor.</summary>
    public static string? SecretProblem(string dshHome)
    {
        try { _ = BrowserSessionSecret(Path.Combine(dshHome, ".credentials.yaml")); return null; }
        catch (Exception error) when (error is DshDesktopException or IOException or UnauthorizedAccessException) { return error.Message; }
    }

    public static bool DesktopRunning(string executable) =>
        Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)).Length > 0;

    public static string Cookie(int port, byte[] secret, DateTimeOffset now)
    {
        var authority = "127.0.0.1:" + port;
        var issued = now.ToUnixTimeMilliseconds();
        var body = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { version = 1, authority, issuedAt = issued, expiresAt = issued + 3_600_000 })));
        var signature = Base64Url(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(body)));
        return "dsh-auth-" + Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(authority))) + "=v1." + body + "." + signature;
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[]? Base64UrlDecode(string value)
    {
        var text = value.Replace('-', '+').Replace('_', '/');
        try { return Convert.FromBase64String(text + new string('=', (4 - text.Length % 4) % 4)); }
        catch (FormatException) { return null; }
    }

    private sealed class Host(int port, byte[] secret) : IDisposable
    {
        private readonly HttpClient http = new() { BaseAddress = new Uri("http://127.0.0.1:" + port + "/"), Timeout = TimeSpan.FromSeconds(30) };

        public async Task<bool> IsDesktopHost(CancellationToken token)
        {
            try
            {
                using var response = await http.GetAsync("", token);
                return response.StatusCode == HttpStatusCode.Unauthorized &&
                    (await response.Content.ReadAsStringAsync(token)).Contains("dsh web authentication required", StringComparison.Ordinal);
            }
            catch (HttpRequestException) { return false; }
        }

        public async Task<JsonNode?> Call(string method, JsonObject request, CancellationToken token, string argument = "request")
        {
            var message = new JsonObject
            {
                ["type"] = "client-request", ["rpcId"] = Guid.NewGuid().ToString(), ["method"] = method,
                ["payload"] = new JsonObject { ["args"] = new JsonObject { [argument] = request } }
            };
            using var content = new StringContent(message.ToJsonString(), Encoding.UTF8, "application/json");
            using var call = new HttpRequestMessage(HttpMethod.Post, "api/" + method) { Content = content };
            call.Headers.Add("Cookie", Cookie(port, secret, DateTimeOffset.UtcNow));
            using var response = await http.SendAsync(call, token);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new DshDesktopException("DSH 桌面拒绝了 Relay 的登录（401）。DSH 可能更新了本机界面的登录方式。");
            response.EnsureSuccessStatusCode();
            var result = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))?["result"];
            if (result?["ok"]?.GetValue<bool>() != true)
                throw new DshDesktopException($"DSH {method} 失败：{result?["error"]?["code"]} {result?["error"]?["message"]}");
            return result["value"];
        }

        public async Task<long> LatestSeq(string sessionId, CancellationToken token) =>
            (await Call("session/projections", new JsonObject { ["sessionId"] = sessionId }, token))?["asOfSeq"]?.GetValue<long>() ?? 0;

        /// <summary>Waits for the first turn that starts after the baseline to end, and returns its last assistant text.</summary>
        public async Task<string> WaitForTurn(string sessionId, long baseline, CancellationToken token)
        {
            while (true)
            {
                await Task.Delay(Poll, token);
                var latest = await LatestSeq(sessionId, token);
                if (latest <= baseline) continue;
                var events = await EventsAfter(sessionId, baseline, latest, token);
                var start = events.FirstOrDefault(e => e.Type == "turn/start");
                if (start is null) continue;
                var turn = start.Data?["turn"]?.GetValue<long>();
                var end = events.FirstOrDefault(e => e.Type == "turn/end" && e.Data?["turn"]?.GetValue<long>() == turn);
                if (end is null) continue;
                var reason = end.Data?["reason"]?["kind"]?.GetValue<string>();
                if (reason != "completed") throw new DshDesktopException("DSH 这一轮没有正常结束：" + reason);
                return events.Where(e => e.Type == "assistant/message" && e.Data?["turn"]?.GetValue<long>() == turn)
                    .Select(e => string.Concat((e.Data?["message"]?["content"]?.AsArray() ?? [])
                        .Where(c => c?["type"]?.GetValue<string>() == "text").Select(c => c!["text"]!.GetValue<string>())))
                    .LastOrDefault(text => text.Length > 0) ?? "";
            }
        }

        public async Task<List<SessionEvent>> EventsAfter(string sessionId, long baseline, long through, CancellationToken token)
        {
            var events = new List<SessionEvent>();
            long? before = null;
            while (true)
            {
                var request = new JsonObject
                {
                    ["address"] = new JsonObject { ["kind"] = "session", ["sessionId"] = sessionId },
                    ["throughSeq"] = through, ["maxMessages"] = 200
                };
                if (before is not null) request["beforeSeq"] = before;
                var page = await Call("session/page", request, token);
                var records = (page?["records"]?.AsArray() ?? []).Select(r => r?["event"]).OfType<JsonNode>()
                    .Select(e => new SessionEvent(e["type"]?.GetValue<string>() ?? "", e["seq"]?.GetValue<long>() ?? 0,
                        (long)(e["time"]?.GetValue<double>() ?? 0), e["data"])).ToList();
                events.AddRange(records.Where(e => e.Seq > baseline));
                if (page?["hasMore"]?.GetValue<bool>() != true || records.Count == 0 || records.Min(e => e.Seq) <= baseline) break;
                before = records.Min(e => e.Seq);
            }
            return [.. events.OrderBy(e => e.Seq)];
        }

        public void Dispose() => http.Dispose();
    }

    private sealed record SessionEvent(string Type, long Seq, long Time, JsonNode? Data);

    private sealed class DshDesktopException(string message) : Exception(message);
}
