using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentRelay.Core;

public sealed record CliCommand(string Executable, string[] Arguments, string? Input, string? WorkingDirectory);
public sealed record CliRecipe(int Version, string Name, string Host, CliCommand Create, CliCommand? Resume,
    string[] Dependencies, string SupportedVersions, string Source, string License, string Limitations);
public sealed record CliProbe(string ProbeId, string Operation, string Marker, string? SessionId,
    bool MachinePassed, bool UserConfirmed, string? Error, DateTimeOffset TestedAt);
public sealed record CliExtensionRecord(string AgentId, string Revision, CliRecipe Recipe,
    CliProbe? CreateProbe, CliProbe? ResumeProbe)
{
    [JsonIgnore] public bool CanCreate => CreateProbe is { MachinePassed: true, UserConfirmed: true };
    [JsonIgnore] public bool CanResume => CanCreate && Recipe.Resume is not null &&
        ResumeProbe is { MachinePassed: true, UserConfirmed: true } && ResumeProbe.SessionId == CreateProbe!.SessionId;
}

public static class CliRecipes
{
    public const string Notice = "CLI 接口扩充用于接入经验证的第三方命令行或桥接方案。可用性取决于目标软件开放的接口、版本、运行环境及会话管理能力，并非所有 Agent 均支持自动调用或原会话续接。扩充方案须完成检查与测试后方可启用；未接通时仍可使用手动交接。";

    public static CliRecipe Parse(string json)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 64 * 1024)
            throw new RelayException("cli_recipe_invalid", "调用方案不能超过 64 KB。");
        CliRecipe recipe;
        try
        {
            recipe = JsonSerializer.Deserialize<CliRecipe>(json, new JsonSerializerOptions(RelayJson.Options)
            {
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                RespectRequiredConstructorParameters = true
            }) ?? throw new JsonException("Expected an object.");
        }
        catch (JsonException ex)
        {
            throw new RelayException("cli_recipe_invalid", "调用方案 JSON 格式不正确：" + ex.Message);
        }
        // Imported recipes are external input. Validate the invocation contract before storing it.
        if (recipe.Version != 1 || string.IsNullOrWhiteSpace(recipe.Name) || string.IsNullOrWhiteSpace(recipe.Host) ||
            recipe.Create is null || recipe.Dependencies is null || recipe.Dependencies.Any(x => x is null) ||
            recipe.SupportedVersions is null || recipe.Source is null || recipe.License is null || recipe.Limitations is null)
            throw new RelayException("cli_recipe_invalid", "方案需要 version=1、名称、宿主、create、dependencies 和来源/版本/许可证/限制说明。未知说明请填空字符串。");
        ValidateCommand(recipe.Create, false);
        if (recipe.Resume is not null) ValidateCommand(recipe.Resume, true);
        return recipe;
    }

    private static void ValidateCommand(CliCommand command, bool resume)
    {
        if (string.IsNullOrWhiteSpace(command.Executable) || command.Arguments is null || command.Arguments.Any(a => a is null) ||
            (command.WorkingDirectory is not null && !Path.IsPathFullyQualified(command.WorkingDirectory)))
            throw new RelayException("cli_recipe_invalid", "命令需要 executable、arguments 字符串数组；working_directory 应为绝对路径或 null。");
        var templates = command.Arguments.Append(command.Input ?? "").ToArray();
        if (!templates.Any(t => t.Contains("{prompt}", StringComparison.Ordinal)))
            throw new RelayException("cli_recipe_invalid", "每条命令必须在 arguments 或 input 中包含 {prompt}。");
        if (resume && !templates.Any(t => t.Contains("{session_id}", StringComparison.Ordinal)))
            throw new RelayException("cli_recipe_invalid", "续接命令必须包含 {session_id}，不能以创建新会话代替续接。");
        if (!resume && templates.Any(t => t.Contains("{session_id}", StringComparison.Ordinal)))
            throw new RelayException("cli_recipe_invalid", "新会话命令不能引用 {session_id}。");
    }

    public static string CreatePrompt(string host) => $$"""
        请为宿主 {{host}} 提供你已核实可用的 CLI 或本机桥接调用方案，仅返回下列 JSON 格式。
        不执行命令，不安装桥接，不修改软件，不读取或返回密钥。无法确认可行方案时请直接说明不能提供，不编造命令。
        executable 是本机可执行文件或 PATH 中的命令；arguments 每项是一个参数，不拼接 shell 命令。
        {prompt} 由 Relay 替换为文本；{session_id} 仅用于 resume；{relay_home} 可用于参数或 input。
        input 为标准输入模板或 null；working_directory 为绝对路径或 null。
        调用必须将请求交给真实 Agent 会话。stdout 必须仅返回一个 JSON 对象：
        {"session_id":"真实会话标识","reply":"Agent 的真实回复","accepted":true}
        探针要求真实回复准确返回短标识；不得由桥接器自行生成回复。交接仅以 accepted=true 表示已提交，不表示任务完成。
        如原生命令不能输出此协议，说明需要的独立桥接程序，提供已存在且可用的程序路径；不要把尚未实现的方案伪装成可用。
        create 必须创建一个用户可查看的会话。resume 必须续接传入的原会话，无法续接时填 null。
        dependencies、supported_versions、source、license、limitations 如实说明；不要假定所有宿主都可自动调用。
        {
          "version":1,"name":"方案名称","host":{{JsonSerializer.Serialize(host)}},
          "create":{"executable":"本机程序路径","arguments":["{prompt}"],"input":null,"working_directory":null},
          "resume":null,"dependencies":[],"supported_versions":"","source":"","license":"","limitations":""
        }
        """;

    internal static Task<ProcessResult> InvokeAsync(CliCommand command, string prompt, string? sessionId,
        string home, TimeSpan timeout, CancellationToken cancellationToken)
    {
        string Expand(string template) => template.Replace("{session_id}", sessionId ?? "", StringComparison.Ordinal)
            .Replace("{relay_home}", home, StringComparison.Ordinal).Replace("{prompt}", prompt, StringComparison.Ordinal);
        return RelayProcess.RunAsync(command.Executable, command.Arguments.Select(Expand),
            command.Input is null ? null : Expand(command.Input), timeout, cancellationToken,
            workingDirectory: command.WorkingDirectory);
    }

    internal static (string SessionId, string Reply)? Receipt(ProcessResult process)
    {
        if (process.TimedOut || process.ExitCode != 0) return null;
        try
        {
            using var doc = JsonDocument.Parse(process.Output);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("accepted", out var accepted) || accepted.ValueKind != JsonValueKind.True ||
                !root.TryGetProperty("session_id", out var session) || session.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(session.GetString()) ||
                !root.TryGetProperty("reply", out var reply) || reply.ValueKind != JsonValueKind.String) return null;
            return (session.GetString()!, reply.GetString()!);
        }
        catch (JsonException) { return null; } // External CLI output cannot establish a receipt when malformed.
    }
}

