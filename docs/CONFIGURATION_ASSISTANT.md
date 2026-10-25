# Agent 配置、按需规则与文件回执

## 统一节点录入

节点只需一个自选名称。Agent 管理工具栏为“新建 Agent、编辑 Agent、删除 Agent、Agent CLI 测试、一键配置”。宿主类型属于内部适配信息，不要求用户另填软件名称。

推荐点击“一键配置”，将复制的提示词粘贴到目标 Agent 的新对话窗口。提示词带独立请求标识和当前节点目录，要求 Agent 先读取最新目录，再创建新节点；新建命令不传 `--id`，不会把当前选中的节点当作写入目标。调查仅读取帮助和非敏感设置，不启动真实模型。可用调用方式随后直接导入同一个新节点。

Agent 可将完整配置保存为 JSON 后，通过发布包的本机接口填写：

```json
{"name":"我的助手","host":"manual","config_path":null,"cli_path":null,"host_version":null,"instruction_paths":[]}
```

```powershell
dotnet .\relay.dll --home <中转站目录> agent setup --manifest <配置JSON路径>
# 更新既有节点时额外提供 --id <节点ID>
```

新节点 ID 自动生成；只有明确更新时才指定已有 `--id`。直写成功后无需在新建窗口重复保存。未知路径留 null，不用会话日志代替配置文件；`host` 可省略，默认 `manual`。

也可使用纯手动表单：名称、可选配置/指令文件，以及独立的 CLI 调用区。CLI 区分别填写程序、每行一个参数、可选标准输入模板、输出格式和工作目录。参数或标准输入必须包含 `{prompt}`；只记录程序路径时仍需补齐调用方式才能测试。GUI 不要求粘贴和解析一整段 Agent 配置。

演示模式会禁用真实探针，这不表示 CLI 失败。正式配置请运行 `gui/AgentRelay.Desktop.exe`；已在演示中登记的资料可从设置保留当前数据并转为正式使用。

## 一行常驻规则

在 Agent 管理选中节点，展开“交接规则与节点详情”，选择宿主实际加载的指令文件，预览后明确确认才写入。Relay 在自有标记区块内加入一行：需要交接文件或收到交接时，先阅读稳定入口。详细规则放在中转站之外的稳定指引文件中，迁移后刷新当前位置与节点目录。

这一行仍占用少量上下文；详细内容按需读取，不保证任意宿主都会自动触发。用户后续编辑会保留。每次安装保留原始备份，移除仅剥离本工具区块，不用旧备份覆盖整份用户文件。

```powershell
dotnet .\relay.dll --home <中转站目录> guidance preview --agent <节点ID> --file <指令文件>
dotnet .\relay.dll --home <中转站目录> guidance install --agent <节点ID> --file <指令文件> --approve
dotnet .\relay.dll --home <中转站目录> guidance remove --all
```

删除便携目录不会执行卸载程序，删除前需在 GUI 或 CLI 移除规则。

## A 输出文件交接 JSON

让 A 先写好文件，再输出如下对象供用户粘贴。`from`、`to` 是已登记 ID 或唯一名称；`file` 是存在且可读的绝对路径；`message` 为可选的一句工作要求，不放完整正文。

```json
{"from":"节点甲","to":"节点乙","file":"C:/work/handoff.md","message":"请检查并按结论修改"}
```

示例路径须换成真实文件。Markdown、压缩包等均按文件处理。用户点击“解析并创建交接”时记录本次任务；发送方和接收方不能相同。相同源文件的同方向活动交接会复用，不重复创建。

也可手动选节点与文件，不要求先写规则。窗口只有复制格式和解析操作，没有额外的“粘贴回复”按钮。

## B 写回执，软件更新状态

给 B 的短提示只指向上下文文件，可通过手动复制或已启用的 CLI 路线投递。该文件与回执在任务的隐藏 `.relay` 子目录中；上下文给出当前文件、工作要求、回稿位置、回执位置及模板，不把协议正文反复复制到用户消息里。

