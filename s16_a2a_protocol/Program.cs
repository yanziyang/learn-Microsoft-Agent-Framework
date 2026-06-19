using System.ClientModel;
using A2A;
using A2A.AspNetCore;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.A2A;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using OpenAI;

// The A2A hosting APIs are experimental — suppress the diagnostic.
#pragma warning disable MAFEXP001

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ── Config ──
var apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? "sk-placeholder";
var baseUrl = Environment.GetEnvironmentVariable("LLM_BASE_URL") ?? "https://api.deepseek.com/v1";
var modelId = Environment.GetEnvironmentVariable("LLM_MODEL") ?? "deepseek-chat";

// ── Build the host (server side: expose an agent via A2A) ──
var builder = WebApplication.CreateBuilder();

IChatClient chatClient = new OpenAIClient(new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(modelId).AsIChatClient();

// Register the agent with the hosting container, then attach an A2A server.
// AddA2AServer bridges the agent so it can receive A2A JSON-RPC messages.
builder.AddAIAgent("weather-agent",
    instructions: "You are a weather assistant. Answer questions about weather concisely.",
    chatClient: chatClient);
builder.AddA2AServer("weather-agent");

// Use a fixed port so the client knows where to connect.
builder.WebHost.UseUrls("http://localhost:5161");

var app = builder.Build();

// Map A2A HTTP+JSON endpoints at /a2a/weather.
// This exposes:  POST /a2a/weather  (message/send, message/stream)
//                GET  /a2a/weather/card  (agent card)
app.MapA2AHttpJson("weather-agent", "/a2a/weather");

await app.StartAsync();
Console.WriteLine("[host] A2A server listening at http://localhost:5161/a2a/weather\n");

// ── Agent Card (published metadata) ──
// The server publishes an AgentCard at GET /a2a/weather/card.
// Callers use it to discover the agent's name, capabilities, and skills.
var card = new AgentCard
{
    Name = "WeatherAgent",
    Description = "Provides weather information for any city",
    Version = "1.0",
    Capabilities = new A2A.AgentCapabilities { Streaming = true },
    Skills =
    [
        new A2A.AgentSkill
        {
            Id = "weather-lookup",
            Name = "weather-lookup",
            Description = "Get current weather for a city",
            Tags = ["weather"],
        },
    ],
};
Console.WriteLine("═══ A2A Agent Card ═══");
Console.WriteLine($"  Name       : {card.Name}");
Console.WriteLine($"  Description: {card.Description}");
Console.WriteLine($"  Version    : {card.Version}");
Console.WriteLine($"  Streaming  : {card.Capabilities?.Streaming}");
Console.WriteLine($"  Skills     : {string.Join(", ", card.Skills.Select(s => $"{s.Name}({s.Description})"))}\n");

// ── Client side: call the hosted agent through A2A ──
// A2AClient implements IA2AClient. AsAIAgent() wraps it as an A2AAgent
// — a full AIAgent that can be used with RunAsync/RunStreamingAsync,
// composed as a tool, or placed in a workflow.
using var http = new HttpClient();
IA2AClient a2aClient = new A2AClient(
    new Uri("http://localhost:5161/a2a/weather"),
    http);

AIAgent remoteAgent = a2aClient.AsAIAgent(
    name: "RemoteWeatherAgent",
    description: "Calls the weather agent via the A2A protocol");

Console.WriteLine("═══ A2A Client → Remote Agent ═══");
Console.WriteLine($"Calling {card.Name} via A2A protocol...\n");

try
{
    AgentSession session = await remoteAgent.CreateSessionAsync();
    var response = await remoteAgent.RunAsync("What is the weather in London?", session);
    Console.WriteLine($"<<< Remote Agent: {response.Text}\n");
}
catch (Exception ex)
{
    Console.WriteLine($"[note] Live A2A call requires a valid LLM key: {ex.GetType().Name}: {ex.Message}\n");
}

// ── A2A in a multi-agent composition ──
// Because A2AAgent IS-A AIAgent, it can be used as a tool for another agent
// (Agent-as-Tool, s08) or placed in a workflow (s15).
Console.WriteLine("═══ A2A Composition ═══");
Console.WriteLine("  A2AAgent IS-A AIAgent → can be used as:");
Console.WriteLine("    • A tool: remoteAgent.AsAIFunction() for delegation");
Console.WriteLine("    • A workflow node: AgentWorkflowBuilder.BuildSequential([localAgent, remoteAgent])");
Console.WriteLine("    • A sub-agent in any MAF orchestration");

// ── Protocol anatomy ──
Console.WriteLine("\n═══ A2A Protocol Anatomy ═══");
Console.WriteLine(@"
  Client                          Server
    │                               │
    │── GET /a2a/weather/card ────→│  (discover AgentCard)
    │←────── AgentCard JSON ───────│
    │                               │
    │── POST /a2a/weather ────────→│  (JSON-RPC: message/send)
    │   {jsonrpc:'2.0', method,     │
    │    params:{message:{...}}}    │
    │                               │  → agent.RunAsync(...)
    │←──── JSON-RPC result ────────│
    │   {result:{kind:'message',    │
    │    parts:[{text:...}]}}        │
    │                               │
  Streaming: message/stream → SSE: task-status-update, task-artifact-update
");

await app.StopAsync();
Console.WriteLine("[host] A2A server stopped.");
