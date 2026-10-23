# Agent Relay — 从演示到第一次交接

## 先选择下载包

从 [GitHub Releases](https://github.com/jason-namehard/agent-relay/releases) 下载 `v0.1.0-preview.2`：

| 包 | 适用场景 | 运行要求 |
| --- | --- | --- |
| `AgentRelay-v0.1.0-preview.2-windows-x64.zip` | Windows 桌面 GUI、演示、CLI、MCP、通知 | GUI/演示/通知自带运行库；CLI/MCP 需要 .NET 10 runtime |
| `AgentRelay-v0.1.0-preview.2-cli-mcp.zip` | Windows/Linux 的命令行或 stdio MCP | .NET 10 runtime；不包含桌面组件 |

`Source code.zip` 是源码，不是应用分发包。Windows 完整解压到固定目录，不在 ZIP 浏览器里运行 exe。桌面包含微软运行库与通知组件，请保留 `licenses/` 并阅读 `DISTRIBUTION_TERMS.md`。尚未提供安装器和代码签名。

## 不用账号先试演示

Windows 包双击 `TryDemo.cmd`，或运行：

```powershell
./gui/AgentRelay.Desktop.exe --demo
```

演示窗口明确显示 `DEMO`，数据存入新建的 `%LOCALAPPDATA%/AgentRelay.Demos/<UUID>/`。它包含三位虚构 Agent、一个成功样本、一个失败样本和一个待手工交接任务，**未调用 AI，也未读取认证或现用 Agent 配置**。

按这个顺序体验：

1. Home 查看待处理任务，选择成功样本后 `Copy Prompt`。
2. Connections 查看 A→B 允许、B→A 明确拒绝和 Agent C 未验证的差别。
3. Inbox 选择待交接任务，查看请求、尝试和受管结果路径。
4. Audit 查看显式完成与失败事件，确认文件变化并不等于任务完成。

关闭演示后，可删除底部所示的独立演示目录。每次 `--demo` 创建新目录；显式 `--home` 必须指向尚不存在的路径。普通空白 Home 也提供 `Open Demo`，不会把演示写入正式账本。

## 接入自己的 Agent

窗口右上角可直接选择简体中文等六种语言。在设置中选择莫兰迪配色和强调色。若不清楚字段值，点击输入框旁复制图标，把提示词交给目标 Agent；将它的固定 JSON 回复粘贴到页面顶部“配置助手”，点击解析代填，再审阅和登记。Agent ID 是本机 Relay 标签，不需要登录账号。详细格式见 [配置助手与外观](https://github.com/jason-namehard/agent-relay/blob/v0.1.0-preview.2/docs/CONFIGURATION_ASSISTANT.md)。

直接运行 `gui/AgentRelay.Desktop.exe`，不加 `--demo`，进入正式账本。默认 `%LOCALAPPDATA%/AgentRelay`；CLI/MCP 必须使用相同的 `--home` 或 `RELAY_HOME`。

在 Agents 登记两个稳定 ID、真实宿主与准确配置路径，单独 Validate。在 Connections 明确允许 A→B。仅有路径验证不代表执行器、认证或会话继续已验证；默认从 `manual` 交接开始。

安装 [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0) 后，在解压目录创建 `request.md` 写入真实任务，再运行：

```powershell
$relay = Join-Path $PWD 'relay.dll'
$dataRoot = Join-Path $env:LOCALAPPDATA 'AgentRelay'
$handoff = dotnet $relay --home $dataRoot task dispatch --from agent-a --to agent-b --request ./request.md --mode manual --idempotency-key first-handoff | ConvertFrom-Json
$task = $handoff.data.task
$handoff.data.prompt
```

手工派发返回退出码 `4`，表示已保存且需要人工投递。把响应中的请求提示词交给 B，然后 A 结束当前轮。B 写入 `$task.result_path` 后显式回调：

```powershell
dotnet $relay --home $dataRoot task ack --task $task.task_id --attempt $task.attempt_id --sender agent-b --idempotency-key first-ack
dotnet $relay --home $dataRoot task complete --task $task.task_id --attempt $task.attempt_id --sender agent-b --status succeeded --result $task.result_path --idempotency-key first-done
```

回到 GUI 的 Inbox，查看 `Succeeded`、结果和继续提示词。如果宿主不支持自动继续，就复制提示词交回 A；不要重复派发。同一次请求重试命令时保持原幂等键。

## 自动化接入与排错

真实 AMQ/Codex 执行、原会话继续和 MCP 配置迁移分别按 [项目 README](https://github.com/jason-namehard/agent-relay#readme)、[Phase 3](https://github.com/jason-namehard/agent-relay/blob/main/docs/PHASE3.md)、[Phase 4](https://github.com/jason-namehard/agent-relay/blob/main/docs/PHASE4.md) 操作。验证、登记不会自动修改宿主配置；应用迁移须先审阅批准。

| 现象 | 下一步 |
| --- | --- |
| CLI 提示缺少 .NET runtime | 安装 .NET 10 runtime；GUI 能启动不代表 CLI 所需 runtime 已全局安装 |
| Agent `invalid` 或连接 Gray | 在 Agents 检查原登记路径，纠正后重新 Validate |
| `awaiting_manual` / 退出码 4 | 复制请求给 B，等待显式完成，不重新派发 |
| `delivery_unknown` / 退出码 6 | 查看运输证据，不自动重发 |
| 结果已过期 | 查看任务记录和审计，决定是否重新生成 |
| GUI 状态看似未更新 | 使用 Refresh；它只读，不调用模型或重新投递 |

当前没有常驻服务或系统调度器。所有进程关闭后的截止时间在下次 Recover 处理；取消 Relay 任务不终止已运行的宿主。提问与缺陷报告请附应用版本、Windows/.NET 版本、复现步骤和已脱敏的错误码，不上传认证文件、完整宿主配置或迁移备份。
