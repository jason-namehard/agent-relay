# Phase 2 — First Real CLI Dispatch

日期：2026-10-22。本机平台为 Windows 11 `10.0.22631`，.NET SDK `10.0.402`，AMQ `0.104.0`，真实 Agent 宿主为 Codex CLI `0.163.0-alpha.11`。

## 交付能力

Phase 2 接通两个独立 CLI 进程的真实工作流：A 生成中文交接指令，Relay 检查 A→B 权限并通过 AMQ 入队，单次 worker 领取精确消息，运行 B 并提交结构化完成/失败声明。A 不继续生成 token 查询结果。

新增 `agent verify-executor`、`agent executors`、`task run`、`task execution` 和 `dispatch/approve --execute`。原生 CLI 路径、版本及配置目录绑定在执行器记录内；worker 领取、尝试、会话和租期在 SQLite 中持久保存。重复执行请求不能重新启动模型。

Windows 使用隐藏的独立进程启动有界 worker，派发终端在启动后退出，worker 写独立报告。没有常驻队列消费者、文件 watcher、后台服务或循环查询。正常退出码、文件变化或 drain 回执都不能替代最终声明。

## 已核实的本机结果

| 场景 | 观察结果 |
| --- | --- |
| 两个真实 CLI Agent | A 写任务，B 计算 `4 + 9 + 16 + 25 = 54` 并返回 `RELAY-PHASE2-54`；任务最终为 `succeeded` |
| 明确失败 | B 收到缺少 sales 输入的任务，返回 `missing_input` 和失败报告；最终为 `failed`，不伪造数据 |
| 派发立即返回 | 可复现脚本中的 Windows 自动派发约 `0.48 s` 返回等待响应，B 随后独立执行 |
| 真实执行超时 | 执行上限设为 1 秒，没有最终声明时保留 `execution_unknown`；新进程 `recover` 后整体任务为 `timed_out` |
| 重复运行 | 已有尝试保留同一执行/会话记录，不再取件或启动模型 |
| 中文传递 | 显式 UTF-8 子进程 stdin/stdout；AMQ 中文验证标记和真实中文任务/结果均可往返 |

真实调用通过本机已有认证完成；测试指令和产物在独立受管目录中，没有修改用户的 Agent 规则、插件、MCP 或配置文件。原始请求是独立测试数据，没有上传项目文件或认证文件。

可复现运行的任务为 `af166451-86c1-460d-b8c3-773117c83ed6`；A 会话 `01a162e5-69b1-7ba3-bd99-d94a3dfd0487`，B 会话 `01a162e7-42bf-7851-88e9-9818eabdab4a`。`summary.json` 记录 `status=passed`、`result=54` 和 `duplicate_run=deduplicated`；测试日志保存在运行目录，不包含在源代码或发布包中。

## 自动回归

当前 44 项测试通过，包含 Phase 1 的持久状态测试，以及单次取件/执行、124 次重复 worker、并发领取、明确失败、漏回调、领取后崩溃、结构化输出契约、消息不匹配、旧尝试、权限撤销、UTF-8、原生子进程超时和派发前参数校验。

普通 Windows/Ubuntu CI 运行确定性测试、锁定恢复、许可检查及打包；不使用账户凭据，不调用真实模型。真实工作流用 `scripts/verify-real-agents.ps1` 在开发者显式运行时验证，会消耗模型额度。脚本使用单次进程等待，没有等待结果文件的轮询循环，并生成 `summary.json`。

```powershell
pwsh ./scripts/verify-real-agents.ps1 -Dotnet dotnet -Codex C:/tools/codex.exe -Amq C:/tools/amq/amq.exe -ConfigPath C:/Users/you/.codex/config.toml
```

已有 Phase 1 数据目录先运行 `relay init` 创建新增表；任务和权限保留。执行器在验证后使用注册配置文件所在目录作为 `CODEX_HOME`。`--ignore-user-config --ephemeral --sandbox read-only` 是当前明确的执行模式，不加载 config.toml 的 provider/model/MCP 设置；自定义 provider、写代码和既有会话继续需后续适配。

## 设计依据与边界

复用 [Codex 官方非交互接口](https://developers.openai.com/codex/noninteractive) 的 JSONL、结构化输出和只读沙箱，通过用户指定的原生二进制调用；没有复制 Codex 或 AMQ 源码、引入新依赖或捆绑外部 Agent。命令行登录态由原 CLI 管理，不复制认证到仓库或公共 CI。

子进程编码显式使用 [.NET StandardInputEncoding](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.standardinputencoding?view=net-10.0)。Windows 单次 worker 独立于派发终端，避免依赖父进程管道生命周期；相关平台行为见 [.NET runtime issue #19569](https://github.com/dotnet/runtime/issues/19569)。

本阶段验证的是同一宿主上的两个独立 Agent，不代表 Claude、OpenCode 或所有 Codex 版本都已支持。会话自动恢复、桌面通知和可选单次系统任务属于必须推进的 Phase 3；当前继续原任务使用持久 Inbox 和复制提示词。
