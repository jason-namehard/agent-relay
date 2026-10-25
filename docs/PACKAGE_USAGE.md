# Agent Relay 发布包

完整解压 Windows 包后，正式运行 `gui/AgentRelay.Desktop.exe`；`TryDemo.cmd` 提供隔离样本，无需模型账号，不调用真实模型。新建 GUI 文件交接当前为手动路线。

最短操作：添加两个自选名称节点 → 可选安装按需交接规则 → 让 A 写文件并输出交接 JSON → 粘贴“解析并创建交接” → 复制短提示给 B → B 写回执后自动更新。需要对方继续时给真实回稿和下一步，不需要就结束；最多四轮。格式见 `CONFIGURATION_ASSISTANT.md`，详细上手见 `GETTING_STARTED.md`。

规则安装只在用户确认后修改所选宿主指令文件，常驻一行仍占少量上下文。删除便携文件夹前先在设置移除全部规则，或用 `guidance remove --all`；直接删除不会执行卸载清理。

GUI/通知自带运行库；CLI 和 `mcp/relay-mcp.dll` 需要 .NET 10 runtime。发布包不包含目标 Agent、账号或桥接程序。JSON 配置不等于可调用接口，真实 CLI 探针可能产生费用。

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

程序代码 MIT。再分发前阅读 `DISTRIBUTION_TERMS.md`、`THIRD_PARTY_NOTICES.md` 和 `licenses/` 中各组件条款。
