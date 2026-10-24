# Agent Relay

Windows-first local task handoffs for coding agents.

[![CI](https://github.com/jason-namehard/agent-relay/actions/workflows/ci.yml/badge.svg)](https://github.com/jason-namehard/agent-relay/actions/workflows/ci.yml)

**公开预览版：** [下载 v0.1.0-preview.6](https://github.com/jason-namehard/agent-relay/releases/tag/v0.1.0-preview.6) · [上手指南](docs/GETTING_STARTED.md) · [更新记录](CHANGELOG.md) · [参与贡献](CONTRIBUTING.md)

Agent Relay 用于中转 Agent 之间的协作消息、请求与临时回稿，并展示交接进度。Agent 的名称、角色和任务由用户决定，Relay 只需要知道谁交给谁。最终成果及长期引用的文件应保存在项目目录；中转站临时文件会按保留策略清理。

Windows 包解压后双击 **`TryDemo.cmd`**，无需模型账号即可体验隔离演示。正式使用运行 **`gui/AgentRelay.Desktop.exe`**：在设置里选择中转站，在 Agent 管理中添加两个自定名称的节点，再从任务看板新建交接。手动复制无需填写宿主配置路径，也无需接通 CLI。

## 四个主页面

| 页面 | 用途 |
| --- | --- |
| 任务看板 | 新建交接，查看请求、回复及处理记录，复制当前所需提示词 |
| Agent 管理 | 统一添加、编辑和移除节点；生成接入提示词并解析 Agent 回复；测试 CLI |
| 连接图 | 查看每个方向的自动通知能力，启用或暂停连接 |
| 设置 | 中转站位置、保留时间、语言、配色和通知；高级 CLI 接口扩充 |

新建交接既可直接填写，也可复制提示词让任意 Agent 整理，再粘贴固定 JSON 回复一键代填。解析不会发送消息，创建前可检查发送方、接收方和内容。没有预设的前端、文档或评审角色。

连接图绿色箭头表示方案已启用，红色箭头表示有方案但暂停或尚未启用，灰色虚线无箭头表示缺少可用方案。A→B 表示 A 可经中转站传递文件并通过 CLI 通知 B；B→A 独立控制。自动路线不可用时仍可手动复制交接提示词。

桌面支持英语、简体中文、繁体中文、日语、韩语和西班牙语，提供四套莫兰迪配色与自定义强调色。配置录入集中在 Agent 添加/编辑窗口，技术字段放在高级区域。详见 [上手指南](docs/GETTING_STARTED.md) 和 [配置助手](docs/CONFIGURATION_ASSISTANT.md)。

## 可选 CLI 接入

在 **设置 → 高级：CLI 接口扩充** 复制提示词，交给了解目标软件调用方式的 Agent。导入返回的方案只保存配置，不执行命令。复杂桥接需要已有、可用的独立桥接程序；原生命令的输出若不符合回执协议，不能直接作为通用接入使用。

在 **Agent 管理 → 测试 CLI** 检查调用方案，再发起短探针。机器回执通过后，由用户在目标软件中找到标识并确认可见；支持续接的方案还可测试同一会话。探针会真实调用目标 CLI，可能产生模型费用，包括宿主附带的上下文。测试通过仅记录本次环境与方案的结果，不保证未来会话均可见。

正式桌面 CLI 交接默认创建新会话，不自动复用探针会话。提交成功不等于任务完成；Agent 仍需显式报告收到、开始、完成或失败。CLI 接口扩充并非适用于所有 Agent，手工交接始终可用。

## Windows 桌面界面

Windows 分发包中运行 `gui/AgentRelay.Desktop.exe`。默认数据目录为 `%LOCALAPPDATA%/AgentRelay`；设置页可迁移到空目录，也可显式指定：

```powershell
./gui/AgentRelay.Desktop.exe --home C:/relay-data
```

迁移在没有活动交接和执行时进行，完成复制和校验后切换，旧目录保留。失败时解除文件占用后使用“迁移校验”继续。CLI/MCP 应使用当前中转站路径；已经复制给 Agent 的旧提示词需重新生成。

GUI 和通知程序自带运行库；CLI、MCP 需要 .NET 10 runtime。界面启动做一次恢复，外部进程更新账本后任务看板刷新，编辑页面保留草稿并提示刷新。关闭全部进程不会继续后台监控。详见 [UI 规格](docs/UI_SPEC.md)。

从源码构建 Windows 组件：

```powershell
dotnet restore src/AgentRelay.Notify/AgentRelay.Notify.csproj --locked-mode
pwsh ./scripts/test-desktop.ps1
pwsh ./scripts/check-licenses.ps1 -IncludeDesktop
pwsh ./scripts/publish.ps1 -IncludeDesktop
```

下方命令行路线供需要 AMQ、MCP 或精确原会话续接的集成使用；桌面手工交接可直接按上手指南完成。

## 构建与验证

需要 .NET 10 SDK；AMQ 为可选外部工具。核心不需要 GUI、服务或云端账号。

```powershell
git clone https://github.com/jason-namehard/agent-relay.git
cd agent-relay
dotnet restore --locked-mode
dotnet test --configuration Release --no-restore
pwsh ./scripts/check-licenses.ps1
pwsh ./scripts/publish.ps1
dotnet ./artifacts/relay/relay.dll --help
```

若 .NET 安装在独立位置，直接使用其 `dotnet.exe` 完整路径；打包脚本接受 `-Dotnet <path>`。发布结果为 framework-dependent CLI，需要 .NET 10 runtime。

## 第一次交接

命令返回 JSON。退出码 `4` 表示已保存但需要人工操作，`6` 表示投递未知，不能把这两种情况当成重新派发的理由。完整选项可运行任意命令的 `--help`。

```powershell
$relay = './artifacts/relay/relay.dll'
$dataRoot = Join-Path $PWD '.relay'
dotnet $relay --home $dataRoot init

# 使用真实、已确认的配置文件路径；validate 只读，不修改 Agent 配置。
dotnet $relay --home $dataRoot agent register --id agent-a --host codex --config-path C:/agents/a/config.toml
dotnet $relay --home $dataRoot agent register --id agent-b --host claude --config-path C:/agents/b/settings.json
dotnet $relay --home $dataRoot agent validate --id agent-a
dotnet $relay --home $dataRoot agent validate --id agent-b
dotnet $relay --home $dataRoot permission set --from agent-a --to agent-b --decision allow

$handoff = dotnet $relay --home $dataRoot task dispatch --from agent-a --to agent-b --request ./request.md --mode manual --idempotency-key review-1 | ConvertFrom-Json
$task = $handoff.data.task
$handoff.data.prompt
```

把请求提示词交给 B，A 随即结束当前轮。B 读取请求，把结果写入响应中的 `result_path`，再显式回调：

```powershell
dotnet $relay --home $dataRoot task ack --task $task.task_id --attempt $task.attempt_id --sender agent-b --idempotency-key ack-1

# B 完成实际工作后写结果；也可用 artifact register 导入外部结果文件。
dotnet $relay --home $dataRoot task complete --task $task.task_id --attempt $task.attempt_id --sender agent-b --status succeeded --result $task.result_path --idempotency-key done-1
dotnet $relay --home $dataRoot prompt show --task $task.task_id
```

若工作失败，使用 `task fail --status failed --error-code <code> --message <reason> --result <failure-file>`，并同样提供 task、attempt、sender 和幂等键。

## 使用 AMQ 传输

从 [AMQ 官方 release](https://github.com/avivsinai/agent-message-queue/releases) 获取对应平台二进制后，显式指定可信文件。验证只在 Relay 自身目录创建测试队列，不安装插件、不启动 AI。

```powershell
dotnet $relay --home $dataRoot adapter verify --id amq-cli --executable C:/tools/amq/amq.exe
dotnet $relay --home $dataRoot task dispatch --from agent-a --to agent-b --request ./request.md --mode auto --adapter amq-cli --idempotency-key review-2
```

`dispatched` 表示 AMQ 已接受消息。B 仍需通过宿主集成或手工命令取件、执行并回调；此阶段不会假装已唤醒原会话。适配器不可用时保存为 `awaiting_manual`。AMQ 版本变化后必须重新验证。

## 自动运行一次 Codex Agent

已有数据目录升级后先运行一次 `init`，以创建新增记录表；已有任务、权限和配置保留。指定原生 `codex.exe`，不用 npm 的 `.cmd`/`.ps1` 启动脚本。

```powershell
dotnet $relay --home $dataRoot init
dotnet $relay --home $dataRoot agent register --id agent-b --host codex --config-path C:/Users/you/.codex/config.toml --cli-path C:/tools/codex.exe
dotnet $relay --home $dataRoot agent validate --id agent-b
dotnet $relay --home $dataRoot agent verify-executor --id agent-b

$handoff = dotnet $relay --home $dataRoot task dispatch --from agent-a --to agent-b --request ./request.md --mode auto --adapter amq-cli --idempotency-key review-3 --execute --execution-timeout-seconds 300 | ConvertFrom-Json
$task = $handoff.data.task
```

派发立即返回等待响应，A 结束当前轮。单次 worker 根据运输凭据领取精确消息，确认当前尝试并启动 B，收到 B 的结构化 `succeeded/failed` 声明后显式调用已有完成/失败操作。worker 在任务处理后退出；退出码 `0`、文件变化或 AMQ 收件回执都不能单独表示任务成功。

执行器使用注册配置文件所在目录作为 `CODEX_HOME`，复用该目录的已有登录态。B 的新任务固定以 `--ignore-user-config --ephemeral --sandbox read-only` 调用，不加载用户 config.toml 的模型/provider/MCP 设置。适用于已登录 Codex 的只读分析和文本结果任务；自定义 provider/profile 及写入代码的执行模式尚未接入。

需要人工确认时使用 `task dispatch --mode confirm`，再显式 `task approve --task <id> --execute`。也可独立运行单次 worker：

```powershell
dotnet $relay --home $dataRoot task run --task $task.task_id --attempt $task.attempt_id --sender agent-b --timeout-seconds 300
dotnet $relay --home $dataRoot task execution --task $task.task_id
```

同一任务尝试只有一个执行领取者；重复 `run` 或重复派发不会再启动模型。执行结果未知时只展示证据；经明确批准的 `task retry` 产生新尝试，再显式运行 worker。日志、结果声明和后台 worker 报告位于该尝试的受管目录，按文件 TTL 清理。

两个真实 CLI 的可复现验证脚本需要本机已有认证，会消耗账户模型额度；普通 CI 不调用模型或读取登录凭据：

```powershell
pwsh ./scripts/verify-real-agents.ps1 -Dotnet dotnet -Codex C:/tools/codex.exe -Amq C:/tools/amq/amq.exe -ConfigPath C:/Users/you/.codex/config.toml
```

## 自动继续原会话与桌面通知

先为 A 登记并验证原生 CLI 路径，再选择会话继续方式。`codex-queue` 只提交消息给宿主；若希望在已结束的 CLI 会话内运行一次只读轮次，显式选择 `codex-exec-resume`，会消耗模型额度。两种方式不自动互相替换。

```powershell
dotnet $relay --home $dataRoot agent verify-resumer --id agent-a --kind codex-queue
```

派发时提供 `--origin-session ./origin.json`，文件包含 `host`、精确 UUID `session_id`、登记的 `config.toml` 绝对路径 `config_profile` 和绝对 `cwd`。Relay 保存适配器快照，继续前核对配置、版本和 session 元数据；不选择最近会话。完成回调先保存结果，再处理继续 outbox。`submitted` 只代表提交回执，`completed` 才表示原 UUID 的一次新轮次有完成证据。

Windows 通知是可选独立组件，按以下命令构建并登记：

```powershell
dotnet restore src/AgentRelay.Notify/AgentRelay.Notify.csproj --locked-mode
pwsh ./scripts/check-licenses.ps1 -IncludeDesktop
pwsh ./scripts/publish.ps1 -IncludeDesktop
dotnet $relay --home $dataRoot desktop configure --executable ./artifacts/relay/desktop/AgentRelay.Notify.exe

dotnet $relay --home $dataRoot task continuations --task $task.task_id
dotnet $relay --home $dataRoot prompt open --task $task.task_id
dotnet $relay --home $dataRoot prompt copy --task $task.task_id
```

没有会话绑定、恢复失败或提交未知时，已配置的 Windows 辅助程序发送本地通知；可打开任务提示词窗口或直接复制。程序退出后由 Windows 点击激活，无常驻 Relay 服务。首次通知会注册该程序的当前用户应用标识和 COM 入口，请保留发布目录位置；停用与卸除说明、`origin.json` 示例和故障语义见 [Phase 3](docs/PHASE3.md)。Windows 二进制中的微软组件按独立条款分发，使用前见 [分发条款](DISTRIBUTION_TERMS.md)。

验证真实 A→B→原 A 闭环可给 `verify-real-agents.ps1` 添加 `-ResumeOrigin`。它创建独立测试会话，检查旧上下文标记、B 的结果和重复继续去重；不向用户当前聊天投递消息。

## stdio MCP 与配置迁移

发布包在 `mcp/` 提供独立 MCP 入口。宿主为每个 Agent 启动一个实例，共用同一个 Relay 数据目录：

```powershell
dotnet ./artifacts/relay/mcp/relay-mcp.dll --home $dataRoot --agent agent-a
```

MCP 提供派发、ack、start、complete、fail、单次 status 和提示词七个工具。身份在启动时固定，模型不能用参数切换派发方或回调方；不会暴露修改权限、批准迁移或无限等待工具。`execute=true` 可启动独立的单次 worker，MCP 客户端关闭后仍能提交结果和继续原会话。

已有配置先只读盘点，再生成准确的变更提案：

```powershell
dotnet $relay --home $dataRoot adapter inspect-config --path C:/Users/you/.codex/config.toml --format toml
dotnet $relay --home $dataRoot migration plan --manifest ./migration.json
dotnet $relay --home $dataRoot migration show --id <migration-UUID>
```

预览不修改配置；应用和回滚须在审阅后显式 `--approve --reason`。JSON/TOML 以唯一精确片段变更，保留 BOM、换行和无关内容；文件已变化则停止。旧入口须引用已验证的新迁移，单独提案、单独批准停用。备份位于数据目录的 `migrations/`，不受三天 TTL 清理。完整 manifest、MCP 参数、证据与恢复流程见 [Phase 4 指南](docs/PHASE4.md)。

真实闭环验证可给 `verify-real-agents.ps1` 添加 `-McpMigration`，它只修改独立测试配置，验证 MCP 派发、真实完成与原会话继续，再验证单独停旧和回滚；会消耗模型额度。

## 超时、重试与清理

```powershell
dotnet $relay --home $dataRoot task status --task $task.task_id
dotnet $relay --home $dataRoot recover --task $task.task_id
dotnet $relay --home $dataRoot task evidence --task $task.task_id
dotnet $relay --home $dataRoot task retry --task $task.task_id --reason '已检查旧执行，批准重新执行' --approve
dotnet $relay --home $dataRoot cleanup --dry-run
dotnet $relay --home $dataRoot cleanup --apply
```

默认所有进程退出后不做后台工作；截止时间在下次 `recover` 时处理。`status` 只报告超时事实，不隐式投递。三天 TTL 只控制受管 Markdown 文件；SQLite 保留请求快照用于幂等比对和经批准的重试，并保留任务、事件、凭据和通知。清理不会删除数据库或宿主原始文件，也不承诺物理擦除。

## 已知边界

- 路径验证只证明文件存在、可读及登记关系，不声称宿主配置语法、认证和会话控制已通过验证。
- 尚未注册 Windows Task Scheduler；所有进程退出后，截止时间在下次 `recover` 处理。通知被系统接受不保证横幅可见。
- 内置精确会话恢复面向 Codex；其他宿主可使用经测试的 CLI 扩充方案或复制提示词。新扩充方案的同会话探针不自动绑定正式任务。
- `delivery_unknown` 保留凭据供人工核对；当前不自动对账或重发。取消只停止 Relay 后续动作，不终止已经启动的宿主。
- 清理覆盖登记产物；异常退出留下的未登记 workspace 文件按文件系统创建时间和当前 TTL 处理，不跟随目录链接。

## 文档与许可

[项目概要](docs/PROJECT_BRIEF.md) · [架构](docs/ARCHITECTURE.md) · [协议](docs/PROTOCOL.md) · [适配器](docs/ADAPTERS.md) · [UI 规格](docs/UI_SPEC.md) · [研究与路线图](docs/ROADMAP_AND_RESEARCH.md)

[Phase 1 验收](docs/PHASE1.md) · [Phase 2 验收](docs/PHASE2.md) · [Phase 3 验收](docs/PHASE3.md)

[Phase 4 使用与验收](docs/PHASE4.md) · [Phase 5 桌面指南](docs/PHASE5.md)

应用代码采用 [MIT License](LICENSE)。依赖和外部工具的来源、许可及分发说明见 [Third-party notices](THIRD_PARTY_NOTICES.md)。
