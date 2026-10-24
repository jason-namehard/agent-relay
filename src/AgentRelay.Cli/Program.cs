using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text.Json;
using AgentRelay.Core;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.ComponentModel;

return await RelayCli.RunAsync(args);

public static class RelayCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        var root = new RootCommand("Agent Relay — 本机 Agent 任务交接与恢复");
        var home = new Option<string>("--home") { Description = "Relay 数据根目录；也可设 RELAY_HOME", Recursive = true };
        root.Options.Add(home);
        root.Options.Add(new Option<bool>("--json") { Description = "以 JSON 输出（操作命令默认已使用 JSON）", Recursive = true });

        void Add(Command parent, string name, string description, string[] specs, Func<RelayService, Values, Task<object>> action)
        {
            var command = new Command(name, description);
            var options = new Dictionary<string, Option<string>>();
            var flags = new Dictionary<string, Option<bool>>();
            foreach (var spec in specs)
            {
                var key = spec.TrimEnd('!', '?');
                if (spec.EndsWith('?'))
                {
                    var flag = new Option<bool>("--" + key); flags.Add(key, flag); command.Options.Add(flag);
                }
                else
                {
                    var option = new Option<string>("--" + key) { Required = spec.EndsWith('!') };
                    options.Add(key, option); command.Options.Add(option);
                }
            }
            command.SetAction(async (parse, cancellationToken) =>
            {
                void Emit(object value)
                {
                    if (options.TryGetValue("report-path", out var reportOption) && parse.GetValue(reportOption) is { } path)
                        File.WriteAllText(path, RelayJson.Write(value), new System.Text.UTF8Encoding(false));
                    else Print(value);
                }
                try
                {
                    var directory = parse.GetValue(home) ?? Environment.GetEnvironmentVariable("RELAY_HOME")
                        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AgentRelay");
                    var service = new RelayService(directory);
                    var result = await action(service, new Values(parse, options, flags, cancellationToken));
                    var code = result switch
                    {
                        TaskView { Task.ErrorCode: "execution_unknown" } when name is "run" or "dispatch" => 6,
                        TaskView { Task.State: TaskState.DeliveryUnknown } when name is "dispatch" or "approve" or "retry" => 6,
                        TaskView { Task.State: TaskState.AwaitingManual or TaskState.AwaitingConfirmation } when name is "dispatch" or "approve" or "retry" => 4,
                        TaskView { Task.State: TaskState.Failed } when name is "dispatch" or "approve" or "retry" => 1,
                        AgentRecord { ValidationStatus: "invalid" } => 3,
                        _ => 0
                    };
                    var view = result as TaskView;
                    object? operationError = code == 1 && view is not null
                        ? new { code = view.Task.ErrorCode, message = view.Task.ErrorMessage } : null;
                    Emit(new { relay_version = 1, ok = code is 0 or 4 or 6, task_id = view?.Task.TaskId,
                        state = view?.Task.State, next_action = view?.NextAction, data = result, error = operationError });
                    return code;
                }
                catch (RelayException error) { return Error(error.Code, error.Message, error.ExitCode, Emit); }
                catch (IOException error) { return Error("file_error", error.Message, 1, Emit); }
                catch (UnauthorizedAccessException error) { return Error("access_denied", error.Message, 3, Emit); }
                catch (JsonException error) { return Error("invalid_json", error.Message, 2, Emit); }
                catch (ArgumentException error) { return Error("invalid_argument", error.Message, 2, Emit); }
                catch (SqliteException error) { return Error("database_error", error.Message, 1, Emit); }
                catch (Win32Exception error) { return Error("process_start_failed", error.Message, 3, Emit); }
            });
            parent.Subcommands.Add(command);
        }
        void Sync(Command parent, string name, string description, string[] specs, Func<RelayService, Values, object> action) =>
            Add(parent, name, description, specs, (service, values) => Task.FromResult(action(service, values)));
        Command Group(string name, string description)
        {
            var command = new Command(name, description); root.Subcommands.Add(command); return command;
        }

        Sync(root, "init", "创建独立数据目录和 SQLite 账本", [], (s, _) => { s.Initialize(); return new { home = s.Home }; });
        var agent = Group("agent", "Agent 注册与只读发现");
        Sync(agent, "discover", "列出候选路径；不登记、不修改配置", [], (s, _) => s.Discover());
        Sync(agent, "list", "列出已登记 Agent", [], (s, _) => s.Agents());
        Sync(agent, "register", "登记或手工修正路径（需要重新 validate）",
            ["id!", "host", "config-path!", "cli-path", "host-version", "instruction-path"],
            (s, v) => s.Register(v.Get("id"), v.Optional("host") ?? "manual", v.Get("config-path"),
                v.Optional("cli-path"), v.Optional("host-version"), v.Optional("instruction-path") is { } path ? [path] : []));
        Sync(agent, "validate", "验证已登记路径存在、可读；不启动宿主", ["id!"], (s, v) => s.ValidateAgent(v.Get("id")));
        Add(agent, "verify-executor", "验证 Codex 原生 CLI 版本及非交互契约，不调用模型", ["id!"],
            async (s, v) => await s.VerifyExecutorAsync(v.Get("id"), v.CancellationToken));
        Sync(agent, "executors", "显示已验证的执行器", [], (s, _) => s.Executors());
        Add(agent, "verify-resumer", "验证精确会话继续接口；exec-resume 会启动模型轮次", ["id!", "kind!"],
            async (s, v) => await s.VerifyResumerAsync(v.Get("id"), v.Get("kind"), v.CancellationToken));
        Sync(agent, "resumers", "显示已验证的会话继续适配器", [], (s, _) => s.Resumers());

        var permission = Group("permission", "有向通信权限，未配置默认拒绝");
        Sync(permission, "set", "只改变指定方向", ["from!", "to!", "decision!"], (s, v) => s.SetPermission(v.Get("from"), v.Get("to"), v.Get("decision")));
        Sync(permission, "list", "显示显式权限", [], (s, _) => s.Permissions());

        var settings = Group("settings", "Relay 自身配置");
        Sync(settings, "show", "显示当前保留和时限配置", [], (s, _) => s.Settings());
        Sync(settings, "set", "更新新任务的默认配置", ["ttl-days", "task-timeout-minutes", "delivery-timeout-seconds", "max-delivery-attempts"],
            (s, v) =>
            {
                var current = s.Settings();
                return s.Configure(new RelaySettings(v.Number("ttl-days", current.ArtifactTtlDays), v.Number("task-timeout-minutes", current.TaskTimeoutMinutes),
                    v.Number("delivery-timeout-seconds", current.DeliveryTimeoutSeconds), v.Number("max-delivery-attempts", current.MaxDeliveryAttempts)));
            });

        var adapter = Group("adapter", "显式验证可信的外部适配器");
        Sync(adapter, "inspect-config", "只读盘点指定 JSON/TOML 的 MCP 命令；不显示 env 值或执行命令", ["path!", "format!"],
            (s, v) => s.InspectConfiguration(v.Get("path"), v.Get("format")));
        Sync(adapter, "sources", "列出迁移登记的来源、版本和声明能力", [], (s, _) => s.AdapterSources());
        Sync(adapter, "list", "查看验证记录", [], (s, _) => s.Adapters());
        Add(adapter, "verify", "在隔离目录运行 AMQ init/send/drain，不启动 AI", ["id!", "executable!"], async (s, v) =>
        {
            if (v.Get("id") != "amq-cli") throw new RelayException("unsupported_adapter", "当前只支持 amq-cli。");
            return await s.VerifyAmqAsync(v.Get("executable"), v.CancellationToken);
        });

        var migration = Group("migration", "配置迁移预览、批准、验证与回滚");
        Sync(migration, "plan", "按唯一精确片段生成提案；不修改目标配置", ["manifest!"], (s, v) =>
        {
            var options = new JsonSerializerOptions(RelayJson.Options) { RespectRequiredConstructorParameters = true,
                RespectNullableAnnotations = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
            var request = JsonSerializer.Deserialize<MigrationRequest>(File.ReadAllText(v.Get("manifest")), options)
                ?? throw new JsonException("Expected migration manifest.");
            var plan = s.PlanMigration(request);
            return new { plan, preview = File.ReadAllText(plan.PreviewPath) };
        });
        Sync(migration, "show", "展示已保存的精确提案与备份位置", ["id!"], (s, v) =>
        {
            var plan = s.Migration(v.Get("id")); return new { plan, preview = File.ReadAllText(plan.PreviewPath) };
        });
        Sync(migration, "list", "列出迁移状态", [], (s, _) => s.Migrations());
        Sync(migration, "apply", "审阅后明确批准应用；配置变化则停止", ["id!", "approve?", "reason"],
            (s, v) => s.ApplyMigration(v.Get("id"), v.Flag("approve"), v.Optional("reason") ?? ""));
        Sync(migration, "validate", "以该 route-id 的派发、完成和继续证据验证新入口", ["id!", "task!"],
            (s, v) => s.ValidateMigration(v.Get("id"), v.Get("task")));
        Sync(migration, "rollback", "明确批准回滚；后续修改不能被备份覆盖", ["id!", "approve?", "reason"],
            (s, v) => s.RollbackMigration(v.Get("id"), v.Flag("approve"), v.Optional("reason") ?? ""));
        Sync(migration, "recover", "只核对中断的应用/回滚状态，不自动写目标配置", ["id!"], (s, v) => s.RecoverMigration(v.Get("id")));
        Sync(migration, "audit", "迁移批准、应用、验证与回滚历史", ["id!"], (s, v) => s.MigrationAudit(v.Get("id")));

        var task = Group("task", "任务派发、显式回调与恢复");
        Add(task, "dispatch", "保存交接并立即返回；execute 启动有界单次 worker", ["from!", "to!", "request!", "mode", "adapter", "idempotency-key!", "deadline-at", "origin-session", "execute?", "execution-timeout-seconds"], async (s, v) =>
        {
            OriginSession? origin = v.Optional("origin-session") is { } path ? RelayJson.Read<OriginSession>(File.ReadAllText(path)) : null;
            var executionTimeout = v.ExecutionTimeout("execution-timeout-seconds");
            if (v.Flag("execute"))
            {
                if (v.Optional("mode") is { } mode && mode != "auto")
                    throw new RelayException("invalid_execution_mode", "dispatch --execute 仅用于 auto；confirm 模式请在 approve 时显式使用 --execute。");
                s.RequireExecutorReady(v.Get("to"));
            }
            var dispatched = await s.DispatchAsync(new DispatchRequest(v.Get("from"), v.Get("to"), File.ReadAllText(v.Get("request")),
                v.Optional("mode") ?? "auto", v.Optional("adapter") ?? (v.Flag("execute") ? "amq-cli" : s.PreferredDeliveryAdapter(v.Get("to"))), v.Get("idempotency-key"), v.Timestamp("deadline-at"), origin), v.CancellationToken);
            if (v.Flag("execute") && dispatched.Task.State == TaskState.Dispatched)
                LaunchExecution(s, dispatched.Task, executionTimeout);
            return s.Status(dispatched.Task.TaskId);
        });
        Add(task, "run", "领取一次 AMQ 任务并运行已验证 Codex；不轮询", ["task!", "attempt!", "sender!", "timeout-seconds", "report-path"],
            async (s, v) => await s.RunTaskAsync(v.Get("task"), v.Get("attempt"), v.Get("sender"),
                v.ExecutionTimeout("timeout-seconds"), cancellationToken: v.CancellationToken));
        Sync(task, "execution", "只读显示执行、会话和 worker 证据", ["task!", "attempt"],
            (s, v) => (object?)s.ExecutionEvidence(v.Get("task"), v.Optional("attempt")) ?? new { message = "尚无执行记录。" });
        Sync(task, "status", "只读状态，不触发投递或时限处理", ["task!"], (s, v) => s.Status(v.Get("task")));
        Sync(task, "list", "只读列出任务", [], (s, _) => s.Tasks());
        Add(task, "approve", "批准待确认任务；可显式启动 worker", ["task!", "execute?", "execution-timeout-seconds"], async (s, v) =>
        {
            var executionTimeout = v.ExecutionTimeout("execution-timeout-seconds");
            if (v.Flag("execute")) s.RequireExecutorReady(s.Status(v.Get("task")).Task.RecipientId);
            var approved = await s.ApproveAsync(v.Get("task"), v.CancellationToken);
            if (v.Flag("execute") && approved.Task.State == TaskState.Dispatched)
                LaunchExecution(s, approved.Task, executionTimeout);
            return s.Status(approved.Task.TaskId);
        });
        Sync(task, "ack", "接收方确认当前尝试", ["task!", "attempt!", "sender!", "idempotency-key!"],
            (s, v) => s.Acknowledge(v.Get("task"), v.Get("attempt"), v.Get("sender"), v.Get("idempotency-key")));
        Sync(task, "start", "接收方报告开始执行", ["task!", "attempt!", "sender!", "idempotency-key!"],
            (s, v) => s.Acknowledge(v.Get("task"), v.Get("attempt"), v.Get("sender"), v.Get("idempotency-key"), start: true));
        foreach (var failure in new[] { false, true })
        {
            var isFailure = failure;
            Add(task, failure ? "fail" : "complete", failure ? "提交失败报告" : "提交成功结果；文件更新本身不表示完成",
                ["task!", "attempt!", "sender!", "idempotency-key!", "status!", "result!", "error-code", "message", "side-effects-possible?"], async (s, v) =>
                {
                    if (v.Get("status") != (isFailure ? "failed" : "succeeded")) throw new RelayException("invalid_status", "status 与回调命令不一致。");
                    s.Complete(v.Get("task"), v.Get("attempt"), v.Get("sender"), v.Get("idempotency-key"), v.Get("result"),
                        isFailure, v.Optional("error-code"), v.Optional("message"), v.Flag("side-effects-possible") ? true : null);
                    await s.ProcessContinuationsAsync(v.Get("task"), cancellationToken: v.CancellationToken);
                    return s.Status(v.Get("task"));
                });
        }
        Sync(task, "cancel", "停止 Relay 等待和后续投递，不终止宿主", ["task!", "reason!"], (s, v) => s.Cancel(v.Get("task"), v.Get("reason")));
        Add(task, "retry", "明确批准后创建新执行尝试；可能重复副作用", ["task!", "reason!", "approve?"],
            async (s, v) => await s.RetryAsync(v.Get("task"), v.Get("reason"), v.Flag("approve"), v.CancellationToken));
        Sync(task, "evidence", "只读显示运输凭据", ["task!"], (s, v) => (object?)s.DeliveryEvidence(v.Get("task")) ?? new { message = "无运输 outbox；可能为手工模式。" });
        Sync(task, "continuations", "只读显示继续提交和桌面通知证据", ["task!"], (s, v) => s.Continuations(v.Get("task")));
        Add(task, "continue", "处理尚未提交的继续消息；不会重发 submitted/unknown", ["task!", "timeout-seconds"], async (s, v) =>
        {
            await s.ProcessContinuationsAsync(v.Get("task"), timeoutSeconds: v.ExecutionTimeout("timeout-seconds"), cancellationToken: v.CancellationToken);
            return s.Status(v.Get("task"));
        });

        var artifact = Group("artifact", "登记受管产物；不会完成任务");
        Sync(artifact, "register", "把外部文件导入当前任务的受管目录", ["task!", "attempt!", "kind!", "path!"],
            (s, v) => s.RegisterArtifact(v.Get("task"), v.Get("attempt"), v.Get("kind"), v.Get("path")));
        Sync(artifact, "list", "显示产物及过期时间", ["task!"], (s, v) => s.Artifacts(v.Get("task")));
        var inbox = Group("inbox", "持久通知");
        Sync(inbox, "list", "列出等待、就绪和失败通知", [], (s, _) => s.Inbox());
        var audit = Group("audit", "事件历史");
        Sync(audit, "list", "按提交顺序显示事件", ["task"], (s, v) => s.Audit(v.Optional("task")));
        var prompt = Group("prompt", "手工交接和继续提示词");
        Sync(prompt, "show", "返回可复制提示词", ["task!"], (s, v) => new { text = s.Status(v.Get("task")).Prompt });
        foreach (var operation in new[] { "copy", "open" })
        {
            var op = operation;
            Add(prompt, op, op == "copy" ? "把继续提示词复制到 Windows 剪贴板" : "打开结果与继续提示词窗口", ["task!"], async (s, v) =>
            {
                var executable = s.DesktopExecutable() ?? throw new RelayException("desktop_unconfigured", "请先 desktop configure。", 3);
                s.Status(v.Get("task"));
                if (op == "open")
                {
                    var start = new ProcessStartInfo(executable) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Normal };
                    foreach (var arg in new[] { "--open", "--home", s.Home, "--task", v.Get("task") }) start.ArgumentList.Add(arg);
                    using var process = Process.Start(start)!;
                    return new { status = "opened", task_id = v.Get("task") };
                }
                var result = await RelayProcess.RunAsync(executable, ["--copy", "--home", s.Home, "--task", v.Get("task")], null, TimeSpan.FromSeconds(15), v.CancellationToken);
                if (result.TimedOut || result.ExitCode != 0) throw new RelayException("clipboard_failed", result.Error, 3);
                return new { status = "copied", task_id = v.Get("task") };
            });
        }
        var desktop = Group("desktop", "Windows 本地通知与继续窗口");
        Add(desktop, "configure", "验证并登记通知程序；首次提醒时注册应用点击激活", ["executable!"],
            async (s, v) => await s.ConfigureDesktopAsync(v.Get("executable"), v.CancellationToken));
        Sync(desktop, "show", "显示通知程序路径", [], (s, _) => new { executable = s.DesktopExecutable() });
        Sync(desktop, "disable", "停止此数据目录的后续桌面提醒", [], (s, _) => { s.DisableDesktop(); return new { status = "disabled" }; });
        Add(root, "recover", "处理超时和遗留 outbox；未知投递不自动重发", ["task"], async (s, v) => await s.RecoverAsync(v.Optional("task"), v.CancellationToken));
        Sync(root, "cleanup", "仅清理过期受管产物，默认预览", ["dry-run?", "apply?"], (s, v) =>
        {
            if (v.Flag("dry-run") && v.Flag("apply")) throw new RelayException("conflicting_options", "dry-run 与 apply 不能同时使用。");
            return s.Cleanup(v.Flag("apply"));
        });

        var parsed = root.Parse(args);
        if (parsed.Errors.Count > 0) return Error("invalid_arguments", string.Join(" ", parsed.Errors.Select(e => e.Message)), 2);
        return await parsed.InvokeAsync();
    }

    private static void Print(object value) => Console.WriteLine(RelayJson.Write(value));
    private static int Error(string code, string message, int exitCode, Action<object>? emit = null)
    {
        (emit ?? Print)(new { relay_version = 1, ok = false, task_id = (string?)null, state = (string?)null,
            next_action = (string?)null, data = (object?)null, error = new { code, message } });
        return exitCode;
    }

    private static void LaunchExecution(RelayService service, TaskRecord task, int timeoutSeconds)
    {
        var report = Path.Combine(Path.GetDirectoryName(task.RequestPath)!, "worker-report.json");
        DetachedWorker.Launch(service, task, timeoutSeconds, ["--home", service.Home, "task", "run", "--task", task.TaskId,
            "--attempt", task.AttemptId, "--sender", task.RecipientId, "--timeout-seconds", timeoutSeconds.ToString(CultureInfo.InvariantCulture), "--report-path", report]);
    }

    private sealed record Values(ParseResult Parse, Dictionary<string, Option<string>> Options,
        Dictionary<string, Option<bool>> Flags, CancellationToken CancellationToken)
    {
        public string Get(string key) => Parse.GetValue(Options[key])!;
        public string? Optional(string key) => Parse.GetValue(Options[key]);
        public bool Flag(string key) => Parse.GetValue(Flags[key]);
        public int Number(string key, int defaultValue)
        {
            if (Optional(key) is not { } value) return defaultValue;
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                throw new RelayException("invalid_number", $"{key} 必须为正整数。");
            return number;
        }
        public int ExecutionTimeout(string key)
        {
            var seconds = Number(key, 180);
            if (seconds is < 1 or > 1800) throw new RelayException("invalid_execution_timeout", "执行时限必须为 1–1800 秒。");
            return seconds;
        }
        public DateTimeOffset? Timestamp(string key)
        {
            if (Optional(key) is not { } value) return null;
            if (!DateTimeOffset.TryParseExact(value, ["yyyy-MM-dd'T'HH:mm:ss'Z'", "O"], CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var result))
                throw new RelayException("invalid_timestamp", $"{key} 必须为 UTC RFC3339 时间，例如 2026-10-22T10:30:00Z。");
            return result.ToUniversalTime();
        }
    }
}
