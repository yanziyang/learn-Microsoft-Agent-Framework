using System.ClientModel;
using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// --- Configuration ---
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();
var baseUrl = config["baseUrl"] ?? "https://api.deepseek.com/v1";
var apiKey = config["apiKey"] ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? throw new InvalidOperationException("No API key. Set apiKey in appsettings.json or DEEPSEEK_API_KEY env var.");
var modelId = config["modelId"] ?? "deepseek-chat";

// --- IChatClient with tool support ---
var client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(modelId).AsIChatClient();

// --- Tool definitions ---
[Description("Get the current weather for a location")]
static string GetWeather([Description("City name")] string city) =>
    city.ToLower() switch {
        "london" => "London: 15°C, cloudy",
        "tokyo" => "Tokyo: 28°C, sunny",
        _ => $"{city}: 22°C, partly cloudy"
    };

[Description("Get the current date and time")]
static string GetCurrentTime() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

[Description("Calculate a mathematical expression")]
static double Calculate(
    [Description("First operand")] double a,
    [Description("The operation: add, subtract, multiply, divide")] string operation,
    [Description("Second operand")] double b) =>
    operation.ToLower() switch {
        "add" => a + b, "subtract" => a - b, "multiply" => a * b, "divide" => a / b,
        _ => throw new ArgumentException($"Unknown operation: {operation}")
    };

// --- Register tools via AIFunctionFactory ---
var tools = new List<AITool>
{
    AIFunctionFactory.Create(GetWeather),
    AIFunctionFactory.Create(GetCurrentTime),
    AIFunctionFactory.Create(Calculate),
};

// --- Build pipeline: FunctionInvokingChatClient wraps the raw client ---
var chatClient = new FunctionInvokingChatClient(client);

// --- Agent setup ---
var agent = new ChatClientAgent(chatClient,
    instructions: "You are a helpful assistant. Use tools to answer questions about weather, time, and math.",
    name: "assistant",
    description: "A helpful assistant",
    tools: tools);

// --- Run queries that trigger tool use ---
var queries = new[]
{
    "What's the weather in Tokyo and London?",
    "What time is it now?",
    "Calculate 42 * 17 + 3",
};

foreach (var query in queries)
{
    Console.WriteLine($"\n>>> User: {query}");
    var response = await agent.RunAsync(query);
    Console.WriteLine($"<<< Agent: {response.Text}");
}
