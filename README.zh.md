# Agent Relay

[English](README.md) | **简体中文**

[![CI](https://github.com/jason-namehard/agent-relay/actions/workflows/ci.yml/badge.svg)](https://github.com/jason-namehard/agent-relay/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/jason-namehard/agent-relay?include_prereleases&label=preview)](https://github.com/jason-namehard/agent-relay/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

给同时使用多个 AI Agent 的人准备的本机交接台：记下谁交给了谁、对方有没有收到、回稿在哪里、下一步该谁继续。

在几个 Agent 会话之间来回复制请求和草稿，很快就分不清：这条消息交给了哪个会话，对方是否真的接手，哪份回稿对应哪个请求，现在该轮到谁。Agent Relay 把每次交接放进本机中转站，用 SQLite 账本记录状态，并在 Windows 桌面界面中展示进度。Agent 和它们的分工由你决定，Relay 只需要知道发送方和接收方。手动复制交接无需任何接入配置；自动投递是可选项，只对你测试过调用方式的 Agent 开放。

当前状态：公开预览版（`0.1.0-preview.9`）。主要入口是 Windows 桌面程序；CLI 与 stdio MCP 已有 Windows/Linux CI 验证，桌面程序仅限 Windows。

## 快速开始（Windows x64）

在 Windows PowerShell 5.1 或 PowerShell 7 中，进入准备存放下载文件的目录（例如“下载”），运行：

```powershell
$version = 'v0.1.0-preview.9'
$zip = "AgentRelay-$version-windows-x64.zip"
$ProgressPreference = 'SilentlyContinue'
Invoke-WebRequest "https://github.com/jason-namehard/agent-relay/releases/download/$version/$zip" -UseBasicParsing -OutFile $zip
Expand-Archive $zip -DestinationPath ".\AgentRelay-$version"
Set-Location ".\AgentRelay-$version"

# 隔离演示：样本 Agent 与预设结果，无需模型账号，不调用模型。
.\TryDemo.cmd
```

请把压缩包完整解压到新目录，不要直接在 ZIP 里运行。也可以从 [Release 页面](https://github.com/jason-namehard/agent-relay/releases/tag/v0.1.0-preview.9) 下载后双击 `TryDemo.cmd`。每次演示都会在 `%LOCALAPPDATA%\AgentRelay.Demos\` 下新建独立账本，不读取你现有的 Agent 配置。

<details>
<summary>校验从 GitHub 下载的发布包（约 227 MB）</summary>

解压前，在下载目录中运行：

```powershell
$expected = 'AA3CF3E2979EFDA4898C64CA3F54395FDC57673E3D133653E207EC188508B8C7'
if ((Get-FileHash $zip -Algorithm SHA256).Hash -ne $expected) {
    throw 'The archive does not match the v0.1.0-preview.9 Windows GitHub asset.'
}
```

该值对应 GitHub 上传的 Windows 发布资产，不适用于从同版本源码生成的任意压缩包。本机构建与 CI 构建相互独立，目前不承诺逐字节可复现。

上述下载与解压命令已在 Windows PowerShell 5.1 中用完整的 GitHub 发布包验证。后面的开发脚本需要 PowerShell 7。

</details>

正式使用时，在同一目录启动桌面程序：

```powershell
.\gui\AgentRelay.Desktop.exe
# 或者使用独立的试用账本，不动默认的 %LOCALAPPDATA%\AgentRelay：
.\gui\AgentRelay.Desktop.exe --home "$env:LOCALAPPDATA\AgentRelay-ManualTrial"
```

桌面程序和通知辅助程序自带 .NET 运行库。CLI 与 MCP（`relay.dll`、`mcp\relay-mcp.dll`）需要安装 [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0)。目前没有安装器，二进制也未做代码签名，首次启动时 Windows SmartScreen 或安全软件可能给出提示。

## 一次交接怎么走

1. **设置：确认中转站。** 默认是 `%LOCALAPPDATA%\AgentRelay`，里面保存请求、临时回稿和账本。你和参与交接的每个 Agent 都需要能读写这个目录。
2. **Agent 管理：添加两个节点。** 名字随你起，没有内置的“前端”“文档”“评审”之类分工。宿主、配置路径和 CLI 路径都是可选项，收在“高级”里。需要帮忙填写时，可以复制接入提示词交给该 Agent，再把它返回的 JSON 粘贴解析；解析只代填草稿，仍由你检查后保存。
3. **任务看板：新建交接。** 选择发送方、接收方并写下请求。也可以复制生成提示词，让任意 Agent 起草，再粘贴它的 JSON 回复；内容会填进窗口供你检查，确认之前不会创建任务。
4. **把交接送出去。** 默认走手动路线：在任务详情里复制给接收方的提示词，粘贴到对方会话中。提示词包含中转站位置、请求与结果路径以及回报命令。如果这个方向已有验证过的自动路线（见[可选：自动 CLI 投递](#可选自动-cli-投递)），也可以改选自动投递。
5. **接收方写回稿，并显式回报。** 通过 Relay CLI 或 MCP 工具报告收到、开始、完成或失败（`ack`、`start`、`complete`、`fail`）。如果对方运行不了这些命令，就把回稿文件交给你，由你在任务详情中点击“导入完成回复”并确认。
6. **原发送方继续。** 任务成功后，在任务详情复制给发送方的提示词，粘贴回发送方的会话。

为了让状态可信，有几条规则：

- **提交成功不等于任务完成。** 投递回执只表示请求已经送达。只有收到显式完成回报，或你确认导入回稿后，任务才会变成“成功”。
- 结果文件出现、被打开或被修改，都不会自动把任务标记为完成。
- 中转站只是中转区。受管文件默认保留 3 天，到期后可通过清理操作删除。最终成果和以后还要引用的文件，请保存到你的项目目录。

## 功能与支持范围

| 方面 | 当前实现 |
| --- | --- |
| 导航 | 四个主页面：任务看板、Agent 管理、连接图、设置 |
| 节点录入 | 一个添加/编辑窗口；名称和角色由用户决定；可选接入提示词，JSON 解析只代填草稿 |
| 新建交接 | 直接填写，或让 Agent 按固定 JSON 格式起草；发送方、接收方和内容在创建前确认 |
| 任务状态 | SQLite 账本记录显式的收到、开始、完成、失败事件；请求、回复和处理记录集中在任务详情 |
| 回稿导入 | 确认接收方已完成后，在任务详情导入回稿文件 |
| 连接图 | 每对 Agent 一条线；箭头表示哪些方向有已验证的调用方案，颜色表示各方向是否启用 |
| CLI 扩充 | 导入既有调用方案（可执行文件、参数数组、输入模板、工作目录、依赖和来源说明）；导入只保存，不执行 |
| CLI 探针 | 发送短标识请求；机器回执与你本人的可见性确认分开记录；同会话续接单独测试 |
| 自动交接 | 使用接收方已验证的调用方案创建新会话，不复用探针会话 |
| 存储 | 本机中转站与保留期清理；迁移到空目录时先复制校验再切换，旧目录保留 |
| 外观 | 英语、简体中文、繁体中文、日语、韩语、西班牙语；四套配色与自定义强调色 |
| 通知 | 发布包附带可选的 Windows 通知辅助程序，由设置页开关自动配置 |

| 组件 | 平台 | 运行库 | 自动检查 |
| --- | --- | --- | --- |
| 桌面程序（WPF） | Windows x64 | 自带 | `windows-latest` CI 运行桌面交互测试 |
| 通知辅助程序 | Windows x64 | 自带（含 Windows App SDK） | 开关保存与切换行为；系统通知中心是否实际弹出需人工确认 |
| CLI 与 stdio MCP | Windows、Linux | .NET 10 runtime | `windows-latest` 与 `ubuntu-latest` CI 运行核心测试 |
| macOS | 无发布包 | — | 未测试 |

中转站位于本机，没有云端中转或托管服务。“无需账号”指的是 Relay 自身界面和演示；你接入的 Agent 仍然使用各自宿主的账号、权限和模型计费。

### 怎么看连接图

| 线型 | 含义 |
| --- | --- |
| 单箭头 | 只有箭头所指方向有已验证的调用方案 |
| 双箭头 | 两个方向都有已验证的调用方案 |
| 灰色虚线，无箭头 | 两个方向都没有已验证的方案 |

接收方的方案通过新会话探针、并由你确认会话可见后，这个方向才算“已验证”。箭头及其相邻半段线为绿色表示该方向已启用，红色表示已暂停或尚未启用；双向线可以一端绿、一端红。点击连线即可分别管理两个方向。无论连线显示什么，手动复制始终可用。

## 可选：自动 CLI 投递

自动投递的前提是目标 Agent 本身已有可用的命令行接口或桥接程序。Relay 不会自己学会任意 CLI。

1. 在 **设置 → 高级：CLI 接口扩充** 复制扩充提示词，交给了解目标软件在你本机如何调用的 Agent，再粘贴它返回的 JSON 方案。导入只保存，不运行任何命令。
2. 方案中的命令必须把请求交给真实的 Agent 会话，并输出一个 JSON 对象：`{"session_id": "...", "reply": "...", "accepted": true}`。原生 CLI 的输出不符合时，需要一个已经存在且可用的独立桥接程序；输出格式不符的原生命令不能算作兼容。
3. 在 **Agent 管理 → 测试 CLI** 运行新会话探针，它会发送一个短标识。机器回执通过后，请你亲自在目标软件里找到这个标识，再确认可见。带续接命令的方案还可以测试同一会话的续接。
4. 在连接图启用该方向。之后“新建交接”就可以为这个方向选择自动 CLI 投递。

探针通过能说明什么、不能说明什么：

- 它只记录该方案在当前环境下的一次结果，不保证以后的会话都可见。
- 正式的自动交接会新建会话，不会把工作发到探针会话里。
- `accepted: true` 表示已提交，接收方仍需显式回报完成。
- 探针和自动交接会真实调用 CLI，可能产生模型费用，包括宿主附带的上下文。Relay 不保证缓存命中或节省 token。
- 自动化测试使用本地模拟桥接，不代表任何具体宿主已经接通。

## CLI 与 MCP

Windows 包和 `cli-mcp` 包都包含同一套 CLI（`relay.dll`）和 stdio MCP 服务（`mcp/relay-mcp.dll`），两者都需要 .NET 10 runtime。

<details>
<summary>用命令行完成一次最简手动交接</summary>

在解压后的发布包目录中使用 PowerShell，确保 `.relay` 是新目录。`dotnet` 需要能找到 .NET 10 runtime，否则请改用该版本 `dotnet.exe` 的完整路径。本例使用示例回稿演示本地流程，不调用 Agent 或模型。

```powershell
$relay = Join-Path $PWD 'relay.dll'
$dataRoot = Join-Path $PWD '.relay'          # 任意一个双方 Agent 都能访问的目录
dotnet $relay --home $dataRoot init

# CLI 要求配置路径。本例使用隔离目录中的演示文件，
# 不读取或配置任何已安装的 Agent 宿主。
$config = Join-Path $dataRoot 'manual-node.json'
Set-Content $config '{"mode":"manual-walkthrough"}' -Encoding UTF8
dotnet $relay --home $dataRoot agent register --id writer   --config-path $config
dotnet $relay --home $dataRoot agent register --id reviewer --config-path $config

Set-Content .\request.md 'Review the attached notes.' -Encoding UTF8
$handoff = dotnet $relay --home $dataRoot task dispatch --from writer --to reviewer --request .\request.md --mode manual --idempotency-key review-1 | ConvertFrom-Json
$task = $handoff.data.task
$handoff.data.prompt           # 把这段提示词交给 reviewer

# reviewer 显式回报：
dotnet $relay --home $dataRoot task ack --task $task.task_id --attempt $task.attempt_id --sender reviewer --idempotency-key ack-1
# 本例写入示例回稿；实际使用时改为接收 Agent 的真实回稿。
Set-Content $task.result_path 'Reviewed: the example handoff is complete.' -Encoding UTF8
dotnet $relay --home $dataRoot task complete --task $task.task_id --attempt $task.attempt_id --sender reviewer --status succeeded --result $task.result_path --idempotency-key done-1
dotnet $relay --home $dataRoot task status --task $task.task_id
```

命令输出 JSON。退出码 `4` 表示已保存、等待人工步骤（上面的手动 `dispatch` 就会返回 4）；`6` 表示投递或执行结果未知，重试前先核对目标会话。`relay` CLI 命令加 `--help` 可查看选项。失败时使用 `task fail --status failed --error-code <代码> --message <原因> --result <失败报告>`，并同样提供 task、attempt、sender 和幂等键参数。

</details>

<details>
<summary>stdio MCP 服务</summary>

由宿主为每个 Agent 启动一个实例，都指向同一个中转站：

```powershell
dotnet .\mcp\relay-mcp.dll --home <中转站目录> --agent <已登记的 Agent ID>
```

提供的工具：`relay_task_dispatch`、`relay_task_ack`、`relay_task_start`、`relay_task_complete`、`relay_task_fail`、`relay_task_status`、`relay_prompt_show`。调用方 Agent 身份在启动时固定，派发时通过 `to` 参数选择接收方；自动投递还会检查对应方向的授权。参数说明和经审阅的配置迁移流程见 [Phase 4](docs/PHASE4.md)。

</details>

<details>
<summary>中转站目录的确定顺序与环境变量</summary>

下表展示 Windows 默认路径；其他系统的 CLI/MCP 使用该平台的本地应用数据目录。

| 程序 | 顺序 |
| --- | --- |
| CLI、MCP | `--home` → `RELAY_HOME` → `%LOCALAPPDATA%\AgentRelay` |
| 桌面程序 | `--home` → `RELAY_HOME` → 设置页选定的目录 → `%LOCALAPPDATA%\AgentRelay` |
| 桌面演示 | `%LOCALAPPDATA%\AgentRelay.Demos\` 下的新目录 |

在设置页迁移中转站后，CLI 和 MCP 不会自动跟随；请传入新的 `--home` 或设置 `RELAY_HOME`，并重新复制交接提示词。

</details>

<details>
<summary>高级 CLI 路线</summary>

以下能力已在 CLI 中实现，详见各文档：

- AMQ 传输，以及用于只读任务的单次 Codex worker — [Phase 2](docs/PHASE2.md)、[适配器](docs/ADAPTERS.md)
- Codex 精确会话继续，以及从 CLI 发出的 Windows 通知 — [Phase 3](docs/PHASE3.md)
- stdio MCP 与经审阅的 JSON/TOML 配置迁移 — [Phase 4](docs/PHASE4.md)
- 任务生命周期、事件与文件契约 — [协议](docs/PROTOCOL.md)

`scripts/verify-real-agents.ps1` 会使用你现有的登录态驱动真实 Agent CLI，并消耗模型额度；普通 CI 不会运行它。

</details>

## 开发与贡献

<details>
<summary>从源码构建与测试</summary>

需要 .NET 10 SDK（见 `global.json`）。在仓库根目录用 PowerShell 运行：

```powershell
git clone https://github.com/jason-namehard/agent-relay.git
cd agent-relay
dotnet restore --locked-mode
dotnet test --configuration Release --no-restore
```

以上构建并测试 solution 中的 Core、CLI、MCP 和 `AgentRelay.Core.Tests`。Windows 桌面程序和通知辅助程序需单独构建（仅限 Windows，PowerShell 7）：

```powershell
dotnet restore src/AgentRelay.Notify/AgentRelay.Notify.csproj --locked-mode
pwsh ./scripts/test-desktop.ps1
pwsh ./scripts/check-licenses.ps1 -IncludeDesktop
pwsh ./scripts/package.ps1 -Flavor Windows      # CLI/MCP 包用 -Flavor Portable
```

如果 `PATH` 中的 `dotnet` 不是 .NET 10 SDK，请为 `test-desktop.ps1`、`publish.ps1` 和 `package.ps1` 传入 `-Dotnet <路径>`，`restore` 和 `test` 也直接调用该路径。发布包输出到 `artifacts/packages/`，脚本拒绝覆盖已存在的同名包。

</details>

<details>
<summary>架构</summary>

| 项目 | 职责 |
| --- | --- |
| `src/AgentRelay.Core` | 任务生命周期、SQLite 账本、受管目录、投递适配器、CLI 扩充、中转站迁移 |
| `src/AgentRelay.Cli` | `relay` 命令行，输出 JSON |
| `src/AgentRelay.Mcp` | stdio MCP 服务，每个实例绑定一个 Agent |
| `src/AgentRelay.Desktop` | WPF 桌面程序（四个主页面） |
| `src/AgentRelay.Notify` | Windows 通知辅助程序（Windows App SDK） |
| `tests/AgentRelay.Core.Tests`、`tests/AgentRelay.Desktop.Smoke` | 核心测试；桌面交互测试 |

CLI、MCP 和桌面程序共用同一个 Core 和账本，因此 Agent 的回报会出现在任务看板上。桌面程序在启动和手动刷新时读取账本，运行期间会接收其他 Relay 进程的更新提示；启动时不执行恢复。超时截止在下次运行 `relay recover` 时处理。更多背景见 [架构](docs/ARCHITECTURE.md)、[UI 规格](docs/UI_SPEC.md)、[Phase 5 桌面说明](docs/PHASE5.md)。较早的设计文档也包含尚未实现的规划；当前行为以代码和本 README 为准。

</details>

欢迎通过 [issues](https://github.com/jason-namehard/agent-relay/issues) 和 pull request 报告缺陷、提交范围明确的改进。请附上版本、平台、复现步骤和脱敏后的错误码；演示模式适合在不涉及认证的情况下复现问题。另见 [贡献指南](CONTRIBUTING.md)、[更新记录](CHANGELOG.md) 和 [发布流程](docs/RELEASING.md)。请勿提交认证信息、真实 Agent 配置、账本或含私人任务的日志。

## 已知限制

- 预览版：没有安装器和代码签名，桌面程序仅支持 Windows x64；没有 macOS 桌面包，也没有云端中转站。
- 所有 Relay 进程关闭后不会再做任何事：没有后台服务或计划任务。超时任务在下次运行 `relay recover` 时处理。
- 通知：开关保存已有测试；Windows 是否在通知中心实际弹出横幅取决于系统，需要人工确认。125%/150% 显示缩放下的界面尚未完整检查。
- 自动投递需要每个目标都有可用的 CLI 或桥接程序，并在你的环境中通过探针。不声明与任何具体 Agent 宿主兼容。
- CLI 内置的精确会话继续面向 Codex；其他宿主使用测试通过的 CLI 扩充方案或手动复制。
- 取消交接只停止 Relay 的后续动作，不会终止已经在运行的 Agent。投递结果未知时不会自动重发。
- 中转站迁移有自动化测试覆盖，但人工使用还很少。

## 许可

Agent Relay 的代码采用 [MIT License](LICENSE)。Windows 发布包还再分发了 .NET 运行库、Windows App SDK 等组件，它们适用各自的条款；再分发前请阅读 [DISTRIBUTION_TERMS.md](DISTRIBUTION_TERMS.md)、[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) 和 `licenses/` 目录。
