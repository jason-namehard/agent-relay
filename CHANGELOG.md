# Changelog

## 0.1.0-preview.2 — 2026-10-23

- 六种桌面语言、即时切换和偏好持久化。
- 四套莫兰迪主题、自定义强调色、圆角控件与原生图标。
- 字段级复制接入提示词、整组配置助手及严格 JSON 解析代填；独立保存、验证与批准。
- 配置回复、语言切换、主题和代填边界回归验证。

使用流程见 [配置助手与外观](docs/CONFIGURATION_ASSISTANT.md)。

## 0.1.0-preview.1 — 2026-10-23

首个公开预览版，覆盖本机任务交接的 CLI、MCP 和 Windows GUI。

- 稳定 Agent 登记、路径验证及 A→B 有向权限，默认拒绝。
- SQLite 任务、显式 ack/start/complete/fail、幂等、审计、受管 Markdown 与 TTL 清理。
- 可选 AMQ 运输与单次只读 Codex 执行；精确 Codex 会话继续。
- Windows 通知、持久 Inbox 与提示词复制。
- 绑定 Agent 身份的七个 stdio MCP 工具。
- JSON/TOML 迁移的来源记录、精确预览、批准、备份、证明与回滚；旧入口单独批准停用。
- WPF 管理界面、有向连接图、事件刷新、任务证据与设置。
- 隔离演示模式、首次使用入口、Windows 完整包和 CLI/MCP 包、标签触发的 Release 工作流。

预览版边界与运行要求见 [上手指南](docs/GETTING_STARTED.md) 和 [README](README.md#已知边界)。