public sealed partial class RelayService
{
    public CliExtensionRecord? CliExtension(string agentId) => store.Read(s => ReadCliExtension(s, agentId));
    public List<CliExtensionRecord> CliExtensions() => store.Read(s => s.Query(
        "SELECT json FROM cli_extensions ORDER BY agent_id", r => RelayJson.Read<CliExtensionRecord>(r.GetString(0))));
    public string CliRecipePrompt(string agentId) => CliRecipes.CreatePrompt(store.Read(s => Agent(s, agentId).Host));
    public string PreferredDeliveryAdapter(string recipientId) => CliExtension(recipientId) is { CanCreate: true }
        ? "cli:" + recipientId : "amq-cli";

    public CliExtensionRecord ImportCliRecipe(string agentId, string json)
    {
        var recipe = CliRecipes.Parse(json);
        return store.Write(s =>
        {
            var agent = Agent(s, agentId);
            if (!string.Equals(recipe.Host, agent.Host, StringComparison.OrdinalIgnoreCase))
                throw new RelayException("cli_recipe_host_mismatch", "调用方案的 host 与选中的 Agent 宿主不一致。");
            var record = new CliExtensionRecord(agentId, Guid.NewGuid().ToString(), recipe, null, null);
            SaveCliExtension(s, record);
            return record;
        });
    }

    public void RemoveCliExtension(string agentId) => store.Write(s =>
        s.Execute("DELETE FROM cli_extensions WHERE agent_id=$id", ("$id", agentId)));

    public void InvalidateCliExtension(string agentId) => store.Write(s =>
    {
        InvalidateCliExtension(s, agentId);
        return true;
    });

    private static void InvalidateCliExtension(StoreSession s, string agentId)
    {
        if (ReadCliExtension(s, agentId) is { } record)
            SaveCliExtension(s, record with { Revision = Guid.NewGuid().ToString(), CreateProbe = null, ResumeProbe = null });
    }

