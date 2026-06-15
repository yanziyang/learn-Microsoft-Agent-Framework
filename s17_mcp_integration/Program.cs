using System.ClientModel;
using System.ComponentModel;
using System.IO.Pipelines;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OpenAI;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ── LLM client ──
var apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? "sk-placeholder";
var baseUrl = Environment.GetEnvironmentVariable("LLM_BASE_URL") ?? "https://api.deepseek.com/v1";
var modelId = Environment.GetEnvironmentVariable("LLM_MODEL") ?? "deepseek-chat";

IChatClient chatClient = new OpenAIClient(new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(modelId).AsIChatClient();

// ── Built-in tools ──
[Description("Get the weather for a location")]
static string GetWeather([Description("City name")] string city) =>
    city.ToLower() switch
    {
        "london" => "London: 15°C, cloudy",
        "tokyo" => "Tokyo: 28°C, sunny",
        _ => $"{city}: 22°C, partly cloudy"
    };

var builtInTools = new List<AITool> { AIFunctionFactory.Create(GetWeather) };

// ── In-memory MCP server with tools ──
Pipe clientToServer = new(), serverToClient = new();

var searchTool = McpServerTool.Create(
    (string query) => $"Search results for '{query}': Found 3 docs about .NET and C#.",
    new() { Name = "search_docs", Description = "Search documentation" });

var calcTool = McpServerTool.Create(
    (double a, double b) => a + b,
    new() { Name = "add_numbers", Description = "Add two numbers" });

var serverOptions = new McpServerOptions { ToolCollection = [searchTool, calcTool] };

await using var server = McpServer.Create(
    new StreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream()),
    serverOptions);
_ = server.RunAsync();

// ── MCP client connects and discovers tools ──
await using var mcpClient = await McpClient.CreateAsync(
    new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()));

var mcpTools = await mcpClient.ListToolsAsync();
Console.WriteLine("═══ MCP Server Tools ═══");
foreach (var tool in mcpTools)
    Console.WriteLine($"  {tool.Name}: {tool.Description}");

// ── Merge built-in + MCP tools (McpClientTool IS-A AIFunction) ──
var allTools = new List<AITool>(builtInTools);
foreach (var t in mcpTools) allTools.Add(t);
Console.WriteLine($"\nTool pool: {builtInTools.Count} built-in + {mcpTools.Count} MCP = {allTools.Count} total");

// ── Agent with unified tool pool ──
Console.WriteLine("\n═══ Agent with MCP Tools ═══");
AIAgent agent = chatClient.AsAIAgent(
    instructions: "You are a helpful assistant. Use search_docs for documentation queries and GetWeather for weather.",
    name: "McpAgent",
    description: "Agent with MCP tools",
    tools: allTools);

var response = await agent.RunAsync("Search for .NET documentation and tell me the weather in Tokyo.");
Console.WriteLine(response.Text);

Console.WriteLine("\n═══ MCP Pattern Summary ═══");
Console.WriteLine(@"
  StdioClientTransport  → connects to external MCP server via stdio
  McpClient.ListToolsAsync() → discovers available tools
  McpClientTool IS-A AIFunction → no conversion needed
  Tools merge into one pool → agent sees built-in + MCP tools uniformly
");
