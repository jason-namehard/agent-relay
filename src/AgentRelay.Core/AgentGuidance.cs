using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentRelay.Core;

public sealed record GuidanceRegistration(string AgentId, string InstructionFile, string InstructionPath,
    string BackupRelativePath, DateTimeOffset InstalledAt);
public sealed record GuidancePreview(string AgentId, string InstructionFile, string InstructionPath, string Rule);
public sealed record AgentSetupManifest(string Name, string Host = "manual", string? ConfigPath = null,
    string? CliPath = null, string? HostVersion = null, string[]? InstructionPaths = null);
internal sealed record GuidanceState(string WorkspaceKey, string RelayHome, GuidanceRegistration[] Registrations);

public sealed partial class RelayService
{
    private string GuidanceStatePath => Path.Combine(Home, "agent-guidance.json");
    private static readonly JsonSerializerOptions AgentSetupJson = new(RelayJson.Options)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static AgentSetupManifest ParseAgentSetupManifest(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > SetupAssistant.MaxResponseBytes)
            throw new RelayException("agent_setup_invalid", "Agent 配置 JSON 不能超过 64 KB。");
        var text = json.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var first = text.IndexOf('\n'); var last = text.LastIndexOf('\n');
            if (first < 0 || last <= first || text[..first].TrimEnd('\r') != "```json" || text[(last + 1)..] != "```")
                throw new RelayException("agent_setup_invalid", "请返回完整 JSON，或单个完整 json 代码围栏。");
            text = text[(first + 1)..last];
        }
        AgentSetupManifest input;
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Expected an object.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
                if (!keys.Add(property.Name)) throw new JsonException("Duplicate property: " + property.Name);
            input = document.RootElement.Deserialize<AgentSetupManifest>(AgentSetupJson) ?? throw new JsonException("Expected an object.");
        }
        catch (JsonException error) { throw new RelayException("agent_setup_invalid", "Agent 配置 JSON 格式不正确：" + error.Message); }
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Host) ||
            input.InstructionPaths?.Any(string.IsNullOrWhiteSpace) == true)
            throw new RelayException("agent_setup_invalid", "name、host 不能为空；instruction_paths 应为有效路径数组。");
        foreach (var path in new[] { input.ConfigPath, input.CliPath }.Concat(input.InstructionPaths ?? []))
            if (!string.IsNullOrWhiteSpace(path) && !Path.IsPathFullyQualified(path))
                throw new RelayException("agent_setup_invalid", "配置和程序文件应使用绝对路径。");
        return input;
    }

    public AgentRecord SetupAgentFromJson(string json, string? id = null)
    {
        var input = ParseAgentSetupManifest(json);
        if (id is not null && !Agents().Any(a => a.AgentId == id))
            throw new RelayException("agent_not_found", "更新时指定的 Agent 不存在。", 3);
        id ??= "agent-" + Guid.NewGuid().ToString("N")[..12];
        var result = string.IsNullOrWhiteSpace(input.ConfigPath)
            ? RegisterManual(id, input.Host.Trim(), input.Name.Trim(), EmptyToNull(input.CliPath), EmptyToNull(input.HostVersion), input.InstructionPaths)
            : Register(id, input.Host.Trim(), input.ConfigPath, EmptyToNull(input.CliPath), EmptyToNull(input.HostVersion), input.InstructionPaths, input.Name.Trim());
        RefreshGuidance();
        return result;

        static string? EmptyToNull(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    public string AgentSetupPrompt(string? id = null, string? relayCommand = "relay")
    {
        if (id is not null && !Agents().Any(a => a.AgentId == id))
            throw new RelayException("agent_not_found", "未找到该 Agent。", 3);
        var requestId = Guid.NewGuid().ToString("N");
        var registered = Agents().Select(agent => new { agent_id = agent.AgentId, name = agent.DisplayName ?? agent.AgentId });
        var intent = id is null
            ? "这是新建节点请求：不要使用任何已有节点 ID，不传 --id，不修改或覆盖目录中的节点。登记命令会自动生成全新的 agent_id。即使名称相似，也不能擅自复用已有节点。"
            : "这是用户明确指定的节点资料更新：仅更新 " + id + "，不要操作其他节点。";
        var setup = relayCommand is null
            ? "当前安装位置没有可确认的 Relay CLI。不要猜测程序路径或尝试执行；向用户说明无法自动登记，可在 Relay 的新建 Agent 页面按字段手动填写。不要让用户回粘整段配置。"
            : $"将下方 JSON 保存到临时文件，再运行：\n{relayCommand} --home \"{Home}\" agent setup --manifest <该 JSON 文件的绝对路径>" +
                (id is null ? "" : " --id " + id) + "\n需要 .NET 10 runtime；命令不可运行时说明原因，不安装依赖，不猜其他程序路径。";
        var recipeTemplate = new CliRecipe(1, "方案名称", "本次调查的宿主",
            new CliCommand("已核实的程序路径", ["{prompt}"], null, null), null, [], "", "", "", "", "text");
        var recipe = relayCommand is null
            ? "若已查证可用的调用方式，告诉用户程序路径、参数和输出格式，供手动配置。"
            : $"若已核实可用 CLI，将调用方案保存为临时 JSON，直接导入：\n{relayCommand} --home \"{Home}\" cli-recipe import --agent <本次登记命令返回的 Agent ID> --manifest <调用方案 JSON 路径>\n只使用本次命令返回的 ID。原生 JSON、JSONL 或文本由 Relay 适配，不要求宿主输出 Relay 专用回执。调用参数每项一个，不拼 shell 字符串；arguments 或 input 必须含 {{prompt}}。resume 无法确认则填 null，支持时须含 {{session_id}}。environment 可设非敏感环境变量。\noutput_format 可选：" +
                string.Join("、", CliRecipes.OutputFormats) + "。DSH 用 dsh-jsonl；Zcode 按输出选 zcode-json 或 zcode-jsonl；Hermes 用 hermes-jsonl；CodeBuddy/WorkBuddy 的兼容 JSON 用 claude-json；仅最终文本用 text；已存在的专用桥用 relay-json。格式应与已查证的实际输出一致，未知不要编造。模板：\n" + RelayJson.Write(recipeTemplate);
        return $$"""
            Agent Relay 一键配置请求：{{requestId}}
            {{intent}}
            当前节点目录（只用于避免重复和覆盖，不是本次写入目标）：
            {{RelayJson.Write(registered)}}
            {{(relayCommand is null ? "" : "执行登记前先运行 " + relayCommand + " --home \"" + Home + "\" agent list，读取最新目录。")}}
            请仅调查本机宿主和命令行接口，区分配置文件、CLI 程序和续接接口，不要把日志路径当作调用方案。不要查找、推测自己的或目标的会话 ID；会话发现与明确绑定由 Relay 管理。
            仅读取帮助、公开文档和不含密钥的设置。用已有 CLI 的 --help/version 查证是否支持创建持久会话、按明确 ID 续接及传入消息。不要要求模型让隐藏会话显示到桌面，不用 latest/continue 猜测会话；不要启动模型、安装程序或修改宿主设置。
            {{setup}}
            直写接口成功后，只向用户报告已登记的节点名称和返回的 agent_id；无需再粘贴相同 JSON 或在“新增”窗口重复保存，以免重复登记。
            {"name":"用户可识别的名称","host":"可识别的内部宿主类型，未知填manual","config_path":null,"cli_path":null,"host_version":null,"instruction_paths":[]}
            用户只需要一个名称。host 是内部适配资料，不另外要求用户提供软件名称。未知信息保留 null 或空数组，不猜路径，不返回认证信息；name 必填。会话 ID 与记录位置不能作为 config_path。
            {{recipe}}
            {{(relayCommand is null ? "" : BridgeGuidance(relayCommand))}}
            导入只保存，不执行或启用方案。没有可用接口时明确说明，使用手动交接即可。
            """;
    }

    private string BridgeGuidance(string relayCommand) => $"""
        优先查证宿主是否支持 Agent Client Protocol（帮助中的 --acp 选项或 acp 子命令）；支持时 output_format 用 acp，命令只启动 ACP 服务，create 与 resume 写同一条命令。
        宿主自带的命令行若不能无人值守调用（需另装官方 CLI、社区 ACP 适配器或其他桥接工具），不要自行安装。先向用户说明：工具名称、来源链接、许可证、为什么需要、不安装的后果（该 Agent 不能通过 CLI 自动接收任务，仍可复制提示词手动交接）。用户明确同意后才安装，并写入方案 dependencies。需要用户在终端登录的工具，把登录命令写入方案的 login 字段，由用户自己运行。
        已知情况：WorkBuddy 桌面版没有对外可用的调用接口，不要为它登记调用方案，也不要另装 CodeBuddy CLI；它通过复制提示词手动交接。
        导入后运行 {relayCommand} --home "{Home}" cli-recipe doctor --agent <Agent ID> 检查本机前提（不调用模型），把未通过的项和修复方法告诉用户。
        """;

    public IReadOnlyList<GuidanceRegistration> GuidanceRegistrations() => ReadGuidanceState()?.Registrations ?? [];

    public string StableInstructionPath(string agentId)
    {
        RequireGuidanceAgent(agentId);
        return InstructionPath(EnsureGuidanceState(), agentId);
    }

    public string GuidancePrompt(string agentId)
    {
        RequireGuidanceAgent(agentId);
        var state = EnsureGuidanceState();
        var path = InstructionPath(state, agentId);
        WriteGuidanceDocument(agentId, path);
        return "需要向其他 Agent 交接文件或收到 Relay 交接时，先阅读 `" + path + "`。";
    }

    public GuidancePreview PreviewGuidance(string agentId, string instructionFile)
    {
        var file = RequireInstructionFile(instructionFile);
        var rule = GuidancePrompt(agentId);
        return new(agentId, file, StableInstructionPath(agentId), rule);
    }

    public GuidanceRegistration InstallGuidance(string agentId, string instructionFile)
    {
        var preview = PreviewGuidance(agentId, instructionFile);
        var state = EnsureGuidanceState();
        var prior = state.Registrations.SingleOrDefault(r => r.AgentId == agentId);
        if (prior is not null && !SamePath(prior.InstructionFile, preview.InstructionFile))
            throw new RelayException("guidance_already_installed", "该节点已接入其他指令文件，请先移除原有接入规则。", 5);
        var original = File.Exists(preview.InstructionFile) ? File.ReadAllText(preview.InstructionFile) : "";
        var relative = prior?.BackupRelativePath ?? Path.Combine("guidance-backups", agentId + ".md");
        var backup = Path.Combine(Home, relative);
        if (!File.Exists(backup))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            if (File.Exists(preview.InstructionFile)) File.Copy(preview.InstructionFile, backup);
            else File.WriteAllText(backup, "");
        }
        var cleaned = StripGuidanceBlock(original, state.WorkspaceKey, agentId);
        var newline = original.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var prefix = cleaned.Length == 0 || cleaned.EndsWith('\n') ? "" : newline;
        var block = StartMarker(state.WorkspaceKey, agentId) + newline + preview.Rule + newline + EndMarker(state.WorkspaceKey, agentId) + newline;
        Directory.CreateDirectory(Path.GetDirectoryName(preview.InstructionFile)!);
        WritePreservingUtf8Bom(preview.InstructionFile, cleaned + prefix + block);
        var registration = new GuidanceRegistration(agentId, preview.InstructionFile, preview.InstructionPath, relative, prior?.InstalledAt ?? Now);
        SaveGuidanceState(state with { Registrations = state.Registrations.Where(r => r.AgentId != agentId).Append(registration).ToArray() });
        return registration;
    }

    public int RemoveGuidance(string agentId)
    {
        var state = ReadGuidanceState();
        if (state is null) return 0;
        var removed = state.Registrations.Where(r => r.AgentId == agentId).ToArray();
        foreach (var registration in removed)
        {
            if (File.Exists(registration.InstructionFile))
            {
                var original = File.ReadAllText(registration.InstructionFile);
                var cleaned = StripGuidanceBlock(original, state.WorkspaceKey, agentId);
                if (cleaned != original) WritePreservingUtf8Bom(registration.InstructionFile, cleaned);
            }
            File.Delete(registration.InstructionPath);
        }
        SaveGuidanceState(state with { Registrations = state.Registrations.Where(r => r.AgentId != agentId).ToArray() });
        return removed.Length;
    }

    public int RemoveAllGuidance()
    {
        var ids = GuidanceRegistrations().Select(r => r.AgentId).Distinct(StringComparer.Ordinal).ToArray();
        return ids.Sum(RemoveGuidance);
    }

    public void RefreshGuidance()
    {
        var state = ReadGuidanceState();
        if (state is null) return;
        var agents = Agents().Select(a => a.AgentId).ToHashSet(StringComparer.Ordinal);
        var registrations = state.Registrations.Select(registration => registration with
        {
            InstructionFile = RelocatedGuidancePath(registration.InstructionFile, state.RelayHome, Home)
        }).ToArray();
        foreach (var registration in registrations)
            if (agents.Contains(registration.AgentId)) WriteGuidanceDocument(registration.AgentId, registration.InstructionPath);
        SaveGuidanceState(state with { RelayHome = Home, Registrations = registrations });
    }

    private AgentRecord RequireGuidanceAgent(string id) => Agents().SingleOrDefault(a => a.AgentId == id)
        ?? throw new RelayException("agent_not_found", "未找到该 Agent。", 3);

    private GuidanceState EnsureGuidanceState()
    {
        var state = ReadGuidanceState();
        if (state is not null) return state;
        state = new(Guid.NewGuid().ToString("N"), Home, []);
        SaveGuidanceState(state);
        return state;
    }

    private GuidanceState? ReadGuidanceState()
    {
        if (!File.Exists(GuidanceStatePath)) return null;
        try
        {
            var state = RelayJson.Read<GuidanceState>(File.ReadAllText(GuidanceStatePath));
            if (!Guid.TryParseExact(state.WorkspaceKey, "N", out _) || string.IsNullOrWhiteSpace(state.RelayHome) ||
                !Path.IsPathFullyQualified(state.RelayHome) || state.Registrations is null)
                throw new RelayException("guidance_state_invalid", "接入规则记录无效。请检查 " + GuidanceStatePath);
            return state;
        }
        catch (JsonException error) { throw new RelayException("guidance_state_invalid", "无法读取接入规则记录：" + error.Message); }
    }

    private void SaveGuidanceState(GuidanceState state)
    {
        Directory.CreateDirectory(Home);
        var temporary = GuidanceStatePath + ".tmp";
        File.WriteAllText(temporary, RelayJson.Write(state));
        File.Move(temporary, GuidanceStatePath, true);
    }

    private static string InstructionPath(GuidanceState state, string agentId) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AgentRelay.Guidance", state.WorkspaceKey, agentId + ".md");

    private void WriteGuidanceDocument(string agentId, string path)
    {
        var agent = RequireGuidanceAgent(agentId);
        var nodes = Agents().Select(a => new { id = a.AgentId, name = a.DisplayName ?? a.AgentId });
        var document = $$"""
            # Agent Relay 按需交接

            当前节点：{{agent.DisplayName ?? agent.AgentId}}（{{agentId}}）
            当前中转站：{{Home}}

            只有用户要求交给其他 Agent 的工作文件或收到明确的 Relay 交接时，才使用本规则。日常对话不读取其他节点文件，不自行发起协作。

            ## 发给其他 Agent

            先写好实际文件，再在回复中仅输出以下 JSON 供用户粘贴到 Relay。file 使用可读的绝对文件路径；to 选下方节点 ID 或唯一名称；message 只写一句实际工作要求。不要在 JSON 中粘贴整份文件。

            {"from":"{{agentId}}","to":"目标节点 ID 或唯一名称","file":"文件绝对路径","message":"请处理的具体事项"}

            ## 收到交接

            先读取短提示指定的 context 文件，再处理其中 current_file 指定的工作文件；按 context 中的回执路径和 receipt_template 写回明确状态。原样保留任务 ID、尝试 ID 与轮次，不用文件修改冒充完成。已有交接只写原任务回执，不再另发一份新建交接 JSON；单次交接的必要回传由 needs_reply 表达，持续修改与验收任务按 context 中 collaboration.allowed_outcomes 与 receipt_template 用 outcome（及 open_items 或 deliverable）写明结论。
            只有仍有具体工作、问题或必须确认的结果要交回对方时才需要再次传递；写明下一步工作。已经处理或确认的事项直接结束，不发送礼貌回合、重复确认或寒暄。

            ## 节点目录

            {{RelayJson.Write(nodes)}}

            AGENTS.md 中的一句话仍会随宿主常驻指令加载；详细规则按需读取，是否遵从取决于宿主。固定入口不随中转站迁移改变；复制出去的旧提示需要重新复制。
            移除接入规则可在 Relay 中操作，或由用户明确要求后运行 relay --home "{{Home}}" guidance remove --all。直接删除 ZIP 解压目录不会执行卸载清理。
            """;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, document, new UTF8Encoding(false));
    }

    private static string RequireInstructionFile(string file)
    {
        if (!Path.IsPathFullyQualified(file)) throw new RelayException("guidance_path_invalid", "请使用指令文件的绝对路径。");
        return Path.GetFullPath(file);
    }

    private static string StartMarker(string key, string id) => $"<!-- agent-relay:{key}:{id}:start -->";
    private static string EndMarker(string key, string id) => $"<!-- agent-relay:{key}:{id}:end -->";
    private static bool SamePath(string first, string second) => string.Equals(first, second,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static string RelocatedGuidancePath(string path, string previous, string current)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var prefix = Path.TrimEndingDirectorySeparator(previous) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, comparison) ? Path.TrimEndingDirectorySeparator(current) + path[(prefix.Length - 1)..] : path;
    }

    private static string StripGuidanceBlock(string text, string key, string id)
    {
        var startToken = StartMarker(key, id); var endToken = EndMarker(key, id);
        var start = text.IndexOf(startToken, StringComparison.Ordinal);
        if (start < 0)
        {
            if (text.Contains(endToken, StringComparison.Ordinal)) throw new RelayException("guidance_block_changed", "接入标记不完整，请先检查指令文件。");
            return text;
        }
        var end = text.IndexOf(endToken, start + startToken.Length, StringComparison.Ordinal);
        if (end < 0 || text.IndexOf(startToken, start + startToken.Length, StringComparison.Ordinal) >= 0)
            throw new RelayException("guidance_block_changed", "接入标记不完整或重复，请先检查指令文件。");
        end += endToken.Length;
        if (end < text.Length && text[end] == '\r') end++;
        if (end < text.Length && text[end] == '\n') end++;
        return text[..start] + text[end..];
    }

    private static void WritePreservingUtf8Bom(string path, string text)
    {
        var hasBom = false;
        if (File.Exists(path))
        {
            using var input = File.OpenRead(path);
            Span<byte> prefix = stackalloc byte[3];
            hasBom = input.Read(prefix) == 3 && prefix.SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF });
        }
        File.WriteAllText(path, text, new UTF8Encoding(hasBom));
    }
}
