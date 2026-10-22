# 预览版发布流程

版本由 `Directory.Build.props` 的 `Version` 定义。CLI 的 `--version`、MCP serverInfo、GUI 和分发包使用同一版本；`relay_version=1` 是 JSON 协议版本，不随产品版本更名。

1. 修改版本、CHANGELOG、上手指南中的版本链接，准备 `docs/releases/v<Version>.md`。
2. 提交 main，等待 Windows/Linux CI。Windows 额外运行真实 WPF 控件交互；两端均执行核心回归、锁定恢复、许可检查和包检查。
3. 本地可用 `pwsh ./scripts/package.ps1 -Flavor Windows` 或 `Portable` 生成同结构 ZIP。脚本使用新的 staging 目录，拒绝覆盖同名包；移除旧的构建输出前核对实际路径，或指定新 `-OutputDirectory`。
4. 为通过验证的提交创建带说明的版本标签并推送。标签必须与源版本一致，禁止移动已发布标签。
5. 标签 CI 成功后，Release job 下载两种 ZIP，通过 GitHub CLI 的 `--verify-tag --prerelease --notes-file` 创建预览发布。只有 Release job 有 `contents: write`，普通 PR/测试为只读。
6. 确认公开 Release 的标签、提交与两个附件名称一致。用户下载的是 ZIP 应用附件；GitHub 自动生成的 Source code 是源码。

示例命令（替换版本和目标提交）：

```powershell
git tag -a v0.1.0-preview.1 <verified-commit> -m 'Agent Relay 0.1.0-preview.1'
git push origin v0.1.0-preview.1
```

构建产物不包含认证、宿主原始配置、数据库或迁移备份。Windows 包保留 .NET 与通知组件分发条款；Portable 包不夹带旧 GUI 输出。

若标签工作流失败，先处理具体失败，未公开的构建不算发布成功。已有公开 Release 和同名资产不自动覆盖；后续修复使用新的预览版本。

工作流复用 GitHub 官方 [upload/download artifact](https://docs.github.com/en/actions/concepts/workflows-and-actions/workflow-artifacts) 与 [GitHub CLI release create](https://cli.github.com/manual/gh_release_create)。
