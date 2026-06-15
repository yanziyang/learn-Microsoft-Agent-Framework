<div align="center">

<img src="./assets/hero.png" alt="Microsoft Agent Framework 实战指南" width="100%" />

# Microsoft Agent Framework &nbsp;·&nbsp; 实战指南

**用 .NET 10 / C# 构建生产级 AI Agent 的 20 章渐进式教程**
*从一次 `IChatClient` 调用，到一支全链路可观测的多 Agent 舰队。*

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-latest-239120?style=flat-square&logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![MAF](https://img.shields.io/badge/Microsoft.Agents.AI-v1.10.0-7B61FF?style=flat-square)](https://www.nuget.org/packages/Microsoft.Agents.AI)
[![MEAI](https://img.shields.io/badge/Microsoft.Extensions.AI-v10.7.0-0078D4?style=flat-square)](https://www.nuget.org/packages/Microsoft.Extensions.AI)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](./LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen?style=flat-square)](https://github.com/yanziyang/learn-Microsoft-Agent-Framework/pulls)

[English](./README.md) &nbsp;·&nbsp; **[中文](./README-zh.md)**

[快速开始](#快速开始) &nbsp;·&nbsp; [架构概览](#架构概览) &nbsp;·&nbsp; [章节地图](#章节地图--20-课) &nbsp;·&nbsp; [学习路径](#学习路径) &nbsp;·&nbsp; [配置说明](#配置说明)

</div>

---

> **为什么是这个仓库？**
> Microsoft Agent Framework (MAF) 与 Microsoft.Extensions.AI (MEAI) 是 .NET 生态官方推出的生产级 Agent 技术栈。本仓库用 **20 个自包含的章节** 把它*全貌*拆开来讲 &mdash; 提供商无关的 Chat 抽象、中间件管道、工具调用、人机审批、工作流、A2A、MCP、评估、托管、可观测性 &mdash; 每章都能在几分钟内运行、阅读、改写。

每个 `sNN_*` 文件夹都是一个独立的 `Program.cs` 控制台程序：没有共享库，没有隐藏魔法，直接用 MAF/MEAI NuGet 包，把概念一个个串起来。默认引擎兼容 **OpenAI 协议**，可切换到任意提供商。

---

## 架构概览

![Microsoft Agent Framework 架构图：应用层调用 MAF，MAF 构建在 MEAI 之上，MEAI 抽象底层各家 Provider SDK](./assets/architecture.png)

**自上而下的三层可组合架构：**

1. **应用层** &mdash; 控制台、ASP.NET Core、后台服务、CLI 或桌面 UI。
2. **Microsoft Agent Framework (MAF)** &mdash; 编排、工作流、多 Agent 拓扑、A2A 与 MCP。
3. **Microsoft.Extensions.AI (MEAI)** &mdash; 统一的 `IChatClient` 抽象：中间件、工具、压缩器、评估器。
4. **Provider SDKs** &mdash; OpenAI、Azure OpenAI、Ollama 或任意 `IChatClient` 实现。**换提供商不改业务代码**。

---

## 快速开始

```bash
# 1. 克隆仓库
git clone https://github.com/yanziyang/learn-Microsoft-Agent-Framework.git
cd learn-Microsoft-Agent-Framework

# 2. 配置 API Key（一次性）
cp appsettings.example.json appsettings.json
#    编辑 appsettings.json，把 PUT-YOUR-KEY-HERE 替换成你的 Key
#    或直接设置环境变量：export OPENAI_API_KEY=sk-...

# 3. 还原 + 编译整个解决方案
dotnet build

# 4. 运行任意章节
dotnet run --project s01_provider_agnostic   # 起点：一次调用，任意提供商
dotnet run --project s03_agent_loop          # Agent 循环本体
dotnet run --project s20_comprehensive       # 终章：全部机制汇总成一个程序
```

> 每一章都是独立的 &mdash; 你完全可以直接跳到 `s13_workflows` 或 `s17_mcp_integration`，不必按顺序走。

---

## 章节地图 &mdash; 20 课

四个递进的阶段。挑一行，运行项目，看代码。

### 阶段一 &middot; 基础篇 &nbsp;`s01 – s06`

> Chat 客户端、中间件管道、Agent 循环、工具调用、审批、钩子。

| # | 章节 | 核心概念 | 技术栈 |
|---|---------|-------------|-------|
| 01 | [`s01_provider_agnostic`](./s01_provider_agnostic/) | `IChatClient`、切换提供商、流式输出 | MEAI |
| 02 | [`s02_middleware_pipeline`](./s02_middleware_pipeline/) | `DelegatingChatClient`、自定义中间件 | MEAI |
| 03 | [`s03_agent_loop`](./s03_agent_loop/) | `ChatClientAgent`、会话、`RunAsync` / `RunStreamingAsync` | MAF |
| 04 | [`s04_tool_use`](./s04_tool_use/) | `AIFunctionFactory.Create()`、工具分发 | MEAI |
| 05 | [`s05_permission`](./s05_permission/) | `ApprovalRequiredAIFunction`、审批循环 | MAF |
| 06 | [`s06_hooks`](./s06_hooks/) | 通过中间件实现工具前/后钩子 | MEAI |

### 阶段二 &middot; Agent 能力篇 &nbsp;`s07 – s12`

> 任务规划、Agent 组合、技能加载、上下文压缩、动态提示词、错误恢复。

| # | 章节 | 核心概念 | 技术栈 |
|---|---------|-------------|-------|
| 07 | [`s07_planning`](./s07_planning/) | 自定义 `todo_write` 工具、状态跟踪 | Custom |
| 08 | [`s08_agent_as_tool`](./s08_agent_as_tool/) | `AIAgent.AsAIFunction()`、嵌套组合 | MAF |
| 09 | [`s09_skill_loading`](./s09_skill_loading/) | 两级技能注入、`SKILL.md` 目录 | Custom |
| 10 | [`s10_context_compaction`](./s10_context_compaction/) | `MessageCountingChatReducer`、`SummarizingChatReducer` | MEAI |
| 11 | [`s11_system_prompt`](./s11_system_prompt/) | 动态系统提示词拼装、缓存 | Custom |
| 12 | [`s12_error_recovery`](./s12_error_recovery/) | 重试中间件、指数退避 | MEAI |

### 阶段三 &middot; 编排与集成 &nbsp;`s13 – s17`

> 工作流、后台任务、多 Agent 拓扑、A2A 协议、MCP 集成。

| # | 章节 | 核心概念 | 技术栈 |
|---|---------|-------------|-------|
| 13 | [`s13_workflows`](./s13_workflows/) | `WorkflowBuilder`、Executor、Edge、Superstep | MAF |
| 14 | [`s14_background_tasks`](./s14_background_tasks/) | `BackgroundService`、异步执行 | .NET |
| 15 | [`s15_multi_agent_workflows`](./s15_multi_agent_workflows/) | `AgentWorkflowBuilder`、串行 / 并发 | MAF |
| 16 | [`s16_a2a_protocol`](./s16_a2a_protocol/) | A2A 协议、`AgentCard` | MAF |
| 17 | [`s17_mcp_integration`](./s17_mcp_integration/) | `McpClient`、`McpClientTool`、内存版服务器 | MCP |

### 阶段四 &middot; 生产与综合 &nbsp;`s18 – s20`

> 评估、托管与可观测性，以及把所有机制汇成一个完整参考架构。

| # | 章节 | 核心概念 | 技术栈 |
|---|---------|-------------|-------|
| 18 | [`s18_evaluation`](./s18_evaluation/) | `CoherenceEvaluator`、`RelevanceEvaluator` | MEAI |
| 19 | [`s19_hosting_observability`](./s19_hosting_observability/) | ASP.NET Core 托管、OpenTelemetry | MAF + OTel |
| 20 | [`s20_comprehensive`](./s20_comprehensive/) | s01–s19 全部机制汇总到一个程序 | All |

---

## 框架版本

| 包 | 版本 | 状态 |
|---------|---------|--------|
| `Microsoft.Extensions.AI` | **10.7.0** | 正式版 |
| `Microsoft.Agents.AI` | **1.10.0** | 正式版 |
| `Microsoft.Agents.AI.Workflows` | **1.10.0** | 正式版 |
| `Microsoft.Agents.AI.Hosting` | **1.10.0-preview** | 预览版 |
| `ModelContextProtocol` | **1.4.0** | 正式版 |

NuGet 版本统一在 [`Directory.Packages.props`](./Directory.Packages.props) 集中管理。所有项目均目标 `net10.0`，并通过 [`Directory.Build.props`](./Directory.Build.props) 共享 `<LangVersion>latest</LangVersion>`、`<Nullable>enable</Nullable>`、`<ImplicitUsings>enable</ImplicitUsings>` 等配置。

---

## 配置说明

每一章都从 `appsettings.json` 或环境变量读取配置 &mdash; **同样的结构，同样的解析顺序**。

```json
{
  "baseUrl": "https://api.openai.com/v1",
  "modelId": "gpt-4o-mini",
  "apiKey":  "PUT-YOUR-KEY-HERE"
}
```

**Key 解析顺序：**
1. `appsettings.json` 里的 `apiKey`（只要不是默认的 `PUT-YOUR-KEY-HERE`）
2. `OPENAI_API_KEY` 环境变量

> **按章覆盖：** 把 `sNN_*/appsettings.example.json` 复制为 `sNN_*/appsettings.json` 再编辑。所有 `s*/` 下的 `appsettings.json` 都已在 `.gitignore` 里 &mdash; **千万不要提交它**。

默认引擎兼容 OpenAI 协议，但 `baseUrl` 可以指向任意 OpenAI 兼容端点（Azure OpenAI、Ollama、vLLM、llama.cpp server、DeepSeek 等）。

---

## 项目结构

```text
learn-Microsoft-Agent-Framework/
├── s01_provider_agnostic/      # IChatClient 抽象
├── s02_middleware_pipeline/    # DelegatingChatClient 中间件
├── s03_agent_loop/             # ChatClientAgent
├── s04_tool_use/               # AIFunctionFactory
├── s05_permission/             # ApprovalRequiredAIFunction
├── s06_hooks/                  # 中间件钩子
├── s07_planning/               # todo_write 工具
├── s08_agent_as_tool/          # Agent 组合
├── s09_skill_loading/          # SKILL.md 目录加载
├── s10_context_compaction/     # 上下文压缩
├── s11_system_prompt/          # 动态提示词
├── s12_error_recovery/         # 重试中间件
├── s13_workflows/              # MAF 工作流
├── s14_background_tasks/       # 后台执行
├── s15_multi_agent_workflows/  # 多 Agent 编排
├── s16_a2a_protocol/           # A2A 协议
├── s17_mcp_integration/        # MCP 集成
├── s18_evaluation/             # 质量评估
├── s19_hosting_observability/  # ASP.NET Core + OpenTelemetry
├── s20_comprehensive/          # 终章：全部机制汇总
│
├── assets/                     # Hero、架构、学习路径图
├── skills/                     # s09 使用的 SKILL.md 资源
├── docs/en/                    # 英文文档
├── docs/zh/                    # 中文文档
├── web/                        # Next.js 文档站点
│
├── Directory.Build.props       # 共享 MSBuild 配置
├── Directory.Packages.props    # NuGet 版本集中管理
└── LearnClaudeCode.slnx        # 解决方案文件
```

---

## 学习路径

![20 章节学习路径，按四个递进阶段分组](./assets/learning-path.png)

**推荐路线：** 从 **阶段一** 开始，把 `IChatClient`、中间件、Agent 循环这三块内化。进入 **阶段二** 加上规划、组合、容错。需要编排、多 Agent 或外部工具服务器时再读 **阶段三**。最后用 **阶段四** 收尾：评估、托管、以及一份参考实现。

> 已经熟悉 MEAI？快速浏览 s01-s02，直接跳到 `s03_agent_loop`。想先看个总览？跑一遍 `s20_comprehensive`，所有机制会在同一个程序里依次亮起来。

---

## 验证命令

仓库里没有 `dotnet test` 项目，也没有配置 lint 工具 &mdash; `dotnet build` 是唯一的静态检查。

```bash
dotnet build                                    # 编译整个解决方案
dotnet run --project s03_agent_loop             # 跑单个章节
dotnet run --project s19_hosting_observability  # ASP.NET Core 托管 + OTel
cd web && npm install && npm run dev             # 文档站点跑在 http://localhost:3000
```

---

## 致谢

本项目受到以下开源项目的启发并参考了其内容：

[learn-claude-code](https://github.com/shareAI-lab/learn-claude-code) by [ShareAI Lab](https://github.com/shareAI-lab)

非常感谢原项目作者和贡献者们与社区分享他们的工作。本项目在其仓库中呈现的创意、概念和/或实现方法的基础上进行了扩展和改进。

请参阅原始项目以获取更多文档、更新和许可证信息。

---

## 许可证

[MIT](./LICENSE) &copy; 各位贡献者。欢迎 PR &mdash; 发现拼写错误、过时 API，或者觉得某一章可以写得更好，请直接开 Issue。

<div align="center">

**构建 Agent，组合中间件，自信交付。**

</div>
