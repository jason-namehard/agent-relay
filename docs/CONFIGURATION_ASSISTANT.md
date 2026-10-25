# Agent 配置、按需规则与文件回执

## 统一节点录入

节点仅需自选名称；宿主、配置路径、CLI、版本与指令文件按实际情况提供。接入提示词区分会话记录、配置文件和 CLI 调用方法，要求只读调查帮助和非敏感设置，不先执行模型调用。

Agent 可将完整配置保存为 JSON 后，通过发布包的本机接口填写：

```json
{"name":"我的助手","host":"manual","config_path":null,"cli_path":null,"host_version":null,"instruction_paths":[]}
```

```powershell
dotnet .\relay.dll --home <中转站目录> agent setup --manifest <配置JSON路径>
# 更新既有节点时额外提供 --id <节点ID>
```

新节点 ID 自动生成。直写成功后无需再在新增窗口重复保存；无法运行 CLI 时，将 JSON 粘贴到 Agent 录入窗口解析，只填写草稿，由用户保存。未知路径留 null，不用会话日志代替配置文件。旧 `relay_setup_version/context/fields` 录入协议仍可解析。

## 一行常驻规则

Agent 管理中选择宿主实际加载的指令文件，预览后明确确认才写入。Relay 在自有标记区块内加入一行：需要交接文件或收到交接时，先阅读稳定入口。详细规则放在中转站之外的稳定指引文件中，迁移后刷新当前位置与节点目录。

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

复制给 B 的短提示只指向上下文文件。该文件与回执在任务的隐藏 `.relay` 子目录中；上下文给出当前文件、工作要求、回稿位置、回执位置及模板，不把协议正文反复复制到用户消息里。

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
- `done` 且 `needs_reply: true`：必须给出真实 `result_file` 与具体 `next_action`；任务继续等待另一方，界面提供相应复制按钮。
- `failed`：任务失败，说明待处理原因；不能自动开启新一轮。
- 达到四轮上限：停止继续传递，交由用户判断。

GUI 通过文件事件读取回执，启动和手动刷新也会补读。普通文件快照只能记录变更，不能代替 Agent 的明确完成声明。GUI 关闭后没有常驻监视进程，重新打开会读取待处理回执。

## 高级 CLI 接口扩充

新 GUI 文件交接当前为手动路线，以下能力与它分开。原有 CLI/MCP 自动消息投递仍可使用已验证调用方案。

调用方案包含 `version/name/host/create/resume/dependencies/supported_versions/source/license/limitations`；命令含 `executable`、`arguments` 数组、可选 `input` 和 `working_directory`。`{prompt}` 为必需模板变量，续接还需 `{session_id}`。完整结构以界面复制的实际协议为准。

```powershell
dotnet .\relay.dll --home <中转站目录> cli-recipe import --agent <节点ID> --manifest <调用方案JSON>
```

导入只保存，不执行、安装或启用。JSON 不是通用桥接器：目标必须已有可用 CLI 或独立桥接程序，其 stdout 回执为 `{"session_id":"真实标识","reply":"真实回复","accepted":true}`。探针核对短标识，机器通过后还需用户在真实宿主中确认可见。续接必须返回原会话 ID 并单独确认。真实调用可能收费，不能保证缓存或所有未来会话可见。

字段、路径、正文与外部诊断保持原样。不要把用户实际会话记录、认证或私有路径提交到公开仓库。
