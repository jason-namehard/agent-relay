using System.Text.Json;

namespace AgentRelay.Core;

/// <param name="Status">pass, warn or fail. A fail means unattended delivery cannot work until the user fixes it.</param>
public sealed record CliHealthCheck(string Id, string Status, string Message);

public sealed partial class RelayService
{
    /// <summary>Checks a recipe's local prerequisites without starting a model turn.</summary>
    public IReadOnlyList<CliHealthCheck> CliHealth(string agentId)
    {
        _ = Agents().SingleOrDefault(a => a.AgentId == agentId) ?? throw new RelayException("agent_not_found", "未找到 Agent：" + agentId);
        var extension = CliExtension(agentId);
        if (extension is null)
            return [new("recipe", "fail", "尚未登记 CLI 调用方案。可在 Agent 管理用“一键配置”让 Agent 调查并登记，或继续复制提示词手动交接。")];
        var recipe = extension.Recipe;
        var format = CliRecipes.OutputFormatFor(recipe);
        var checks = new List<CliHealthCheck>();
        foreach (var (name, command) in new[] { ("create", recipe.Create), ("resume", recipe.Resume) })
        {
            if (command is null) continue;
            checks.Add(ResolveExecutable(command.Executable) is { } found
                ? new($"{name}.executable", "pass", "程序存在：" + found)
                : new($"{name}.executable", "fail", "找不到程序 " + command.Executable + "。宿主可能已移动或卸载，请重新运行一键配置或编辑调用方案。"));
            foreach (var script in command.Arguments.Where(a => Path.IsPathFullyQualified(a) && !Directory.Exists(a)))
                if (!File.Exists(script)) checks.Add(new($"{name}.script", "fail", "找不到入口文件 " + script + "。宿主更新后路径可能已改变。"));
            if (command.WorkingDirectory is { } cwd && !Directory.Exists(cwd))
                checks.Add(new($"{name}.cwd", "fail", "工作目录不存在：" + cwd));
        }
        checks.Add(recipe.Resume is null
            ? new("resume", "warn", "方案没有续接命令：每轮都会新建会话，回稿不能回到同一对话。")
            : new("resume", "pass", "已配置续接命令。"));
        checks.AddRange(HostChecks(recipe, format));
        if (extension.Block is { } block) checks.Add(new("block", "fail", block.Message));
        checks.Add(new("verified.create", extension.CanCreate ? "pass" : "warn", extension.CanCreate
            ? "新建会话已验证。" : "新建会话尚未验证：探针通过并确认可见，或一次新建会话的真实交接写回回执后才会自动投递。"));
        if (recipe.Resume is not null)
            checks.Add(new("verified.resume", extension.CanResume ? "pass" : "warn", extension.CanResume
                ? "续接已验证。" : "续接尚未验证：续接探针通过并确认，或一次续接的真实交接写回回执后才会自动投递。"));
        return checks;
    }

    private static IEnumerable<CliHealthCheck> HostChecks(CliRecipe recipe, string format)
    {
        var arguments = recipe.Create.Arguments.Prepend(recipe.Create.Executable).ToArray();
        if (format == "claude-json")
        {
            if (arguments.Any(a => a.Replace('\\', '/').EndsWith("/codebuddy-lite-wb.mjs", StringComparison.OrdinalIgnoreCase)))
                yield return new("host.login", "fail", "WorkBuddy 桌面版没有对外可用的调用接口：自带的 CLI 单独启动用不了桌面登录，打开中的对话也不接受外部提示词。请给 WorkBuddy 选择“无”，用复制提示词手动交接。");
            if (!arguments.Any(a => a is "-y" or "--dangerously-skip-permissions" or "--permission-mode"))
                yield return new("host.permission", "fail", "缺少非交互权限参数（-y 或 --permission-mode acceptEdits）：无人值守时 Agent 无法写回执。");
        }
        if (format is "zcode-json" or "zcode-jsonl")
        {
            var config = recipe.Create.Environment?.GetValueOrDefault("ZCODE_PERSONAL_PROVIDER_CONFIG_FILE")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".zcode", "v2", "provider_config.json");
            // The desktop app registers account plans itself; a standalone CLI needs the identity record that `zcode login` writes.
            yield return HasZcodeAccountProvider(Path.Combine(Path.GetDirectoryName(config)!, "credentials.json"))
                ? new("host.account", "pass", "命令行已登记 Z.AI 账号套餐。")
                : new("host.account", "fail", "Zcode 命令行没有登记账号套餐，可选模型为空，新建会话会报“Select a model”。桌面已登录不等于命令行可用：请在终端运行一次 zcode login zai（并设置 ZCODE_BUILTIN_PROVIDER_CONFIG_FILE 指向安装目录下 resources/config/provider/zcode-builtin.json），或续接已选过模型的会话。");
            yield return HasZcodeDefaultModel(config)
                ? new("host.model", "pass", "已设置命令行默认模型（" + config + "）。")
                : new("host.model", "warn", "Zcode 命令行没有默认模型；登录后会自动写入，在桌面切换模型不会写入这一项。");
        }
    }

    // Reads key names only; credential values are never loaded into Relay.
    private static bool HasZcodeAccountProvider(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.EnumerateObject()
                .Any(p => p.Name.StartsWith("account-provider:", StringComparison.Ordinal) && p.Name.EndsWith(":identity", StringComparison.Ordinal));
        }
        catch (JsonException) { return false; }
    }

    private static bool HasZcodeDefaultModel(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("config", out var config) &&
                config.TryGetProperty("defaultModelSelection", out var selection) && selection.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException) { return false; } // An unreadable host file means the CLI cannot use it either.
    }

    private static string? ResolveExecutable(string executable)
    {
        if (Path.IsPathFullyQualified(executable)) return File.Exists(executable) ? executable : null;
        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';').Prepend("")
            : [""];
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        foreach (var extension in extensions)
        {
            var candidate = Path.Combine(directory.Trim('"'), executable + extension);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
