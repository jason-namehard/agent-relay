namespace AgentRelay.Core;

public sealed partial class RelayService
{
    public async Task<List<TaskView>> RecoverAsync(string? taskId = null, CancellationToken cancellationToken = default)
    {
        var ids = store.Write(s =>
        {
            var tasks = taskId is null
                ? s.Query("SELECT json FROM tasks", r => RelayJson.Read<TaskRecord>(r.GetString(0)))
                : [s.Task(taskId)];
            foreach (var task in tasks)
            {
                RecoverContinuations(s, task);
                if (Terminal(task.State)) continue;
                var execution = Execution(s, task.TaskId, task.AttemptId);
                if (execution is { State: "scheduled" or "receiving" or "running" } && execution.LeaseUntil <= Now)
                {
                    SaveExecution(s, execution with { State = "unknown", ErrorCode = "execution_lease_expired" });
                    task.ErrorCode = "execution_unknown";
                    task.ErrorMessage = "单次 worker 未在时限内留下最终声明。";
                    s.Save(task); s.Event(task, "execution.unknown", "relay", Now, "execution_lease_expired");
                    s.Notify(task, "execution_attention", "执行结果未知，不会自动重新启动 Agent。", Now);
                }
                var box = Outbox(s, task.TaskId);
                if (task.IsFileHandoff)
                {
                    // File handoffs keep their sessions and files when a turn runs over; the user decides how to go on.
                    if (task.State != TaskState.Paused && task.DeadlineAt <= Now) { PauseForTimeout(s, task); s.Save(task); }
                    else if (RecoverExpiredFileDelivery(s, task)) s.Save(task);
                }
                else if (task.DeadlineAt <= Now)
                {
                    task.State = TaskState.TimedOut; s.Save(task);
                    s.Event(task, "task.timed_out", "relay", Now);
                    s.Notify(task, "timed_out", "截止时间已过，未收到有效完成结果；宿主可能仍在执行。", Now);
                    if (!task.IsFileHandoff) QueueContinuation(s, task, "timed_out");
                    s.Execute("UPDATE outbox SET state='settled',lease_until=NULL WHERE task_id=$id", ("$id", task.TaskId));
                }
                else if (box is { State: "in_flight", LeaseUntil: not null } && box.LeaseUntil <= Now)
                {
                    task.State = TaskState.DeliveryUnknown; s.Save(task);
                    s.Execute("UPDATE outbox SET state='unknown',lease_until=NULL WHERE task_id=$id", ("$id", task.TaskId));
                    s.Event(task, "task.delivery_unknown", "relay", Now, "expired in-flight lease; do not resend");
                    s.Notify(task, "delivery_attention", "运输进程未留下确定结果，禁止自动重发。", Now);
                }
            }
            return tasks.Select(t => t.TaskId).ToArray();
        });
        foreach (var id in ids)
        {
            await DeliverPendingAsync(id, cancellationToken);
            await ProcessContinuationsAsync(id, cancellationToken: cancellationToken);
        }
        return ids.Select(Status).ToList();
    }

    public CleanupResult Cleanup(bool apply)
    {
        var settings = Settings();
        var allArtifacts = store.Read(s => s.Query("SELECT json FROM artifacts", r => RelayJson.Read<ArtifactRecord>(r.GetString(0))));
        var eligible = allArtifacts.Where(a => a.State != "expired" && a.ExpiresAt <= Now).ToList();
        var registeredPaths = allArtifacts.Select(a => a.Path).ToHashSet(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        // A filesystem/SQLite crash can leave unpublished temp files or unregistered submissions.
        // Explicit cleanup expires those by filesystem creation time, without following directory links.
        var orphans = Directory.Exists(workspace.Root)
            ? Directory.EnumerateFiles(workspace.Root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint })
                .Where(path => !registeredPaths.Contains(path) && new DateTimeOffset(File.GetCreationTimeUtc(path)).AddDays(settings.ArtifactTtlDays) <= Now).ToArray()
            : [];
        foreach (var artifact in eligible) workspace.RequireManaged(artifact.Path);
        foreach (var path in orphans) workspace.RequireManaged(path);
        var paths = eligible.Select(a => a.Path).Concat(orphans).ToArray();
        if (!apply) return new CleanupResult(paths.Length, 0, paths);
        var deleted = 0;
        foreach (var artifact in eligible)
        {
            var marked = store.Write(s =>
            {
                var current = s.Json<ArtifactRecord>("SELECT json FROM artifacts WHERE id=$id", ("$id", artifact.ArtifactId))!;
                if (current.State == "expired" || current.ExpiresAt > Now) return false;
                var task = s.Task(current.TaskId);
                if (current.AttemptId == task.AttemptId && !Terminal(task.State))
                {
                    task.State = TaskState.TimedOut; s.Save(task);
                    s.Execute("UPDATE outbox SET state='settled',lease_until=NULL WHERE task_id=$id", ("$id", task.TaskId));
                    s.Event(task, "task.timed_out", "relay", Now, "artifact TTL elapsed");
                    s.Notify(task, "timed_out", "交接产物已过期，请检查任务并决定重新生成。", Now);
                    if (!task.IsFileHandoff) QueueContinuation(s, task, "timed_out");
                }
                SaveArtifact(s, current with { State = "deleting" });
                return true;
            });
            if (!marked) continue;
            File.Delete(workspace.RequireManaged(artifact.Path));
            store.Write(s =>
            {
                var current = s.Json<ArtifactRecord>("SELECT json FROM artifacts WHERE id=$id", ("$id", artifact.ArtifactId))!;
                if (current.State == "expired") return false;
                SaveArtifact(s, artifact with { State = "expired" });
                s.Event(s.Task(artifact.TaskId), "artifact.expired", "relay", Now, artifact.ArtifactId);
                return true;
            });
            deleted++;
        }
        foreach (var path in orphans)
        {
            var removed = store.Write(s =>
            {
                if (s.Json<ArtifactRecord>("SELECT json FROM artifacts WHERE path=$path", ("$path", path)) is not null) return false;
                if (!File.Exists(path) || new DateTimeOffset(File.GetCreationTimeUtc(path)).AddDays(settings.ArtifactTtlDays) > Now) return false;
                File.Delete(workspace.RequireManaged(path));
                return true;
            });
            if (removed) deleted++;
        }
        return new CleanupResult(paths.Length, deleted, paths);
    }
}
