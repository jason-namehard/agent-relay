using Microsoft.Data.Sqlite;

namespace AgentRelay.Core;

public sealed record RelayRelocationRequest(string Source, string Target);

public sealed partial class RelayService
{
    internal const string RelocationPendingFile = "relay.relocation.pending.json";
    internal const string RelocationMovedFile = "relay.relocation.moved.json";
    internal const string RelocationIncompleteFile = "relay.relocation.incomplete.json";

    public RelayService VerifyRelocation()
    {
        var pending = Path.Combine(Home, RelocationPendingFile);
        if (!File.Exists(pending))
            throw new RelayException("relocation_no_pending", $"没有待继续的目录迁移。迁移校验不会更换中转站；请先选择更换中转站文件夹。当前目录：{Home}");
        return Relocate(RelayJson.Read<RelayRelocationRequest>(File.ReadAllText(pending)).Target);
    }

    public RelayService Relocate(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) throw new RelayException("relocation_target_invalid", "请选择新的中转站目录。");
        target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target));
        var source = Path.TrimEndingDirectorySeparator(Home);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(source, target, comparison))
            throw new RelayException("relocation_same_directory", "选择的目录就是当前中转站，未执行迁移。请为更换中转站选择另一个空目录。");
        if (Within(source, target, comparison) || Within(target, source, comparison))
            throw new RelayException("relocation_target_invalid", "新旧中转站目录不能相互包含。");
        var request = new RelayRelocationRequest(source, target);
        var incomingMarker = Path.Combine(target, RelocationIncompleteFile);
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
        {
            if (!File.Exists(incomingMarker) || RelayJson.Read<RelayRelocationRequest>(File.ReadAllText(incomingMarker)) != request)
                throw new RelayException("relocation_target_not_empty", "目标目录已有文件，请选择空目录；待完成迁移可使用迁移校验重试。");
        }

        using var sourceConnection = store.Open();
        using var sourceTransaction = sourceConnection.BeginTransaction();
        var session = new StoreSession(sourceConnection, sourceTransaction);
        var active = session.Query("SELECT json FROM tasks", r => RelayJson.Read<TaskRecord>(r.GetString(0)))
            .FirstOrDefault(task => !Terminal(task.State));
        if (active is not null)
            throw new RelayException("relocation_active_tasks", $"任务 {active.TaskId} 尚未结束，请完成或取消交接后再迁移。");
        var working = session.Query("SELECT json FROM executions", r => RelayJson.Read<ExecutionRecord>(r.GetString(0)))
            .Any(entry => entry.State is "scheduled" or "receiving" or "running");
        var continuing = session.Query("SELECT json FROM continuations", r => RelayJson.Read<ContinuationRecord>(r.GetString(0)))
            .Any(entry => entry.State == "in_flight" || entry.DesktopState == "in_flight");
        if (working || continuing)
            throw new RelayException("relocation_active_tasks", "仍有执行或通知正在进行，请等待结束后再迁移。");

        // Filesystem failures retain the source and a retry request; the destination cannot be opened until complete.
        File.WriteAllText(Path.Combine(source, RelocationPendingFile), RelayJson.Write(request));
        try
        {
            Directory.CreateDirectory(target);
            File.WriteAllText(incomingMarker, RelayJson.Write(request));
            CopyRelocationDirectory(source, target, true);
            using (var snapshotSource = store.Open(true))
            using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(target, "relay.db"), Pooling = false
            }.ToString()))
            {
                destination.Open();
                snapshotSource.BackupDatabase(destination);
                using var transaction = destination.BeginTransaction();
                RewriteRelocatedPaths(new StoreSession(destination, transaction), source, target, comparison);
                transaction.Commit();
                using var check = destination.CreateCommand();
                check.CommandText = "PRAGMA quick_check";
                if ((string?)check.ExecuteScalar() != "ok")
                    throw new RelayException("relocation_database_invalid", "复制后的数据库校验未通过，原中转站仍可使用。");
            }
            File.Delete(incomingMarker);
            // The atomic rename is the switch: all fallible destination work finishes first.
            File.Move(Path.Combine(source, RelocationPendingFile), Path.Combine(source, RelocationMovedFile));
            return new RelayService(target, clock, adapters.Values);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SqliteException)
        {
            if (Directory.Exists(target) && !File.Exists(incomingMarker))
                File.WriteAllText(incomingMarker, RelayJson.Write(request));
            throw new RelayException("relocation_failed", $"中转站迁移未完成，原目录仍保留。请解除文件占用或修复访问权限，再运行迁移校验。{error.Message}");
        }
    }

    private static bool Within(string path, string root, StringComparison comparison) =>
        path.StartsWith(root + Path.DirectorySeparatorChar, comparison);

    private static void CopyRelocationDirectory(string source, string target, bool isRoot)
    {
        // A retry mirrors the current source, including files removed since the failed attempt.
        foreach (var entry in Directory.EnumerateFileSystemEntries(target))
        {
            var name = Path.GetFileName(entry);
            if (isRoot && (name is "relay.db" or "relay.db-wal" or "relay.db-shm" or RelocationIncompleteFile)) continue;
            if (Path.Exists(Path.Combine(source, name))) continue;
            if (Directory.Exists(entry)) Directory.Delete(entry, true);
            else File.Delete(entry);
        }
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            var name = Path.GetFileName(entry);
            if (isRoot && (name is "relay.db" or "relay.db-wal" or "relay.db-shm" or
                RelocationPendingFile or RelocationMovedFile or RelocationIncompleteFile)) continue;
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new RelayException("relocation_linked_path", $"中转站包含链接路径，无法按普通文件迁移：{entry}");
            var output = Path.Combine(target, name);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                Directory.CreateDirectory(output);
                CopyRelocationDirectory(entry, output, false);
            }
            else
            {
                File.Copy(entry, output, true);
                if (new FileInfo(entry).Length != new FileInfo(output).Length)
                    throw new IOException($"复制文件长度不一致：{entry}");
            }
        }
    }

    private static void RewriteRelocatedPaths(StoreSession session, string source, string target, StringComparison comparison)
    {
        string Move(string path) => string.Equals(path, source, comparison) ? target :
            Within(path, source, comparison) ? target + path[source.Length..] : path;
        TaskRecord MoveTask(TaskRecord task)
        {
            task.RequestPath = Move(task.RequestPath);
            task.ResultPath = Move(task.ResultPath);
            task.ManualCurrentFile = task.ManualCurrentFile is null ? null : Move(task.ManualCurrentFile);
            task.ManualSourceFile = task.ManualSourceFile is null ? null : Move(task.ManualSourceFile);
            return task;
        }
        void Rewrite<T>(string table, string column, Func<T, T> transform) where T : class
        {
            var records = session.Query($"SELECT rowid,{column} FROM {table}", r => (Row: r.GetInt64(0), Value: RelayJson.Read<T>(r.GetString(1))));
            foreach (var record in records)
                session.Execute($"UPDATE {table} SET {column}=$json WHERE rowid=$row", ("$json", RelayJson.Write(transform(record.Value))), ("$row", record.Row));
        }
        Rewrite<TaskRecord>("tasks", "json", MoveTask);
        Rewrite<AgentRecord>("agents", "json", value => value with
        {
            ConfigPath = Move(value.ConfigPath), CliPath = value.CliPath is null ? null : Move(value.CliPath),
            InstructionPaths = value.InstructionPaths.Select(Move).ToArray()
        });
        Rewrite<ExecutorRecord>("executors", "json", value => value with
        {
            Executable = Move(value.Executable), ConfigDirectory = Move(value.ConfigDirectory)
        });
        Rewrite<ResumerRecord>("resumers", "json", value => value with
        {
            Executable = Move(value.Executable), ConfigPath = Move(value.ConfigPath)
        });
        Rewrite<AdapterRecord>("adapters", "json", value => value with { Executable = Move(value.Executable) });
        foreach (var executable in session.Query("SELECT executable FROM desktop WHERE id=1", r => r.GetString(0)))
            session.Execute("UPDATE desktop SET executable=$path WHERE id=1", ("$path", Move(executable)));
        CliCommand MoveCommand(CliCommand command) => command with
        {
            Executable = Move(command.Executable),
            WorkingDirectory = command.WorkingDirectory is null ? null : Move(command.WorkingDirectory)
        };
        Rewrite<CliExtensionRecord>("cli_extensions", "json", value => value with
        {
            Recipe = value.Recipe with { Create = MoveCommand(value.Recipe.Create),
                Resume = value.Recipe.Resume is null ? null : MoveCommand(value.Recipe.Resume) }
        });
        Rewrite<ArtifactRecord>("artifacts", "json", value => value with { Path = Move(value.Path) });
        foreach (var artifact in session.Query("SELECT id,json FROM artifacts", r => (Id: r.GetString(0), Value: RelayJson.Read<ArtifactRecord>(r.GetString(1)))))
            session.Execute("UPDATE artifacts SET path=$path WHERE id=$id", ("$path", artifact.Value.Path), ("$id", artifact.Id));
        Rewrite<ContinuationRecord>("continuations", "json", value => value with { OutputPath = value.OutputPath is null ? null : Move(value.OutputPath) });
        Rewrite<MigrationPlan>("migrations", "json", value => value with
        {
            OriginalPath = Move(value.OriginalPath), ProposedPath = Move(value.ProposedPath),
            BackupPath = Move(value.BackupPath), PreviewPath = Move(value.PreviewPath)
        });
        // Idempotency inputs and user prose remain byte-for-byte unchanged; only returned task paths move.
        Rewrite<TaskView>("callbacks", "result_json", value => value with { Task = MoveTask(value.Task) });
    }
}
