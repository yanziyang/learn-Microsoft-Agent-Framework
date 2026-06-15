using System.ClientModel;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var jso = new JsonSerializerOptions { WriteIndented = true };

// ── LLM client ──
var apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "sk-placeholder";
var baseUrl = Environment.GetEnvironmentVariable("LLM_BASE_URL") ?? "https://api.deepseek.com/v1";
var modelId = Environment.GetEnvironmentVariable("LLM_MODEL") ?? "deepseek-chat";

IChatClient chatClient = new OpenAIClient(new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(modelId).AsIChatClient();

// ── A2A Agent Card (public metadata about an agent) ──
var agentCard = new
{
    name = "WeatherAgent",
    description = "Provides weather information for any city",
    version = "1.0",
    url = "https://example.com/a2a/weather",
    capabilities = new { streaming = true, pushNotifications = false },
    skills = new[] { new { name = "weather查询", description = "Get current weather" } },
    defaultInputModes = new[] { "text" },
    defaultOutputModes = new[] { "text" },
};
Console.WriteLine("═══ A2A Agent Card ═══");
Console.WriteLine(JsonSerializer.Serialize(agentCard, jso));

// ── A2A Request (JSON-RPC envelope) ──
var contextId = Guid.NewGuid().ToString();
var a2aRequest = new
{
    jsonrpc = "2.0",
    id = Guid.NewGuid().ToString(),
    method = "message/send",
    @params = new
    {
        message = new
        {
            kind = "message",
            role = "user",
            parts = new[] { new { kind = "text", text = "What is the weather in London?" } },
            messageId = Guid.NewGuid().ToString(),
            contextId,
        }
    }
};
Console.WriteLine("\n═══ A2A Request ═══");
Console.WriteLine(JsonSerializer.Serialize(a2aRequest, jso));

// ── Local agent processes the A2A message ──
AIAgent agent = chatClient.AsAIAgent(
    instructions: "You are a weather agent. Respond with current weather information.",
    name: "WeatherAgent",
    description: "Provides weather information");

var userText = a2aRequest.@params.message.parts[0].text;
Console.WriteLine($"\n═══ Processing: {userText} ═══");
var response = await agent.RunAsync(userText);
Console.WriteLine($"Agent: {response.Text}");

// ── A2A Response (JSON-RPC result) ──
var a2aResponse = new
{
    jsonrpc = "2.0",
    id = a2aRequest.id,
    result = new
    {
        kind = "message",
        role = "agent",
        parts = new[] { new { kind = "text", text = response.Text } },
        messageId = Guid.NewGuid().ToString(),
        contextId,
    }
};
Console.WriteLine("\n═══ A2A Response ═══");
Console.WriteLine(JsonSerializer.Serialize(a2aResponse, jso));

// ── A2A Task lifecycle ──
Console.WriteLine("\n═══ Task Lifecycle ═══");
Console.WriteLine("  submitted → working → completed (or failed/canceled)");
Console.WriteLine("  Streaming uses message/stream → SSE: task-status-update, task-artifact-update");

// ── ASP.NET Core hosting (conceptual) ──
Console.WriteLine("\n═══ ASP.NET Core Hosting (conceptual) ═══");
Console.WriteLine("  // app.MapA2A(weatherAgent, \"/a2a/weather\", agentCard: new() { ... });");
Console.WriteLine("  // builder.Services.AddAIAgent(\"weather\", ...);");