    public async Task<CliProbe> RunCliProbeAsync(string agentId, string operation, CancellationToken cancellationToken = default)
    {
        if (operation is not ("create" or "resume")) throw new RelayException("cli_probe_operation", "探针类型应为 create 或 resume。");
        var record = CliExtension(agentId) ?? throw new RelayException("cli_recipe_missing", "请先导入 CLI 调用方案。");
        if (operation == "resume" && (!record.CanCreate || record.Recipe.Resume is null))
            throw new RelayException("cli_probe_not_ready", "请先完成新会话探针并确认可见；方案也需要提供续接命令。");
        var marker = "RELAY-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        var sessionId = operation == "resume" ? record.CreateProbe!.SessionId : null;
        var command = operation == "create" ? record.Recipe.Create : record.Recipe.Resume!;
        var prompt = "Reply with exactly " + marker + ". Do not use tools or inspect files.";
        bool passed;
        string? error;
        try
        {
            var process = await CliRecipes.InvokeAsync(command, prompt, sessionId, Home,
                TimeSpan.FromSeconds(Settings().DeliveryTimeoutSeconds), cancellationToken);
            var receipt = CliRecipes.Receipt(process);
            passed = receipt is { } r && r.Reply.Trim() == marker && (operation == "create" || r.SessionId == sessionId);
            if (receipt is { } received) sessionId = received.SessionId;
            error = passed ? null : process.TimedOut ? "探针超时或已取消，可能已经创建会话，请到目标软件检查。" :
                process.ExitCode != 0 ? "CLI 返回错误：" + process.Error : "回执未包含正确标识和会话 ID；续接必须返回同一会话。";
        }
        catch (Win32Exception ex)
        {
            passed = false;
            error = "无法启动 CLI：" + ex.Message;
        }
        var probe = new CliProbe(Guid.NewGuid().ToString(), operation, marker, sessionId, passed, false, error, Now);
        return store.Write(s =>
        {
            var current = ReadCliExtension(s, agentId);
            if (current is null || current.Revision != record.Revision || current.CreateProbe != record.CreateProbe || current.ResumeProbe != record.ResumeProbe)
                throw new RelayException("cli_recipe_changed", "调用方案或探针已变化，本次结果不能用于启用。请重新测试。");
            SaveCliExtension(s, operation == "create" ? current with { CreateProbe = probe, ResumeProbe = null } : current with { ResumeProbe = probe });
            return probe;
        });
    }

    public CliExtensionRecord ConfirmCliProbe(string agentId, string probeId, bool visible) => store.Write(s =>
    {
        var record = ReadCliExtension(s, agentId) ?? throw new RelayException("cli_recipe_missing", "未找到调用方案。");
        var probe = record.CreateProbe?.ProbeId == probeId ? record.CreateProbe : record.ResumeProbe?.ProbeId == probeId ? record.ResumeProbe :
            throw new RelayException("cli_probe_missing", "未找到当前探针，请重新测试。");
        if (visible && !probe.MachinePassed) throw new RelayException("cli_probe_failed", "机器回执未通过，不能启用此能力。");
        var updated = probe with { UserConfirmed = visible };
        record = probe.Operation == "create" ? record with { CreateProbe = updated, ResumeProbe = visible ? record.ResumeProbe : null } : record with { ResumeProbe = updated };
        SaveCliExtension(s, record);
        return record;
    });

    private static CliExtensionRecord? ReadCliExtension(StoreSession s, string agentId) =>
        s.Json<CliExtensionRecord>("SELECT json FROM cli_extensions WHERE agent_id=$id", ("$id", agentId));
    private static void SaveCliExtension(StoreSession s, CliExtensionRecord record) => s.Execute(
        "INSERT INTO cli_extensions VALUES($id,$json) ON CONFLICT(agent_id) DO UPDATE SET json=excluded.json",
        ("$id", record.AgentId), ("$json", RelayJson.Write(record)));
}

public sealed class CliRecipeDeliveryAdapter(CliExtensionRecord extension) : IDeliveryAdapter
{
    public string Id => "cli:" + extension.AgentId;

    public async Task<DeliveryResult> DeliverAsync(TaskRecord task, string home, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!extension.CanCreate || task.RecipientId != extension.AgentId)
            return new("rejected", null, "接收 Agent 的 CLI 新会话能力未验证。");
        var prompt = $"""
            Agent Relay handoff from {task.RequesterId} to {task.RecipientId}.
            Relay home: {home}
            Task ID: {task.TaskId}
            Attempt ID: {task.AttemptId}
            Read the request file: {task.RequestPath}
            Write the reply file: {task.ResultPath}
            Report received/started/completed/failed explicitly through Relay CLI or MCP; changing files alone does not complete the handoff.
            CLI callbacks (relay is shipped with Agent Relay):
            relay --home "{home}" task ack --task {task.TaskId} --attempt {task.AttemptId} --sender {task.RecipientId} --idempotency-key received
            relay --home "{home}" task complete --task {task.TaskId} --attempt {task.AttemptId} --sender {task.RecipientId} --idempotency-key completed --status succeeded --result "{task.ResultPath}"
            If Relay CLI/MCP is unavailable, return the reply path to the operator for manual confirmation. Save permanent deliverables in the project, not the relay directory.
            """;
        var process = await CliRecipes.InvokeAsync(extension.Recipe.Create, prompt, null, home, timeout, cancellationToken);
        return CliRecipes.Receipt(process) is not null
            ? new("accepted", process.Output, null)
            : new("unknown", null, process.TimedOut ? "CLI 交接超时，可能已经提交，请检查目标会话。" : "CLI 未返回可识别的提交回执：" + process.Error);
    }
}
