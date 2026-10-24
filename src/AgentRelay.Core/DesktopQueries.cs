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
        return (from sender in agents from recipient in agents where sender.AgentId != recipient.AgentId
            let permission = permissions.TryGetValue((sender.AgentId, recipient.AgentId), out var p) ? p.Decision : "default_deny"
            let cli = CliExtension(recipient.AgentId)?.CanCreate == true
            let available = cli
            select new ConnectionView(sender.AgentId, recipient.AgentId, permission,
                available ? "verified CLI" : "manual",
                !available ? "Unavailable" : permission == "allow" ? "Allowed" : "Denied",
                available ? "目标 Agent 的可见新会话已验证；该方向需要单独启用。" : "缺少已验证的自动通知方案；可在设置中扩充 CLI 接口，或手动复制提示词。"))
            .ToList();
    }

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
