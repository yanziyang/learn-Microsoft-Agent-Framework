using System.ClientModel;
using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

#pragma warning disable MEAI001

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ── Config ──────────────────────────────────────────────
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();
var baseUrl = config["baseUrl"] ?? "https://api.deepseek.com/v1";
var apiKey = config["apiKey"] ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? throw new InvalidOperationException("No API key. Set apiKey in appsettings.json or DEEPSEEK_API_KEY env var.");
var modelId = config["modelId"] ?? "deepseek-chat";

var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(modelId).AsIChatClient();

// ── MEAI Chat Reducers ──────────────────────────────────
var countingReducer = new MessageCountingChatReducer(50);
Console.WriteLine("[init] MessageCountingChatReducer: keeps last 50 messages");

var summarizingReducer = new SummarizingChatReducer(chatClient, 20, 40);
Console.WriteLine("[init] SummarizingChatReducer: summarize when >40 msgs, target 20");

// ── Build pipeline ──────────────────────────────────────
var client = chatClient.AsBuilder()
    .UseChatReducer(countingReducer)
    .UseFunctionInvocation()
    .Build();

Console.WriteLine("[init] Pipeline: Reducer(counting) → FunctionInvocation → LLM");

// ── Demo ────────────────────────────────────────────────
[Description("Gets the current time in UTC")]
static string GetCurrentTime() => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

var tools = new List<AITool> { AIFunctionFactory.Create(GetCurrentTime) };
var options = new ChatOptions { Tools = tools };

Console.WriteLine("\n[chat] Sending 5 messages to demonstrate context window tracking...");
var messages = new List<ChatMessage>();

for (int i = 1; i <= 5; i++)
{
    messages.Add(new ChatMessage(ChatRole.User, $"Message {i}: What time is it?"));
    var response = await client.GetResponseAsync(messages, options);
    messages.Add(new ChatMessage(ChatRole.Assistant, response.Text));
    Console.WriteLine($"  [{i}] {response.Text?.TrimEnd()}");
}

Console.WriteLine($"\n[info] Conversation has {messages.Count} messages");

// ── MAF Compaction Strategies ───────────────────────────
// Microsoft.Agents.AI offers PipelineCompactionStrategy with:
//   - ToolResultCompactionStrategy   — collapse verbose tool results
//   - SummarizationCompactionStrategy — LLM summarize older spans
//   - SlidingWindowCompactionStrategy — keep last N turns
//   - TruncationCompactionStrategy   — emergency drop oldest

Console.WriteLine("\n[done] Context compaction demo complete.");
