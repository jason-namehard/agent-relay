namespace AgentRelay.Core;

/// <param name="Blocking">Retrying unattended cannot succeed until the user changes the host or recipe.</param>
public sealed record CliFailure(string Kind, string Message, bool Blocking);

public static class CliFailureDiagnostics
{
    private static readonly string[] AuthenticationSignals =
        ["Authentication required", "Please use /login", "Not logged in", "Please log in", "login required"];

    public static string Explain(ProcessResult process) => Classify(process).Message;

    public static CliFailure Classify(ProcessResult process, CliRecipe? recipe = null)
    {
        // These are known external host diagnostics. Keep full output in the probe's diagnostic detail.
        var output = process.Error + "\n" + process.Output;
        if (output.Contains("Select a model before", StringComparison.OrdinalIgnoreCase))
            return new("model_not_selected", "目标 CLI 尚未选择可用模型。请先在宿主选择默认模型，或在任务中选择已有且已配置模型的会话继续。Relay 不会猜测模型；原始错误见 CLI 诊断详情。", true);
        // Some hosts print the refusal and still exit 0, so the exit code cannot rule this out.
        if (AuthenticationSignals.Any(signal => output.Contains(signal, StringComparison.OrdinalIgnoreCase)))
            return new("auth_required", "目标 CLI 没有可用的登录。桌面应用的登录不一定能被命令行使用，需要为该 CLI 单独登录" +
                (string.IsNullOrWhiteSpace(recipe?.Login) ? "。" : "：在终端运行 " + recipe.Login + " 并按提示完成。") +
                "完成后在 Agent 管理中点“我已处理，重新检测”。原始错误见 CLI 诊断详情。", true);
        if (process.TimedOut)
            return new("timeout", "CLI 等待超时或已取消，可能已经提交。请检查目标会话，不要重复发送。", false);
        if (process.ExitCode != 0)
            return new("exit_error", "CLI 返回错误：" + process.Error, false);
        return new("output_unrecognized", "未能从配置的输出格式读取准确的 CLI 结果。请检查输出格式与诊断详情；续接必须返回同一会话 ID。", false);
    }

    public static CliFailure PermissionRequired(CliRecipe recipe) => new("permission_required",
        "CLI 回复了探针标识，但没有写出要求的文件：无人值守调用时缺少工具权限，Agent 无法写回执。" +
        "请在调用方案中加入该宿主文档列出的非交互授权参数" +
        (CliRecipes.OutputFormatFor(recipe) == "claude-json" ? "（CodeBuddy/WorkBuddy 为 -y 或 --permission-mode）" : "") + "后重新测试。", true);
}
