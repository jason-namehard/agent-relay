# Agent Relay 发布包

完整解压 Windows 包后，正式运行 `gui/AgentRelay.Desktop.exe` 配置真实 Agent。`TryDemo.cmd` 提供隔离样本，会禁止真实 CLI 调用；需要保留演示中登记的数据，可在设置转为正式使用。

CLI 最短操作：分别配置 A、B → 看板“新建 CLI 任务” → 选择来源及目标会话 → 直接填写工作要求 → 按需选择持续修改与验收（作者、验收标准、验收轮数）和自动投递 → 明确允许所需方向 → 创建并发送。软件准备任务提示，不要求先生成文件或 JSON。实际调用可能产生模型费用。

已有文件使用“新建交接”：粘贴短 JSON 或自己选文件，再通过启用的 CLI 路线或手动复制投递。可安装一句话按需规则，帮助 Agent 输出交接格式。回执推动状态：单次交接默认最多 4 轮；持续修改与验收在作者确认接受后交付。轮数用完、单轮超时或无进展时暂停，等你选择继续、取消或标记完成。格式见 `CONFIGURATION_ASSISTANT.md`，详细上手见 `GETTING_STARTED.md`。

Agent 管理只有一个用户名称；一键配置复制的是独立新建请求，不覆盖当前选中节点。编辑已有节点请使用“编辑 Agent”。CLI 调用配置为分区字段，支持原生 JSON/JSONL/文本适配；导入方案不启动模型。手动 CLI 发送不要求测试；自动投递要求探针（含写文件检查）通过并确认可见，或一次真实交接写回了回执。真实调用可能产生费用；已有会话必须明确选择，执行前核实存储，不能用最近会话替代。

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

最小化按钮缩到任务栏；只有 × 按“关闭时最小化到托盘”设置隐藏或退出。首次点击 × 询问并记住选择，以后不再询问，设置可修改。托盘“打开”恢复窗口，“退出”始终关闭程序。

设置可选择提示音、试听和测试通知。等待处理、回传和完成等新事件按任务合并提醒；通知程序使用当前包内路径。运行时显示标记为“应用内通知”的右下角提醒；Windows 通知中心可用时静默记录，不重复弹系统横幅，系统记录失败不影响应用内提示。测试通知不创建业务任务或调用模型。

程序代码 MIT。再分发前阅读 `DISTRIBUTION_TERMS.md`、`THIRD_PARTY_NOTICES.md` 和 `licenses/` 中各组件条款。
