namespace AgentRelay.Core;

public sealed record PermissionRecord(string From, string To, string Decision);
public sealed record ConnectionView(string From, string To, string Permission, string Capability, string Display, string Reason);
public sealed record HomeSummary(int RegisteredAgents, int PendingTasks, int NeedsAttention, int RetentionDays);

public sealed partial class RelayService
{
    public List<PermissionRecord> ConnectionPermissions() => store.Read(s => s.Query(
        "SELECT sender,recipient,decision FROM permissions ORDER BY sender,recipient",
        r => new PermissionRecord(r.GetString(0), r.GetString(1), r.GetString(2))));

    public List<ConnectionView> Connections()
    {
        var agents = Agents();
        var permissions = ConnectionPermissions().ToDictionary(p => (p.From, p.To));
        var automatic = Adapters().Any(a => a.AdapterId == "amq-cli" && a.VerificationStatus == "passed" && File.Exists(a.Executable));
        return (from sender in agents from recipient in agents where sender.AgentId != recipient.AgentId
            let permission = permissions.TryGetValue((sender.AgentId, recipient.AgentId), out var p) ? p.Decision : "default_deny"
            let available = Available(sender) && Available(recipient)
            select new ConnectionView(sender.AgentId, recipient.AgentId, permission,
                available ? automatic ? "manual + AMQ (verified)" : "manual" : "unavailable / unverified",
                permission == "deny" ? "Denied" : permission == "default_deny" ? "Default deny" : available ? "Allowed" : "Unavailable",
                available ? "手工交接可用；自动执行与会话继续需各自验证。" : "登记路径未验证、为空或已失效；请在 Agents 纠正并验证。"))
            .ToList();
    }

    private static bool Available(AgentRecord a) => a.ValidationStatus == "path_verified" &&
        File.Exists(a.ConfigPath) && new FileInfo(a.ConfigPath).Length > 0 &&
        (a.CliPath is null || File.Exists(a.CliPath)) && a.InstructionPaths.All(File.Exists);

    public static bool NeedsAttention(TaskView view) => view.DeadlineExceeded ||
        view.Task.State is TaskState.Failed or TaskState.TimedOut or TaskState.DeliveryUnknown ||
        view.Task.ErrorCode == "execution_unknown" ||
        (view.Task.State == TaskState.Succeeded && view.Task.ResumeStatus is not ("submitted" or "completed"));

    public HomeSummary Summary()
    {
        var tasks = Tasks();
        return new(Agents().Count, tasks.Count(t => !Terminal(t.Task.State)), tasks.Count(NeedsAttention), Settings().ArtifactTtlDays);
    }

    public void MarkNotificationRead(long notificationId) => store.Write(s =>
    {
        if (s.Execute("UPDATE notifications SET is_read=1 WHERE id=$id", ("$id", notificationId)) != 1)
            throw new RelayException("notification_not_found", "未找到该通知。", 3);
        return true;
    });
}
