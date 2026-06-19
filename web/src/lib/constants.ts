import type { AgentLayer } from "@/types/agent-data";

export const VERSION_ORDER = [
  "s01",
  "s02",
  "s03",
  "s04",
  "s05",
  "s06",
  "s07",
  "s08",
  "s09",
  "s10",
  "s11",
  "s12",
  "s13",
  "s14",
  "s15",
  "s16",
  "s17",
  "s18",
  "s19",
  "s20",
] as const;

export const LEARNING_PATH = VERSION_ORDER;

export type VersionId = typeof LEARNING_PATH[number];

export const VERSION_META: Record<string, {
  title: string;
  subtitle: string;
  coreAddition: string;
  keyInsight: string;
  layer: AgentLayer;
  prevVersion: string | null;
}> = {
  s01: {
    title: "Provider-Agnostic Client",
    subtitle: "One Interface, Any Provider",
    coreAddition: "IChatClient abstraction",
    keyInsight: "MEAI's IChatClient lets you swap LLM providers by changing config, not code.",
    layer: "tools",
    prevVersion: null,
  },
  s02: {
    title: "Middleware Pipeline",
    subtitle: "Stack Behavior Like Onion Layers",
    coreAddition: "DelegatingChatClient chain",
    keyInsight: "Middleware composes cross-cutting concerns (logging, caching, telemetry) around the chat client.",
    layer: "tools",
    prevVersion: "s01",
  },
  s03: {
    title: "Agent Loop",
    subtitle: "One Agent = One ChatClientAgent",
    coreAddition: "MAF ChatClientAgent",
    keyInsight: "MAF's ChatClientAgent wraps IChatClient into a full agent with sessions and streaming.",
    layer: "tools",
    prevVersion: "s02",
  },
  s04: {
    title: "Tool Use",
    subtitle: "Functions Become Tools Automatically",
    coreAddition: "AIFunctionFactory dispatch",
    keyInsight: "AIFunctionFactory.Create() generates JSON schemas from C# method signatures automatically.",
    layer: "tools",
    prevVersion: "s03",
  },
  s05: {
    title: "Permission",
    subtitle: "Ask Before You Act",
    coreAddition: "ApprovalRequiredAIFunction",
    keyInsight: "Wrap any tool with ApprovalRequiredAIFunction to gate dangerous operations through human approval.",
    layer: "tools",
    prevVersion: "s04",
  },
  s06: {
    title: "Hooks",
    subtitle: "Middleware Is the New Hook Bus",
    coreAddition: "Pre/post tool middleware",
    keyInsight: "DelegatingChatClient middleware replaces hook buses with composable, testable decorators.",
    layer: "tools",
    prevVersion: "s05",
  },
  s07: {
    title: "Planning",
    subtitle: "An Agent Without a Plan Drifts",
    coreAddition: "TodoWrite custom tool",
    keyInsight: "Explicit plans via AIFunction tools keep long-running work visible and correctable.",
    layer: "planning",
    prevVersion: "s06",
  },
  s08: {
    title: "Agent-as-Tool",
    subtitle: "Agents Compose Like Functions",
    coreAddition: "AIAgent.AsAIFunction()",
    keyInsight: "Any agent can become a tool for another agent, enabling hierarchical composition.",
    layer: "planning",
    prevVersion: "s07",
  },
  s09: {
    title: "Skill Loading",
    subtitle: "Load Knowledge on Demand",
    coreAddition: "Two-level skill injection",
    keyInsight: "Inject skill names in the system prompt; load full content only when the model requests it.",
    layer: "planning",
    prevVersion: "s08",
  },
  s10: {
    title: "Context Compaction",
    subtitle: "Context Will Fill Up",
    coreAddition: "MEAI chat reducers",
    keyInsight: "MessageCountingChatReducer and SummarizingChatReducer keep context windows manageable automatically.",
    layer: "memory",
    prevVersion: "s09",
  },
  s11: {
    title: "System Prompt",
    subtitle: "Assembled at Runtime, Never Hardcoded",
    coreAddition: "Dynamic prompt assembly",
    keyInsight: "The system prompt is a generated product of context, tools, and environment state.",
    layer: "planning",
    prevVersion: "s10",
  },
  s12: {
    title: "Error Recovery",
    subtitle: "Errors Are the Start of a Retry",
    coreAddition: "Retry middleware",
    keyInsight: "DelegatingChatClient middleware handles retries with exponential backoff and fallback models.",
    layer: "planning",
    prevVersion: "s11",
  },
  s13: {
    title: "Workflows",
    subtitle: "Graphs Beat Flat Loops",
    coreAddition: "MAF WorkflowBuilder",
    keyInsight: "WorkflowBuilder orchestrates executors with edges, conditional routing, and parallel supersteps.",
    layer: "collaboration",
    prevVersion: "s12",
  },
  s14: {
    title: "Background Tasks",
    subtitle: "Slow Work Goes to the Background",
    coreAddition: "Async tool execution",
    keyInsight: "AIFunctionFactory tools start Task.Run work; <task_notification> messages inject results into the next agent turn.",
    layer: "concurrency",
    prevVersion: "s13",
  },
  s15: {
    title: "Multi-Agent Workflows",
    subtitle: "Teams Built from Workflow Graphs",
    coreAddition: "AgentWorkflowBuilder",
    keyInsight: "AgentWorkflowBuilder.BuildSequential/BuildConcurrent orchestrates multiple agents as a workflow.",
    layer: "collaboration",
    prevVersion: "s14",
  },
  s16: {
    title: "A2A Protocol",
    subtitle: "Agents Talk to Agents",
    coreAddition: "Agent-to-Agent communication",
    keyInsight: "The A2A protocol standardizes how agents discover and communicate with each other.",
    layer: "collaboration",
    prevVersion: "s15",
  },
  s17: {
    title: "MCP Integration",
    subtitle: "External Tools, Standard Protocol",
    coreAddition: "McpClient tool bridge",
    keyInsight: "McpClientTool inherits AIFunction — MCP tools merge seamlessly into any agent's tool pool.",
    layer: "collaboration",
    prevVersion: "s16",
  },
  s18: {
    title: "Evaluation",
    subtitle: "Measure What You Build",
    coreAddition: "MEAI quality evaluators",
    keyInsight: "CoherenceEvaluator, RelevanceEvaluator, and friends score agent responses automatically.",
    layer: "collaboration",
    prevVersion: "s17",
  },
  s19: {
    title: "Hosting & Observability",
    subtitle: "Production-Ready Agents",
    coreAddition: "ASP.NET Core + OpenTelemetry",
    keyInsight: "AddAIAgent() hosts agents in ASP.NET Core; UseOpenTelemetry() adds distributed tracing.",
    layer: "collaboration",
    prevVersion: "s18",
  },
  s20: {
    title: "Comprehensive Agent",
    subtitle: "All Mechanisms, One Pipeline",
    coreAddition: "Integrated MAF/MEAI harness",
    keyInsight: "The final agent wires tools, middleware, compaction, skills, and composition into one pipeline.",
    layer: "collaboration",
    prevVersion: "s19",
  },
};

export const LAYERS = [
  {
    id: "tools" as const,
    label: "Tools & Execution",
    color: "#3B82F6",
    versions: ["s01", "s02", "s03", "s04", "s05", "s06"],
  },
  {
    id: "planning" as const,
    label: "Planning & Control",
    color: "#10B981",
    versions: ["s07", "s08", "s09", "s11", "s12"],
  },
  {
    id: "memory" as const,
    label: "Memory Management",
    color: "#8B5CF6",
    versions: ["s10"],
  },
  {
    id: "concurrency" as const,
    label: "Concurrency & Scheduling",
    color: "#F59E0B",
    versions: ["s14"],
  },
  {
    id: "collaboration" as const,
    label: "Orchestration & Production",
    color: "#EF4444",
    versions: ["s13", "s15", "s16", "s17", "s18", "s19", "s20"],
  },
] as const;
