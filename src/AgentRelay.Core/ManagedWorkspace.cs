namespace AgentRelay.Core;

public sealed class ManagedWorkspace(string home)
{
    public string Root { get; } = Path.GetFullPath(Path.Combine(home, "workspace"));

    public string AttemptDirectory(string taskId, string attemptId) => Path.Combine(Root, taskId, attemptId);

    public string RequireManaged(string path)
    {
        var full = Path.GetFullPath(path);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!full.StartsWith(Root + Path.DirectorySeparatorChar, comparison))
            throw new RelayException("unmanaged_path", "产物必须位于 Relay workspace 内；外部文件请先用 artifact register 导入。", 3);
        // Imported paths must not redirect writes or cleanup outside the managed tree.
        for (var current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new RelayException("linked_path", "受管路径不能经过符号链接或目录联接。", 3);
            if (string.Equals(current, Root, comparison)) break;
        }
        return full;
    }

    public ArtifactRecord Publish(TaskRecord task, string kind, string content, DateTimeOffset now, int ttlDays)
    {
        var id = Guid.NewGuid().ToString();
        var path = RequireManaged(Path.Combine(AttemptDirectory(task.TaskId, task.AttemptId), $"{kind}-{id}.md"));
        var artifact = new ArtifactRecord(id, task.TaskId, task.AttemptId, kind, path, now, now.AddDays(ttlDays), "available");
        var markdown = $"""
            ---
            relay_version: 1
            task_id: {task.TaskId}
            attempt_id: {task.AttemptId}
            sender_id: {(kind == "request" ? task.RequesterId : task.RecipientId)}
            recipient_id: {(kind == "request" ? task.RecipientId : task.RequesterId)}
            artifact_kind: {kind}
            created_at: {now.UtcDateTime:O}
            expires_at: {artifact.ExpiresAt.UtcDateTime:O}
            ---

            {content}
            """;
        WriteNew(path, markdown);
        return artifact;
    }

    public void WriteNew(string path, string content)
    {
        var full = RequireManaged(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp = full + "." + Guid.NewGuid() + ".tmp";
        File.WriteAllText(temp, content, new System.Text.UTF8Encoding(false));
        File.Move(temp, full);
    }
}
