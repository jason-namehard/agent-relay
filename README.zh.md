# Agent Relay

[English](README.md) | **简体中文**

[![CI](https://github.com/jason-namehard/agent-relay/actions/workflows/ci.yml/badge.svg)](https://github.com/jason-namehard/agent-relay/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

给同时使用多个 AI Agent 的人准备的本机交接台：把文件从 A 交给 B，看清下一步该谁处理，工作结束就停止往返。

## 快速开始 — Windows x64

公开预览版：`0.1.0-preview.12`。在 Windows PowerShell 5.1 或 PowerShell 7 中下载并完整解压到新目录：

```powershell
$version = 'v0.1.0-preview.12'
$zip = "AgentRelay-$version-windows-x64.zip"
$download = Join-Path $PWD $zip
$ProgressPreference = 'SilentlyContinue'
Invoke-WebRequest "https://github.com/jason-namehard/agent-relay/releases/download/$version/$zip" -UseBasicParsing -OutFile $download
Expand-Archive -LiteralPath $download -DestinationPath ".\AgentRelay-$version"
Set-Location ".\AgentRelay-$version"
.\gui\AgentRelay.Desktop.exe
```

也可从 [Releases](https://github.com/jason-namehard/agent-relay/releases/tag/v0.1.0-preview.12) 下载。配置真实 Agent 请运行上述正式程序；`TryDemo.cmd` 是隔离演示，会禁止真实 CLI 调用。在设置中可保留当前演示数据并转为正式使用。GUI 与通知自带运行库，CLI/MCP 需要 [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0)。目前没有安装器和代码签名，Windows 可能给出安全提示。

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

## 一次文件交接怎么走

1. **配置 A 和 B。** 在 Agent 管理点击“一键配置”，把已复制的提示词粘贴到要登记的 Agent 的新对话窗口，让它调查并直接登记。每次提示词都有独立登记请求。也可点“新建 Agent”按字段手动填写，只有一个名称必填。
2. **按需安装交接规则。** 选择宿主实际加载的指令文件，检查一句话规则并确认。详细说明只在需要时读取，已有会话可能要重新读取指令。
3. **让 A 写文件给 B。** 安装规则后，A 可输出短交接 JSON；未安装时，把新建交接中复制的格式附在你的要求后面。粘贴 JSON，点击“解析并创建交接”，或手动选择文件。
4. **选择 B 的对话。** 已配置 CLI 时，可以新建会话，或明确“选择已有会话”。选择器只刷新当前 Agent，不会自动替你选中第一项。剪贴板交接无法自动知道 A 的来源会话；要让后续 CLI 回传到那里，可明确选择一次。
5. **把提示交给 B。** 已登记调用方案且 A → B 方向已启用时，软件通过 CLI 发送；否则点击“复制提示词给 B”。短提示指向文件、工作要求和回执模板。
6. **B 写回执。** 软件打开时通过回执文件事件更新任务，启动和刷新也会补读。这条流程无需用户再导入已完成回稿。
7. **确有工作才继续传递。** `done` 且 `needs_reply: false` 就结束。需要对方继续时必须给出真实回稿和具体 `next_action`，软件按已绑定的准确会话和方向继续，或提示复制给对方；最多四轮，达到上限后停止，由人判断。

Agent 正在处理时，看板会提示等待回执。同一轮 CLI 只记录一次投递；结果不确定时应先检查目标会话，再决定是否手动传递。同一任务 attempt 的后续往返复用双方准确的会话 ID；会话失效会停止自动投递并要求重新选择，不会改用“最近会话”。见[会话选择与限制](docs/SESSIONS.md)。

文件改变不能证明完成，明确回执才能。外部文件复制进受管目录，受管文件直接复用，不额外生成正文包装文件或重复结果快照。控制 JSON 放在隐藏的 `.relay` 目录。长期成果保存在项目中，过期受管文件可通过清理操作删除。

## 包含什么

| 区域 | 当前行为 |
| --- | --- |
| 任务看板 | 手动或已启用 CLI 文件交接、明确选择会话、等待者与任务操作 |
| 会话管理 | 同一 SQLite 中按任务/attempt/轮次/Agent 绑定，定向发现、展示缓存及续接前真实核实 |
| 节点录入 | 一个名称、一键直接配置、分区手动字段与本机配置接口 |
| 交接规则 | 用户批准后常驻一句话，详细说明按需读取，移除保留其他编辑 |
| 连接图 | 每对节点一条线和独立方向开关；节点亮点区分方案已登记、探针失败与缺少方案 |
| CLI 扩充 | 原生 JSON/JSONL/文本输出适配，新会话和续接探针分别核验 |
| 设置 | 中转站、保留期、通知、六种语言、四套配色与强调色 |
| Windows 托盘 | 首次关闭询问一次；设置记住关闭到托盘或直接退出，托盘“退出”始终结束程序 |

GUI 与通知为 Windows x64。Core CLI/MCP 有 Windows/Linux CI 覆盖，需要 .NET 10。没有 macOS 桌面包、云端中转站，所有进程关闭后没有后台服务。CLI 输出与多数深层文档目前为中文。

### Agent 配置与规则移除

Agent 管理工具栏为“新建 Agent、编辑 Agent、删除 Agent、Agent CLI 测试、一键配置”。配置提示词让 Agent 调查帮助和非敏感设置、读取最新节点目录，再通过 `agent setup --manifest` 登记并导入可用调用方案。新建配置不会使用当前选中节点的 ID；编辑才修改已选节点。接口不能运行时按字段手动填写，不需要把整段配置回粘到 GUI。

软件负责适配受支持的原生 JSON、JSONL 与最终文本输出，不再要求宿主输出 Relay 专用回执。短探针与可见性确认是可选诊断，不再作为投递前提；登记真实调用方案后，在连接图启用所需方向。续接要求明确绑定、执行前核实宿主记录，并检查返回的会话 ID 一致。节点绿点表示已有方案，悬停可看诊断状态；红点表示探针失败，灰点表示无方案。会话日志或仅一个 CLI 路径不等于完整调用方式。

只有调用适配器提供准确上下文时，来源身份才能自动登记。软件不让模型查找会话 ID 或修改桌面会话列表。新建会话可能仍需先在宿主中选模型；续接默认继承原对话的模型与设置，不伪造统一的模型或思考强度覆盖能力。

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

- 真实宿主的创建会话、可见性和续接需在该宿主中测试；模拟桥接不代表真实接通，实际探针可能收费。
- 不完整或无效回执会提示处理，保存结果本身不会完成任务。旧消息任务保留原先 CLI/MCP 协议。
- 删除任务需确认，会删除账本记录并保留文件供保留期清理。人工标记完成记录用户决定；这两项都不验证 Agent 的工作，也不终止已经运行的 Agent。
- 迁移要求没有未完成任务，包括等待对方继续的回传。文件和账本复制后再切换；旧站作为备份不自动清理，迁移校验只继续有记录的待完成迁移。
- 通知保存有自动测试，系统通知中心弹出和 125%/150% 缩放仍需人工验证。
- Agent 必须能访问中转站文件，任意远程或沙盒会话不一定能读取本机目录。

更多见 [上手指南](docs/GETTING_STARTED.md)、[贡献约定](CONTRIBUTING.md)、[更新记录](CHANGELOG.md) 与 [issues](https://github.com/jason-namehard/agent-relay/issues)。报告请附版本、平台、步骤及脱敏错误。

## 许可

项目代码为 [MIT](LICENSE)。随包 .NET、Windows App SDK 等组件沿用各自条款，见 [分发条款](DISTRIBUTION_TERMS.md)、[第三方说明](THIRD_PARTY_NOTICES.md) 与 `licenses/`。
