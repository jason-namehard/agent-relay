# Agent Relay 发布包

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

按 [项目使用文档](https://github.com/jason-namehard/agent-relay#readme) 登记 Agent、验证路径、授权方向，再选择手工交接或单次 Codex worker。原生 CLI 路径必须是明确的可执行文件；路径验证不修改 Agent 配置。

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

GUI 的 Agents、Connections、Inbox、Adapters、Audit 和 Settings 共用核心操作。启动恢复可能处理已登记的待投递和继续操作；Refresh 只读。迁移应用、回滚、重试及适配器命令验证会显示影响并要求明确批准，详见 [Phase 5 指南](https://github.com/jason-namehard/agent-relay/blob/main/docs/PHASE5.md)。

应用代码为 MIT。使用或再分发 Windows 组件前，请阅读 `DISTRIBUTION_TERMS.md`、`THIRD_PARTY_NOTICES.md` 和 `licenses/` 中各组件原始条款。
