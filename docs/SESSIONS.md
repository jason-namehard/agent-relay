# Session Registry 与精确续接

会话绑定回答“这个任务的下一轮应进入哪一段对话”。新建会话时，Relay 从适配器结构化结果登记真实 ID；使用已有对话时，由用户明确选择一次。同一任务 attempt 的后续 A → B → A → B 使用双方已绑定的准确 ID，不重新猜测目标。

## 用户操作

“新建任务”直接接受工作要求、发送方及来源会话、接收方和目标会话；不必先生成文件或交接 JSON。已有文件仍可走“新建交接”，存在 CLI 方案时确认会话选择。目标默认“新建会话”；选择“使用已有会话”后点击“选择已有会话”。列表优先展示标题、活跃时间、项目路径和可获取的模型信息；准确 ID 用于确认身份。列表不会默认选择第一条，也不会把最近活跃或同 cwd 当作已绑定。

已有缓存立即显示，同时后台刷新该 Agent。取消不绑定，点击“使用此会话”才确定选择。无发现提供器时保留手动 ID 入口，不显示一个注定无法工作的发现列表。

任务详情“会话”折叠区显示双方绑定，可主动更换或明确新建。会话已失效、存储不可访问或返回的 ID 不一致时，自动续接停止；用户可以重新选择或手动复制提示。不会自动跳到其他对话。

## 协作方式与会话绑定

