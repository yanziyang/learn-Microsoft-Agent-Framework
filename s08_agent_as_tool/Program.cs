using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ── Config ──
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();
var baseUrl = config["baseUrl"] ?? "https://api.openai.com/v1";
var apiKey = config["apiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new Exception("No API key. Set OPENAI_API_KEY or configure appsettings.json");
var modelId = config["modelId"] ?? "gpt-4o-mini";

var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(modelId).AsIChatClient();

// ── Specialist Agents ──
AIAgent researcher = chatClient.AsAIAgent(
    instructions: "You are a research specialist. Given a topic, provide key facts, recent developments, and important context. Be thorough but concise.",
    name: "researcher",
    description: "Researches topics and gathers information");

AIAgent writer = chatClient.AsAIAgent(
    instructions: "You are a writing specialist. Given research notes, compose a clear, engaging summary suitable for a general audience.",
    name: "writer",
    description: "Writes summaries from research notes");

// ── Convert agents to tools ──
var tools = new List<AITool>
{
    researcher.AsAIFunction(),
    writer.AsAIFunction(),
};

// ── Coordinator Agent ──
var coordinator = new ChatClientAgent(chatClient,
    instructions: "You are a coordinator that delegates to specialist agents. " +
                  "Use the researcher tool to gather information, then use the writer tool to compose a summary. " +
                  "Always use both tools in sequence: research first, then write.",
    name: "coordinator",
    description: "Coordinates research and writing tasks",
    tools: tools);

// ── Run ──
Console.WriteLine("s08: Agent as Tool — specialist agents composed under a coordinator\n");
var response = await coordinator.RunAsync("Research the current state of quantum computing in 2025 and write a short summary about it.");
Console.WriteLine(response.Text);
