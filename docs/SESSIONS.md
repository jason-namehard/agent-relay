# Session Registry 与精确续接

会话绑定回答“这个任务的下一轮应进入哪一段对话”。新建会话时，Relay 从适配器结构化结果登记真实 ID；使用已有对话时，由用户明确选择一次。同一任务 attempt 的后续 A → B → A → B 使用双方已绑定的准确 ID，不重新猜测目标。

## 用户操作

“新建 CLI 任务”直接接受工作要求、发送方及来源会话、接收方和目标会话；不必先生成文件或交接 JSON。已有文件仍可走“新建交接”，存在 CLI 方案时确认会话选择。目标默认“新建会话”；选择“使用已有会话”后点击“选择已有会话”。列表优先展示标题、活跃时间、项目路径和可获取的模型信息；准确 ID 用于确认身份。列表不会默认选择第一条，也不会把最近活跃或同 cwd 当作已绑定。

已有缓存立即显示，同时后台刷新该 Agent。取消不绑定，点击“使用此会话”才确定选择。无发现提供器时保留手动 ID 入口，不显示一个注定无法工作的发现列表。

任务详情“会话”折叠区显示双方绑定，可主动更换或明确新建。会话已失效、存储不可访问或返回的 ID 不一致时，自动续接停止；用户可以重新选择或手动复制提示。不会自动跳到其他对话。

## 完成计划与会话绑定

会话绑定只确定下一轮进入哪里，完成计划确定还有谁必须处理。可选的审阅落实计划在任务 `handoff_plan` 中记录 `[接收方,发送方,接收方]`，对应 B 处理、A 审阅、B 落实；`turn` 是当前步骤。中间步骤即使错误声明 `needs_reply:false`，也不能提前结束任务；必须提交真实回稿及具体下一步才能推进，缺少则保持当前步骤并提示补齐。

上下文提供 `required_next_agent`、剩余步骤和正确的回执模板。无计划旧任务保留原语义，但“无需继续”与非空 `next_action` 相互矛盾，会被拒绝。Relay 不从自然语言猜计划，也不自动追改已经结束的历史任务；必要的额外往返仍受四轮上限约束。

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

`session_open` 没有统一实现，也不会用“请模型把会话显示出来”代替。可见性与可续接性分别展示；可见性未知不自动阻断一个准确且存在的绑定。

模型和思考强度属于执行配置，当前没有统一跨宿主覆盖表单。新建会话若提示未选模型，需要在宿主设置或选择已经配置的旧会话；续接默认继承旧会话。Relay 不把模型参数塞进任务正文，也不伪造宿主不支持的参数。

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

`cli-create` 不加 `--send` 只保存工作要求和绑定，发送必须显式 `--approve`；清单结构见 [配置与回执](CONFIGURATION_ASSISTANT.md)。`source` 的 provider 可选 `dsh`、`zcode`、`hermes`、`workbuddy`，数据目录应与该提供器对应。查询只读元数据，`capabilities` 返回已登记方案声明的能力，不代表实际调用已经验证。`bind` 保存用户提供的精确 ID，保存不等于已验证；自动发送前仍核实存在。未知提供器或不能访问的存储不会回退到 latest。短探针属于可选诊断，真实发送和续接可能产生模型费用。
