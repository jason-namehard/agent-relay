# Agent Relay

[English](README.md) | **简体中文**

[![CI](https://github.com/jason-namehard/agent-relay/actions/workflows/ci.yml/badge.svg)](https://github.com/jason-namehard/agent-relay/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

给同时使用多个 AI Agent 的人准备的本机交接台：把文件从 A 交给 B，看清下一步该谁处理，工作结束就停止往返。

公开下载版为 `0.1.0-preview.22`；当前源码包含重新设计的 `0.1.1` 桌面候选版。

## 快速开始 — Windows x64

公开预览版：`0.1.0-preview.22`。在 Windows PowerShell 5.1 或 PowerShell 7 中下载并完整解压到新目录：

```powershell
$version = 'v0.1.0-preview.22'
$zip = "AgentRelay-$version-windows-x64.zip"
$download = Join-Path $PWD $zip
$ProgressPreference = 'SilentlyContinue'
Invoke-WebRequest "https://github.com/jason-namehard/agent-relay/releases/download/$version/$zip" -UseBasicParsing -OutFile $download
Expand-Archive -LiteralPath $download -DestinationPath ".\AgentRelay-$version"
Set-Location ".\AgentRelay-$version"
.\gui\AgentRelay.Desktop.exe
```

也可从 [Releases](https://github.com/jason-namehard/agent-relay/releases/tag/v0.1.0-preview.22) 下载。配置真实 Agent 请运行上述正式程序；`TryDemo.cmd` 是隔离演示，会禁止真实 CLI 调用。在设置中可保留当前演示数据并转为正式使用。GUI 与通知自带运行库，CLI/MCP 需要 [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0)。目前没有安装器和代码签名，Windows 可能给出安全提示。

<details>
<summary>核对上传的 GitHub 发布资产</summary>

在同一个 PowerShell 会话中运行：

```powershell
$release = Invoke-RestMethod "https://api.github.com/repos/jason-namehard/agent-relay/releases/tags/$version"
$asset = $release.assets | Where-Object name -eq $zip
if (-not $asset.digest) { throw 'The release API did not provide an asset digest.' }
$actual = 'sha256:' + (Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $asset.digest) { throw 'The file does not match the GitHub asset.' }
```

本机构建与 CI 构建相互独立，不承诺逐字节一致。

</details>

## 发起 CLI 任务

1. **配置 A 和 B。** 在 Agent 管理使用“一键配置”或按字段手动新建；CLI 投递需要先保存真实可用的调用方案。
2. **在看板点击“新建任务”。** 选择 A、可选的来源会话（不选时交给 A 的轮次手动复制）、B，以及新建或已有的目标会话，直接填写工作要求。软件生成任务提示并安排调用，不需要先让 Agent 写交接文件或 JSON。
3. **选择协作方式。** “单次交接”由 B 完成后结束。“持续修改与验收”由你选择最终验收方，手动填写标准或让发起方拟定，并可选择是否限制验收次数。验收方列出具体待解决问题或确认无问题通过；Relay 记录其回执，不独立验证成果。可设单轮时限，并选择是否允许自动通过 CLI 投递。
4. **创建任务。** 必要时明确允许启用所需方向，再提交。实际调用可能产生模型费用。看板显示当前处理者、角色、轮次与阶段，读取工作回执后更新，后续往返精确续接同一组已绑定会话。只有该 Agent 的 CLI 已验证才会自动发送。

轮数用完、单轮超时或无进展时任务暂停，可继续（可附说明）、取消或标记完成。会话失效时需要重新选择，不会猜测最近会话；投递不确定时不会自动重试，检查后可点“确认未收到 · 允许重发”再发送一次。CLI 返回成功或文件发生变化，都不等于工作完成。见[会话选择与限制](docs/SESSIONS.md)。

<details>
<summary>手动交接已有文件</summary>

### 手动传递怎么走

1. 使用“一键配置”或手动字段登记两个 Agent。
2. 选择“新建任务 → 新建手动传递任务”，指定发送方和接收方，填写工作正文。复制完整提示词给发送方。
3. 将发送方返回的 JSON 粘贴到软件中并解析创建；也可以选择已有文件。
4. 按看板提示把下一条提示词交给当前接收方。软件运行时自动读取回执，刷新时也会补读。需要继续时按提示往返；整项任务明确完成后才结束。

Agent 正在处理时，看板会提示等待回执。同一轮 CLI 只记录一次投递；结果不确定时应先检查目标会话，再决定是否手动传递。同一任务 attempt 的后续往返复用双方准确的会话 ID；会话失效会停止自动投递并要求重新选择，不会改用“最近会话”。见[会话选择与限制](docs/SESSIONS.md)。

文件改变不能证明完成，明确回执才能。外部文件复制进受管目录，受管文件直接复用，不额外生成正文包装文件或重复结果快照。控制 JSON 放在隐藏的 `.relay` 目录。长期成果保存在项目中，过期受管文件可通过清理操作删除。

</details>

## 包含什么

| 区域 | 当前行为 |
| --- | --- |
| 任务看板 | 直接填写 CLI 工作要求或文件交接、准确会话、单次交接或持续修改与验收、暂停决定与任务操作 |
| 会话管理 | 同一 SQLite 中按任务/attempt/轮次/Agent 绑定，定向发现、展示缓存及续接前真实核实 |
| 节点录入 | 一个名称、一键直接配置、分区手动字段与本机配置接口 |
| 交接规则 | 用户批准后常驻一句话，详细说明按需读取，移除保留其他编辑 |
| 连接图 | 每对节点一条线和独立方向开关；节点亮点区分方案已登记、探针失败与缺少方案 |
| CLI 扩充 | 原生 JSON/JSONL/文本输出适配，新会话和续接探针分别核验 |
| 设置 | 中转站、保留期、桌面提醒、可选提示音、六种语言与配色 |
| Windows 托盘 | 最小化留在任务栏；× 按记住的选择进托盘或退出，托盘“退出”始终结束程序 |

GUI 与通知为 Windows x64。Core CLI/MCP 有 Windows/Linux CI 覆盖，需要 .NET 10。没有 macOS 桌面包、云端中转站，所有进程关闭后没有后台服务。CLI 输出与多数深层文档目前为中文。

### Agent 配置与规则移除

Agent 管理工具栏为“新建 Agent、编辑 Agent、删除 Agent、测试桥接、一键配置”。配置提示词让 Agent 调查帮助和非敏感设置、读取最新节点目录，再通过 `agent setup --manifest` 登记并导入可用调用方案。新建配置不会使用当前选中节点的 ID；编辑才修改已选节点。接口不能运行时按字段手动填写，不需要把整段配置回粘到 GUI。

软件负责适配受支持的原生 JSON、JSONL 与最终文本输出，不再要求宿主输出 Relay 专用回执。手动通过 CLI 发送只需已登记方案并启用方向。自动投递还要求任务允许且能力已验证：探针通过（Agent 须写出标记文件）并确认可见，或一次真实交接中 Agent 写回了回执。登录被拒、未选模型或缺少工具权限会停止该 Agent 的自动投递，处理后点击“我已处理 · 允许自动发送”。登记真实调用方案后，在连接图启用所需方向。续接要求明确绑定、执行前核实宿主记录，并检查返回的会话 ID 一致。节点绿点表示已有方案，悬停可看诊断状态；红点表示探针失败，灰点表示无方案。会话日志或仅一个 CLI 路径不等于完整调用方式。

只有调用适配器提供准确上下文时，来源身份才能自动登记。软件不让模型查找会话 ID 或修改桌面会话列表。新建会话可能仍需先在宿主中选模型；续接默认继承原对话的模型与设置，不伪造统一的模型或思考强度覆盖能力。

Zcode 的 CLI 新会话和续接内容可能受桌面缓存影响；可切换到其他会话、约 11 分钟后再切回，或保存工作后重启 Zcode。不要为刷新界面重复发送或运行付费探针；DSH 桌面桥接通常实时显示。看板分别显示发送中、桥接受阻、投递未确认和自动能力待验证，避免把未送达说成正在处理。最左侧复选框支持批量人工完成、批量删除，操作前需确认范围。

看板提供全选。受阻任务、Agent 详情和桥接测试可复制指定节点的诊断提示词，携带本机排查入口，不复制命令参数、环境值或宿主原始输出。DSH 配置检查会认证只读接口，不启动模型；历史阻塞仍由用户处理后解除，不等同于当前检查失败。

任务摘要只显示轮次和下一接收方，处理要求放在默认折叠的文件内容中。取消需确认，立即结束 Relay 跟踪和后续投递；宿主中已开始的工作可能继续。DSH 认证阻塞提供重新检测并恢复入口，检查通过才解除旧阻塞，保留能力证据，不恢复已取消任务。

Agent 桥接分三种：ACP、运行中桌面应用自己的接口（DSH 为 `dsh-desktop`）、宿主的命令行。Relay 续接对话时，发出的请求开头必须和桌面一样，即相同的工具定义和系统提示；否则供应商的提示缓存失效，每次切换都要为整段历史重新付费。

- 已知宿主已对齐：Zcode 靠启动参数，Hermes 共用同一个网关，DSH 由运行中的桌面执行回合。实测切换后命中 93–99%。
- `cli-recipe doctor` 会报告与桌面不一致的启动设置。
- `agent cache-check` 显示某个会话每次请求的缓存命中。
- 其他宿主由一键配置交给配置 Agent 处理，它会使用自带的 `relay-cache-parity` 技能。

DSH 桌面桥接用桌面自己的本机签名密钥登录，须经你同意，Relay 不保存该密钥；发送时 DSH 需要开着。

一句话规则仍占少量常驻上下文，详细指引按需读取，是否触发取决于宿主遵从。你选择实际指令文件，软件不假定每个宿主都会读 `AGENTS.md`；中转站迁移后刷新稳定指引入口。

删除便携软件前，在设置选择“移除全部交接规则”，或在解压目录使用 .NET 10：

```powershell
dotnet .\relay.dll --home <中转站目录> guidance remove --all
```

只移除 Relay 标记区块，保留其他编辑。直接删除 ZIP 或解压目录不会执行卸载清理。

<details>
<summary>CLI、MCP、开发与架构</summary>

`dotnet .\relay.dll --help` 查看 CLI。宿主以 `dotnet .\mcp\relay-mcp.dll --home <中转站目录> --agent <已登记ID>` 启动 stdio MCP，调用方身份固定；MCP 服务不实现 `--help`。

新克隆目录安装 .NET 10 SDK 后：

```powershell
git clone https://github.com/jason-namehard/agent-relay.git
cd agent-relay
dotnet restore --locked-mode
dotnet test --configuration Release --no-restore
```

Solution 覆盖 Core、CLI、MCP 和核心测试。Windows 桌面检查与打包使用 PowerShell 7：

```powershell
dotnet restore src/AgentRelay.Notify/AgentRelay.Notify.csproj --locked-mode
pwsh ./scripts/test-desktop.ps1
pwsh ./scripts/check-licenses.ps1 -IncludeDesktop
pwsh ./scripts/package.ps1 -Flavor Windows
```

脚本可传 `-Dotnet <路径>`，包输出到 `artifacts/packages`，不覆盖已有压缩包。普通测试使用本地数据，不调用付费模型。Core 负责 SQLite 账本与生命周期，CLI/MCP 和四页 WPF 界面共用；Notify 是可选 Windows 辅助程序。

[配置和回执格式](docs/CONFIGURATION_ASSISTANT.md) · [会话管理](docs/SESSIONS.md) · [UI 规格](docs/UI_SPEC.md) · [旧消息协议](docs/PROTOCOL.md) · [高级适配器](docs/ADAPTERS.md)

</details>

## 限制与验证边界

- 一键配置只登记调用方式，不会同步宿主桌面端与独立 CLI 的认证。WorkBuddy 没有对外可用的调用接口，交给它的任务需手动复制提示词（[说明](docs/SESSIONS.md#workbuddy)）。真实宿主的创建会话、可见性和续接需在该宿主中测试；模拟桥接不代表真实接通，实际探针可能收费。
- 不完整或无效回执会提示处理，保存结果本身不会完成任务。旧消息任务保留原先 CLI/MCP 协议。
- 删除任务需确认，会删除账本记录并保留文件供保留期清理。人工标记完成记录用户决定；这两项都不验证 Agent 的工作，也不终止已经运行的 Agent。
- 迁移要求没有未完成任务，包括等待对方继续的回传。文件和账本复制后再切换；旧站作为备份不自动清理，迁移校验只继续有记录的待完成迁移。
- 软件运行时直接显示标注为“应用内通知”的右下角提醒；Windows 通知中心可用时静默记录，不重复弹系统横幅。设置提供测试通知、提示音选择与试听；显示缩放仍需人工检查。
- Agent 必须能访问中转站文件，任意远程或沙盒会话不一定能读取本机目录。

更多见 [上手指南](docs/GETTING_STARTED.md)、[贡献约定](CONTRIBUTING.md)、[更新记录](CHANGELOG.md) 与 [issues](https://github.com/jason-namehard/agent-relay/issues)。报告请附版本、平台、步骤及脱敏错误。

## 许可

项目代码为 [MIT](LICENSE)。随包 .NET、Windows App SDK 等组件沿用各自条款，见 [分发条款](DISTRIBUTION_TERMS.md)、[第三方说明](THIRD_PARTY_NOTICES.md) 与 `licenses/`。