回执结构：

```json
{
  "task_id":"上下文中的任务ID",
  "attempt_id":"上下文中的尝试ID",
  "sender":"当前等待的节点ID",
  "turn":0,
  "status":"done",
  "needs_reply":false,
  "result_file":null,
  "next_action":null
}
```

任务 ID、尝试 ID、发送节点与轮次必须采用当前上下文原值。可先写 `received` 或 `working` 表示收到/处理，再写 `done` 或 `failed`。旧轮次不覆盖新轮次，错误或未写完的 JSON 不会当作完成。

- `done` 且 `needs_reply: false`：任务结束，无需回复寒暄或重复确认。
- `done` 且 `needs_reply: true`：必须给出真实 `result_file` 与具体 `next_action`；任务继续等待另一方，按下一方向的设置自动投递或提供复制按钮。
- `failed`：任务失败，说明待处理原因；不能自动开启新一轮。
- 达到四轮上限：停止继续传递，交由用户判断。

GUI 通过文件事件读取回执，启动和手动刷新也会补读。已接收或正在处理时提示等待回执，不再把重复复制列为下一步。普通文件快照只能记录变更，不能代替 Agent 的明确完成声明。所有进程退出后没有常驻监视进程；最小化到托盘时 GUI 仍在运行。

任务可以取消，也可以在确认后删除账本记录；删除保留磁盘文件，之后仍可按保留期清理。人工标记完成会记录用户决定并停止等待，不代表获得 Agent 完成证据，也不会停止已运行的外部进程。

## 高级 CLI 接口扩充

文件交接在接收方 CLI 新会话探针通过且用户确认可见、发送方向明确启用时自动投递。首次创建和需要对方继续的回合都按当前方向判断；条件不足时保留手动复制。每轮只提交一次，投递状态未知时先检查真实会话，不自动重发。当前文件投递创建新会话，同会话探针独立验证续接能力。

方案可由一键配置直接导入，或在高级设置中手动导入。包含 `version/name/host/output_format/create/resume/dependencies/supported_versions/source/license/limitations`；命令含 `executable`、`arguments` 数组、可选 `input`、`working_directory` 和非敏感 `environment`。`{prompt}` 为必需模板变量，续接还需 `{session_id}`。

```powershell
dotnet .\relay.dll --home <中转站目录> cli-recipe import --agent <节点ID> --manifest <调用方案JSON>
```

导入只保存，不启动模型、安装程序或启用连接。Relay 负责读取下列原生输出，目标程序无需改成专用回执格式：

| `output_format` | 对应输出 |
| --- | --- |
| `dsh-jsonl` | DSH 的 JSONL 事件流 |
| `zcode-json` / `zcode-jsonl` | Zcode 的 JSON 对象或 JSONL 事件流 |
| `hermes-jsonl` | Hermes 的流式 JSON 事件 |
| `claude-json` | 采用相应结构的 CodeBuddy/WorkBuddy 等 JSON 输出 |
| `text` | 仅最终回复的普通文本；没有真实会话 ID 时无法验证同会话续接 |
| `relay-json` | 已有桥接的 `{"session_id":"真实标识","reply":"真实回复","accepted":true}` |

选择已查证的输出格式，参数必须与安装版本的帮助及实际输出匹配。内置格式解析不等于所有版本或所有宿主都已接通。探针核对 Agent 的真实短标识回复，机器通过后还需在宿主中确认会话可见；续接需返回原会话 ID 并单独确认。真实调用可能收费，不能保证缓存或所有未来会话可见。

连接图节点绿点表示已收录调用方案，悬停区分待测与已验证；探针失败为红点，无方案为灰点。线路箭头及开关表示方向状态。节点绿点本身不代表已获自动投递权限。

字段、路径、正文与外部诊断保持原样。不要把用户实际会话记录、认证或私有路径提交到公开仓库。
