# Agent Relay 发布包

CLI 需要 .NET 10 runtime；Windows x64 的 `desktop/AgentRelay.Notify.exe` 自带所需运行库。AMQ 与 Codex CLI 由使用者单独安装并登记，不包含在包内。

在解压目录运行 PowerShell：

```powershell
$relay = Join-Path $PWD 'relay.dll'
$dataRoot = Join-Path $PWD '.relay'
dotnet $relay --home $dataRoot init
dotnet $relay --help

# Windows 可选桌面通知；首次发送时登记该程序的当前用户点击激活入口。
dotnet $relay --home $dataRoot desktop configure --executable ./desktop/AgentRelay.Notify.exe
```

按 [项目使用文档](https://github.com/jason-namehard/agent-relay#readme) 登记 Agent、验证路径、授权方向，再选择手工交接或单次 Codex worker。原生 CLI 路径必须是明确的可执行文件；路径验证不修改 Agent 配置。

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

应用代码为 MIT。使用或再分发 Windows 辅助程序前，请阅读 `DISTRIBUTION_TERMS.md`、`THIRD_PARTY_NOTICES.md` 和 `licenses/` 中各组件原始条款。