会话绑定只确定下一轮进入哪里，协作方式确定下一轮由谁处理。单次交接由回执的 `needs_reply` 决定是否回传，默认最多 4 轮。持续修改与验收由用户指定作者，另一方验收，按回执 `outcome` 在 `implement`/`revise`、`review`、`accept` 阶段间推进，只有作者 `accepted` 才完成；格式见 [配置与回执](CONFIGURATION_ASSISTANT.md#持续修改与验收)。

经 CLI 把轮次送回发送方需要其准确来源会话和续接命令；未绑定来源时这些轮次手动复制。轮数用完、单轮超时或无进展时任务暂停，绑定保留，继续后仍进入同一对话。新任务不再使用固定 `handoff_plan`；早期版本已带计划的任务仍按原计划完成。

## 来源身份

来源 Agent 的名称不能证明当前 Session。普通剪贴板中的 `{from,to,file,message?}` 也无法提供调用进程身份，因此界面显示来源未识别，并允许用户选择一次；模型不需要查找或填写自己的 ID。

只有调用适配器已有准确会话上下文时，才能自动登记来源。精确 resume 调用会注入 `AGENT_RELAY_CALLER_AGENT_ID`、`AGENT_RELAY_CALLER_SESSION_ID`、`AGENT_RELAY_CALLER_HOST`、`AGENT_RELAY_CALLER_CWD`，以及可获取的 profile。通过上下文入口创建新交接时，发送方必须与该上下文一致。尚未返回会话 ID 的新建调用不能提前声称已经识别其来源。

这些是本机适配器传递的上下文，不是要求模型搜查文件、返回会话 ID 或修改宿主 UI。长期规则负责短 handoff 格式，每轮任务不重复注入完整协议。

## Registry 与持久化

权威数据保存在现有 Relay SQLite：

| 表 | 内容 |
| --- | --- |
| `task_session_bindings` | task、attempt、turn、agent 及准确 session ID、cwd、profile、路径、绑定依据和时间 |
| `session_discovery_sources` | 指定节点的提供器及数据根目录 |
| `session_discovery_cache` | 节点展示缓存和扫描时间 |

绑定来源包括适配器上下文 `automatic`、创建结果 `created`、用户点选 `user_selected` 和手动输入 `manual`。明确选择新建时记录 `new`；命令只返回最终文本、没有持久会话 ID 时记录 `ephemeral`，不能把它当作可续接会话。

查找绑定限定当前 attempt，再读取该 Agent 最新轮次的明确记录。新 attempt 不跨用旧记录；新的返回 ID 不会悄悄替换原绑定。回执仍决定工作是否完成，CLI 提交成功本身不完成任务。

## Discovery

提供器只读取指定节点的宿主数据，不执行模型请求，也不调用外部“全量会话总览”脚本。非标准位置可通过 `session source` 配置，或从调用方案里明确的数据目录环境变量取得。

| 提供器 | 发现依据 | 可见性依据 |
| --- | --- | --- |
| DSH | 实际持久会话日志文件；已有 projcache 提供标题、cwd、model | 默认 unknown，不把投影存在当作 GUI 可见 |
| Zcode | CLI SQLite 会话表，排除删除和归档；定向读取目标目录与用户任务索引 | 明确索引匹配为 native-visible；有索引但未收录为 persistent-but-not-indexed，否则 unknown |
| Hermes | SQLite 会话元数据、profile、cwd、model | 隐藏状态单独标注；桌面来源本身不证明当前 UI 可见 |
| WorkBuddy | SQLite 会话元数据、自定义标题、cwd、model，排除删除和归档 | 读取已知隐藏标记，其余保留 unknown |

Relay 缓存用于快速展示，不能证明续接仍然有效。每次 resume 前重新查询宿主存储中的准确 ID；数据库文件存在也不代表其中一定有这个 Session。刷新失败明确显示错误，不用旧缓存伪造一次成功刷新。

## 当前能力

以下是已实现的适配能力，不是对任意安装版本或账号状态的真实调用保证。创建与续接仍需登记符合该宿主的命令。

| 宿主/输出 | 新建并取得 ID | 精确续接 | 定向发现 | 打开宿主会话 | 模型/思考设置 |
| --- | --- | --- | --- | --- | --- |
| DSH JSONL | 有效 session/final/完成事件可登记 | 需明确 ID、resume 命令及存在性验证 | 已实现 | 未实现 | 继承宿主/原会话 |
| Zcode JSON | 有效 result 的 sessionId | 同上，返回 ID 必须一致 | 已实现 | 未实现 | 继承宿主/原会话 |
| Hermes JSONL | 有效 result 的 session_id | 同上 | 已实现 | 未实现 | 继承宿主/原会话 |
| WorkBuddy 兼容 JSON | 有效成功结果的 session_id | 同上 | 已实现 | 未实现 | 继承宿主/原会话 |
| 普通 text | 无 ID，仅一次性执行 | 不支持凭文本伪造 ID | 取决于另配提供器 | 未实现 | 由命令与宿主管理 |

`session_open` 没有统一实现，也不会用“请模型把会话显示出来”代替。可见性与可续接性分别展示；可见性未知不自动阻断一个准确且存在的绑定。Relay 为本任务创建的会话没有发现能力也可续接，返回 ID 仍须一致；用户选择的已有会话仍需发现确认。

模型和思考强度属于执行配置，当前没有统一跨宿主覆盖表单。新建会话若提示未选模型，需要在宿主设置或选择已经配置的旧会话；续接默认继承旧会话。Relay 不把模型参数塞进任务正文，也不伪造宿主不支持的参数。

## WorkBuddy

WorkBuddy 桌面版目前没有对外可用的调用接口，只能复制提示词手动交接：

- 应用自带的 CLI（`codebuddy-lite-wb.mjs`）单独启动时用不了桌面登录，报 `Authentication required`。
- 每个打开中的对话由一个本机工作进程承载，`~/.workbuddy/sessions/<pid>.json` 记录其 ACP 地址。WorkBuddy 5.9.2 对每个对话只允许一个写入方：桌面在线时外部连接只能旁观，外部 `session/prompt` 被忽略、`session/load` 报 `Persistent Session already has an external writer`。这是程序规则，不是可调的客户端设置。
- 官方 CodeBuddy Code CLI 有独立的登录、额度和会话存储，建的会话不出现在 WorkBuddy 里，不能代替 WorkBuddy。

会话发现仍读取 `~/.workbuddy/workbuddy.db`，手动交接时可以绑定 WorkBuddy 的对话以便记录。

## DSH

DSH 用自带的 headless profile 接入：`DeepSeek Harness.exe`（环境变量 `ELECTRON_RUN_AS_NODE=1`、`DSH_HOME` 指向 dsh-home）运行 `...\dsh-desktop-host\lib\cli.js --profile headless --json {prompt}`，续接加 `--session-id {session_id}`，`output_format` 为 `dsh-jsonl`，会话发现提供器为 `dsh`、目录为 dsh-home。

- headless profile 默认权限是 workspace-write，写不了工作区外的 Relay 回执。用 `--patch` 叠加一个只含 `permission.defaultPreset: danger-full-access` 的补丁（与桌面 profile 的默认相同）。
- headless 使用它自己的模型与凭据。直接叠加桌面 profile 的补丁会改用桌面的供应商，但凭据按 profile 存放，会报 `MISSING_CREDENTIAL`。

## Zcode CLI

从桌面应用外启动 `zcode.cjs` 时，命令行只认自己登记过的账号套餐；桌面已登录、或在桌面里切换模型，都不会为它登记，于是新建会话报 “Select a model before sending a message”。在终端登录一次即可登记套餐并写入默认模型：

```powershell
$env:ZCODE_BUILTIN_PROVIDER_CONFIG_FILE = "D:\Zcode\resources\config\provider\zcode-builtin.json"   # 按实际安装目录
& <Zcode 自带 node.exe> "D:\Zcode\resources\glm\zcode.cjs" login zai --no-browser
```

不设该变量时，登录会因找不到内置供应商配置而失败。Windows 自动打开浏览器时会在第一个 `&` 处截断授权地址，页面报缺少 `response_type`、`redirect_uri`、`state`；加 `--no-browser` 后复制终端打印的完整地址到浏览器打开。`relay cli-recipe doctor` 会检查命令行是否已登记套餐。命令行的供应商只来自它自己登记的账号套餐和 `~/.zcode/cli/config.json` 中的自定义供应商（`provider` 与 `model` 字段，仅在 `~/.zcode/v2/provider_config.json` 不存在时导入一次）；桌面里配置的自定义供应商（如 OpenCode Go）由桌面经内部协议交给自己的进程，命令行看不到，因此 `defaultModelSelection` 只能指向命令行已登记的供应商。续接已选过模型的会话不受影响。

## CLI

在发布包目录使用 .NET 10，替换示例中的节点、任务和目录：

```powershell
dotnet .\relay.dll --home <中转站目录> handoff cli-create --manifest <CLI任务清单>
dotnet .\relay.dll --home <中转站目录> handoff cli-create --manifest <CLI任务清单> --send --approve
dotnet .\relay.dll --home <中转站目录> handoff send --task <任务ID> --approve
dotnet .\relay.dll --home <中转站目录> session query --agent <节点ID> --refresh
dotnet .\relay.dll --home <中转站目录> session capabilities --agent <节点ID>
dotnet .\relay.dll --home <中转站目录> session source --agent <节点ID> --provider dsh --directory <宿主数据根目录>
dotnet .\relay.dll --home <中转站目录> session bindings --task <任务ID>
dotnet .\relay.dll --home <中转站目录> session bind --task <任务ID> --agent <节点ID> --session <准确会话ID>
dotnet .\relay.dll --home <中转站目录> session new --task <任务ID> --agent <节点ID>
```

`cli-create` 不加 `--send` 只保存工作要求和绑定，发送必须显式 `--approve`；清单结构见 [配置与回执](CONFIGURATION_ASSISTANT.md)。`source` 的 provider 可选 `dsh`、`zcode`、`hermes`、`workbuddy`，数据目录应与该提供器对应。查询只读元数据，`capabilities` 返回已登记方案声明的能力，不代表实际调用已经验证。`bind` 保存用户提供的精确 ID，保存不等于已验证；自动发送前仍核实存在。未知提供器或不能访问的存储不会回退到 latest。自动投递需任务允许且该 Agent 对应能力已验证，见 [配置与回执](CONFIGURATION_ASSISTANT.md#高级-cli-接口扩充)；真实发送和续接可能产生模型费用。
