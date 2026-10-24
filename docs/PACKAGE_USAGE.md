# Agent Relay 发布包

首次使用见 `GETTING_STARTED.md`。Windows 完整包双击 `TryDemo.cmd` 可启动隔离演示，无需模型账号；DEMO 使用预设结果，不代表真实 Agent 执行。正式模式直接运行 `gui/AgentRelay.Desktop.exe`。

右上角选择界面语言；设置中更换配色。Agent 管理中点击“添加 Agent”，可只填写自选名称，也可让 Agent 帮忙生成配置并解析代填。任务看板的“新建交接”选择发送方、接收方和内容，支持让 Agent 生成后解析。解析仅填写草稿，保存或发送由用户确认。操作见 `CONFIGURATION_ASSISTANT.md`。

CLI 与 `mcp/relay-mcp.dll` 需要 .NET 10 runtime；Windows x64 的 `gui/AgentRelay.Desktop.exe` 和 `desktop/AgentRelay.Notify.exe` 自带所需运行库。AMQ 与 Codex CLI 由使用者单独安装并登记，不包含在包内。

在解压目录运行 PowerShell：

```powershell
$relay = Join-Path $PWD 'relay.dll'
$dataRoot = Join-Path $PWD '.relay'
dotnet $relay --home $dataRoot init
dotnet $relay --help

# 原生 Windows 管理界面，与 CLI 指定同一数据目录。
./gui/AgentRelay.Desktop.exe --home $dataRoot

# Windows 可选桌面通知；首次发送时登记该程序的当前用户点击激活入口。
dotnet $relay --home $dataRoot desktop configure --executable ./desktop/AgentRelay.Notify.exe
```

首次体验建议添加两个自选名称的 Agent，使用手工交接，无需宿主配置文件。自动通知可在设置的高级“CLI 接口扩充”导入已核实的调用方案，再到 Agent 管理测试 CLI。真实探针可能产生模型费用；只有回执通过并由用户确认目标会话可见，才能启用对应方向。

stdio MCP 由宿主启动，使用 `dotnet ./mcp/relay-mcp.dll --home <数据目录> --agent <已登记Agent>`；每个实例绑定一个 Agent。JSON/TOML 配置接入按 [Phase 4 迁移指南](https://github.com/jason-namehard/agent-relay/blob/main/docs/PHASE4.md) 先 plan/show，审阅后 apply；旧入口独立验证与批准，回滚拒绝覆盖后续修改。升级已有数据目录先运行一次 `init` 创建新增表。

[精确会话恢复指南](https://github.com/jason-namehard/agent-relay/blob/main/docs/PHASE3.md) 说明 `codex-queue`、`codex-exec-resume` 和 `origin.json`。没有原会话能力时，Inbox 保留结果与继续提示词：

```powershell
dotnet $relay --home $dataRoot inbox list
dotnet $relay --home $dataRoot task list
dotnet $relay --home $dataRoot prompt open --task <任务UUID>
dotnet $relay --home $dataRoot prompt copy --task <任务UUID>
dotnet $relay --home $dataRoot recover
dotnet $relay --home $dataRoot cleanup --dry-run
```

保留解压目录的位置，使 Windows 通知仍能启动对应程序。停止本数据目录的提醒可运行 `desktop disable`；移动或删除程序前，在原位置运行 `./desktop/AgentRelay.Notify.exe --unregister` 移除应用注册。没有注册系统计划任务或服务；所有进程关闭后的截止时间在下次 `recover` 处理。

GUI 包含任务看板、Agent 管理、连接图、设置四页。交接记录在任务详情展开；旧版高级配置迁移仍可通过 CLI 操作。启动与刷新只读取记录。连接图每个方向独立控制：绿色已启用、红色暂停或尚未启用、灰色虚线缺少已验证方案。手动交接不受 CLI 线路开关影响。

应用代码为 MIT。使用或再分发 Windows 组件前，请阅读 `DISTRIBUTION_TERMS.md`、`THIRD_PARTY_NOTICES.md` 和 `licenses/` 中各组件原始条款。
