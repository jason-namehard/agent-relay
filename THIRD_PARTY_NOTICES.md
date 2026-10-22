# 第三方来源与许可

Agent Relay 的应用代码按本仓库 MIT License 提供。研究对象不是本项目源码的授权来源；没有把 AMQ、Multiagent Handoff、Agents Handoff、AgentBus、ACP Router 或 CC Switch 的应用源码复制进本仓库。设计依据及固定研究提交见 [研究文档](docs/ROADMAP_AND_RESEARCH.md)。

## 分发依赖

实际版本以各项目 `packages.lock.json` 为准。恢复后运行 `pwsh ./scripts/check-licenses.ps1`，读取 NuGet 包自身的 `.nuspec`，检查全部直接和传递依赖的许可声明；不是仅检查顶层包。

| 组件 | 版本 | 许可与版权 | 用途 |
| --- | --- | --- | --- |
| Microsoft.Data.Sqlite / Microsoft.Data.Sqlite.Core | 10.0.13 | MIT；.NET Foundation and Contributors | SQLite 驱动 |
| System.CommandLine | 2.0.13 | MIT；.NET Foundation and Contributors | 命令行解析及帮助 |
| SQLitePCLRaw.bundle_e_sqlite3 / core / lib.e_sqlite3 / provider.e_sqlite3 | 2.1.13 | Apache-2.0；Copyright 2014–2024 SourceGear, LLC | SQLite 原生桥接及二进制 |
| SQLite | 由 lib.e_sqlite3 包携带 | [上游 public domain 声明](https://www.sqlite.org/copyright.html) | 嵌入式数据库引擎 |

许可全文位于 `licenses/`。Microsoft.Data.Sqlite 来源为 [dotnet/efcore](https://github.com/dotnet/efcore)，System.CommandLine 来源为 [dotnet/command-line-api](https://github.com/dotnet/command-line-api)，SQLitePCLRaw 来源为 [ericsink/SQLitePCL.raw](https://github.com/ericsink/SQLitePCL.raw)。许可证适用于各自组件，不因本项目 MIT 声明而被替换。打包脚本将本文件、项目 LICENSE 和依赖许可全文一起复制到分发目录。

## 开发依赖

MSTest.TestFramework / MSTest.TestAdapter 4.0.2、Microsoft.NET.Test.Sdk 18.0.1 用于构建和测试，不随 CLI 应用分发。其传递依赖由许可检查脚本一并列出；本次恢复的全部开发包声明为 MIT 或 Apache-2.0。具体版本和内容记录在测试项目的 lockfile。

.NET SDK 和运行时遵循 [.NET 许可](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) 及其第三方声明。本项目默认发布 framework-dependent 应用，不打包 .NET 运行时；改成 self-contained 分发时必须连同 runtime 的第三方声明一起核对和保留。

## 外部 AMQ

AMQ 作为用户指定的独立可执行文件，通过公开 CLI 调用，不随本项目发布包捆绑。测试基线为 [AMQ 0.104.0](https://github.com/avivsinai/agent-message-queue/releases/tag/v0.104.0)，上游 MIT，Copyright (c) 2025 Aviv Sinai。若今后捆绑 AMQ，应保留其 LICENSE 并核对其二进制包含的第三方组件声明；当前不把外部 AMQ 的许可误计为本项目独立实现的版权。

新贡献不得复制无明确许可的源码。引入或改写上游代码时，应在变更中记录来源、版本、许可和修改内容，并保留所需声明。仅通过改名或改写语句不能消除原代码的许可义务。
