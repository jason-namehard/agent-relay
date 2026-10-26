namespace AgentRelay.Core;

public static class CliFailureDiagnostics
{
    public static string Explain(ProcessResult process)
    {
        // These are known external host diagnostics. Keep full output in the probe's diagnostic detail.
        var output = process.Error + "\n" + process.Output;
        if (output.Contains("Select a model before", StringComparison.OrdinalIgnoreCase))
            return "目标 CLI 尚未选择可用模型。请先在宿主选择模型，或在任务中选择已有且已配置模型的会话继续。Relay 不会猜测模型；原始错误见 CLI 诊断详情。";
        if (output.Contains("Authentication required", StringComparison.OrdinalIgnoreCase))
            return "目标 CLI 尚未完成登录。请在该 CLI 中完成登录后再发送；桌面端登录不一定适用于 CLI。原始错误见 CLI 诊断详情。";
        if (process.TimedOut)
            return "CLI 等待超时或已取消，可能已经提交。请检查目标会话，不要重复发送。";
        if (process.ExitCode != 0)
            return "CLI 返回错误：" + process.Error;
        return "未能从配置的输出格式读取准确的 CLI 结果。请检查输出格式与诊断详情；续接必须返回同一会话 ID。";
    }
}
