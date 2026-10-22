namespace AgentRelay.Core;

public sealed class DesktopNotifier(string? executable) : IDesktopNotifier
{
    public async Task<ResumeResult> NotifyAsync(string home, TaskRecord task, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) return new ResumeResult("unavailable", "desktop_platform", "桌面通知当前仅支持 Windows。");
        if (executable is null) return new ResumeResult("unavailable", "desktop_unconfigured", "请使用 desktop configure 登记通知程序；结果与提示词已保存在 Inbox。");
        var report = Path.Combine(Path.GetDirectoryName(task.RequestPath)!, "desktop-" + Guid.NewGuid() + ".json");
        var process = await RelayProcess.RunAsync(executable,
            ["--notify", "--home", home, "--task", task.TaskId, "--attempt", task.AttemptId, "--report-path", report], null, TimeSpan.FromSeconds(30), cancellationToken);
        if (process.TimedOut) return new ResumeResult("unknown", "desktop_timed_out", "通知调用超时，可能已提交，不会自动重复发送。");
        if (!File.Exists(report)) return new ResumeResult("failed", "desktop_report_missing", "通知程序没有返回报告：" + process.Error);
        var result = RelayJson.Read<ResumeResult>(File.ReadAllText(report));
        if (result.State is not ("submitted" or "failed" or "unavailable"))
            throw new RelayException("invalid_desktop_report", "通知程序返回了无效状态。", 3);
        if (process.ExitCode != 0 && result.State == "submitted")
            return new ResumeResult("unknown", "desktop_exit_failed", "通知可能已提交，辅助程序异常退出。");
        return result;
    }
}

public sealed partial class RelayService
{
    public string? DesktopExecutable() => store.Read(s => s.Query("SELECT executable FROM desktop WHERE id=1", r => r.GetString(0)).SingleOrDefault());

    public async Task<object> ConfigureDesktopAsync(string executable, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) throw new RelayException("desktop_platform", "桌面通知当前仅支持 Windows。", 3);
        var full = Path.GetFullPath(executable);
        var report = Path.Combine(Home, "desktop-probe-" + Guid.NewGuid() + ".json");
        try
        {
            var result = await RelayProcess.RunAsync(full, ["--probe", "--report-path", report], null, TimeSpan.FromSeconds(20), cancellationToken);
            if (result.TimedOut || result.ExitCode != 0 || !File.Exists(report) ||
                RelayJson.Read<ResumeResult>(File.ReadAllText(report)) is not { State: "supported", Detail: "agent-relay-desktop:1" })
                throw new RelayException("desktop_verification_failed", "通知辅助程序未通过能力验证：" + result.Error, 3);
        }
        finally { File.Delete(report); }
        store.Write(s => s.Execute("INSERT INTO desktop VALUES(1,$exe) ON CONFLICT(id) DO UPDATE SET executable=excluded.executable", ("$exe", full)));
        return new { executable = full, status = "configured", registration = "on_first_notification" };
    }

    public void DisableDesktop() => store.Write(s => s.Execute("DELETE FROM desktop"));
}
