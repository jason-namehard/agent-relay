# Agent Relay 发布包

完整解压 Windows 包后，正式运行 `gui/AgentRelay.Desktop.exe` 配置真实 Agent。`TryDemo.cmd` 提供隔离样本，会禁止真实 CLI 调用；需要保留演示中登记的数据，可在设置转为正式使用。

最短操作：分别一键配置 A、B（或按字段手动新建）→ 可选安装按需交接规则 → 让 A 写文件并输出短 JSON → 粘贴“解析并创建交接” → 已验证且启用方向的 CLI 自动发送，否则复制短提示给 B → B 写回执后自动更新。需要对方继续时给真实回稿和下一步，不需要就结束；最多四轮。格式见 `CONFIGURATION_ASSISTANT.md`，详细上手见 `GETTING_STARTED.md`。

Agent 管理只有一个用户名称；一键配置复制的是独立新建请求，不覆盖当前选中节点。编辑已有节点请使用“编辑 Agent”。CLI 调用配置为分区字段，支持原生 JSON/JSONL/文本适配；导入方案不启动模型，真实短探针可能产生费用。探针通过还需确认会话可见和启用连接方向。

规则安装只在用户确认后修改所选宿主指令文件，常驻一行仍占少量上下文。删除便携文件夹前先在设置移除全部规则，或用 `guidance remove --all`；直接删除不会执行卸载清理。

GUI/通知自带运行库；CLI 和 `mcp/relay-mcp.dll` 需要 .NET 10 runtime。发布包不包含目标 Agent、账号或桥接程序。JSON 配置不等于可调用接口，真实 CLI 探针可能产生费用。

任务详情允许取消、确认删除记录以及人工标记完成。删除不直接删文件，后续仍可按保留期清理；人工完成不验证 Agent 的工作，也不停止已运行的 Agent。CLI 结果不确定时先检查真实会话，不重复发送同一轮。

在解压目录用 PowerShell：

```powershell
$relay = Join-Path $PWD 'relay.dll'
$dataRoot = Join-Path $PWD '.relay'
dotnet $relay --home $dataRoot init
dotnet $relay --help
.\gui\AgentRelay.Desktop.exe --home $dataRoot
```

原 CLI/MCP 消息交接、自动投递和精确会话路线仍保留，旧任务不会自动转为新文件任务。MCP 由宿主启动：`dotnet .\mcp\relay-mcp.dll --home <目录> --agent <已登记ID>`，不实现 `--help`。高级说明见公开仓库的 [README](https://github.com/jason-namehard/agent-relay#readme)。

文件回执位于任务的隐藏 `.relay` 子目录，GUI 打开时观察事件，启动和刷新补读。所有进程关闭后无后台监控。迁移要求没有未完成任务，复制文件与账本后切换，旧站备份不自动清理；无 pending 的迁移校验不会假称成功。

最小化窗口会隐藏到托盘并继续运行；托盘“打开”恢复窗口，“退出”关闭。右上角关闭按钮正常退出。

程序代码 MIT。再分发前阅读 `DISTRIBUTION_TERMS.md`、`THIRD_PARTY_NOTICES.md` 和 `licenses/` 中各组件条款。
