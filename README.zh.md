# Agent Relay

[English](README.md) | **简体中文**

[![CI](https://github.com/jason-namehard/agent-relay/actions/workflows/ci.yml/badge.svg)](https://github.com/jason-namehard/agent-relay/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

给同时使用多个 AI Agent 的人准备的本机交接台：把文件从 A 交给 B，看清下一步该谁处理，工作结束就停止往返。

## 快速开始 — Windows x64

公开预览版：`0.1.0-preview.10`。在 Windows PowerShell 5.1 或 PowerShell 7 中下载并完整解压到新目录：

```powershell
$version = 'v0.1.0-preview.10'
$zip = "AgentRelay-$version-windows-x64.zip"
$download = Join-Path $PWD $zip
$ProgressPreference = 'SilentlyContinue'
Invoke-WebRequest "https://github.com/jason-namehard/agent-relay/releases/download/$version/$zip" -UseBasicParsing -OutFile $download
Expand-Archive -LiteralPath $download -DestinationPath ".\AgentRelay-$version"
Set-Location ".\AgentRelay-$version"
.\gui\AgentRelay.Desktop.exe
```

也可从 [Releases](https://github.com/jason-namehard/agent-relay/releases/tag/v0.1.0-preview.10) 下载。`TryDemo.cmd` 提供不调用模型的隔离样本，不代表真实宿主已接通。GUI 与通知自带运行库，CLI/MCP 需要 [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0)。目前没有安装器和代码签名，Windows 可能给出安全提示。

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

1. **添加 A 和 B。** 在 Agent 管理中自选名称，角色和 CLI 路径都不是必填项。
2. **按需安装交接规则。** 选择宿主实际加载的指令文件，检查一句话规则并确认。详细说明只在需要时读取，已有会话可能要重新读取指令。
3. **让 A 写文件给 B。** 安装规则后，A 可输出短交接 JSON；未安装时，把新建交接中复制的格式附在你的要求后面。粘贴 JSON，点击“解析并创建交接”，或手动选择文件。
4. **复制提示词给 B。** 粘贴到 B 的真实会话。短提示指向任务上下文，其中有文件、工作要求和回执模板。
5. **B 写回执。** 软件打开时通过回执文件事件更新任务，启动和刷新也会补读。这条流程无需用户再导入已完成回稿。
6. **确有工作才继续传递。** `done` 且 `needs_reply: false` 就结束。需要对方继续时必须给出真实回稿文件和具体 `next_action`，软件才提示复制给对方；最多四轮，达到上限后停止继续传递，由人判断。

本版本新建 GUI **文件交接只走手动路线**。原有 CLI/MCP 自动消息投递和高级探针分别保留，文件交接窗口没有自动投递选项。

文件改变不能证明完成，明确回执才能。外部文件复制进受管目录，受管文件直接复用，不额外生成正文包装文件或重复结果快照。控制 JSON 放在隐藏的 `.relay` 目录。长期成果保存在项目中，过期受管文件可通过清理操作删除。

## 包含什么

| 区域 | 当前行为 |
| --- | --- |
| 任务看板 | 文件交接、明确等待或处理中的 Agent、下一步复制操作与记录 |
| 节点录入 | 自选名称、选填路径、JSON 解析草稿或本机配置接口 |
| 交接规则 | 用户批准后常驻一句话，详细说明按需读取，移除保留其他编辑 |
| 连接图 | 每对节点一条线；箭头表示有方案方向，绿/红表示启用或暂停 |
| CLI 扩充 | 保存已有方案但不执行，新会话和续接探针分别核验 |
| 设置 | 中转站、保留期、通知、六种语言、四套配色与强调色 |

GUI 与通知为 Windows x64。Core CLI/MCP 有 Windows/Linux CI 覆盖，需要 .NET 10。没有 macOS 桌面包、云端中转站，所有进程关闭后没有后台服务。CLI 输出与多数深层文档目前为中文。

### Agent 配置与规则移除

接入提示词说明如何调查 CLI 帮助和非敏感设置，并通过 `agent setup --manifest` 填写。GUI 能找到 CLI 时提供实际命令，否则让 Agent 返回 JSON 供解析。会话 ID 或记录日志不是调用方法；JSON 不能凭空生成桥接程序，不对未测试宿主承诺兼容。

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

[配置和回执格式](docs/CONFIGURATION_ASSISTANT.md) · [UI 规格](docs/UI_SPEC.md) · [旧消息协议](docs/PROTOCOL.md) · [高级适配器](docs/ADAPTERS.md)

</details>

## 限制与验证边界

- 真实宿主的创建会话、可见性和续接需在该宿主中测试；模拟桥接不代表真实接通，实际探针可能收费。
- 不完整或无效回执会提示处理，保存结果本身不会完成任务。旧消息任务保留原先 CLI/MCP 协议。
- 迁移要求没有未完成任务，包括等待对方继续的回传。文件和账本复制后再切换；旧站作为备份不自动清理，迁移校验只继续有记录的待完成迁移。
- 通知保存有自动测试，系统通知中心弹出和 125%/150% 缩放仍需人工验证。
- Agent 必须能访问中转站文件，任意远程或沙盒会话不一定能读取本机目录。

更多见 [上手指南](docs/GETTING_STARTED.md)、[贡献约定](CONTRIBUTING.md)、[更新记录](CHANGELOG.md) 与 [issues](https://github.com/jason-namehard/agent-relay/issues)。报告请附版本、平台、步骤及脱敏错误。

## 许可

项目代码为 [MIT](LICENSE)。随包 .NET、Windows App SDK 等组件沿用各自条款，见 [分发条款](DISTRIBUTION_TERMS.md)、[第三方说明](THIRD_PARTY_NOTICES.md) 与 `licenses/`。
