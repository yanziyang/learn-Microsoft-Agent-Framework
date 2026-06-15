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
var baseUrl = config["baseUrl"] ?? "https://api.openai.com/v1";
var apiKey = config["apiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new Exception("No API key. Set OPENAI_API_KEY or configure appsettings.json");
var modelId = config["modelId"] ?? "gpt-4o-mini";

// --- IChatClient ---
var rawClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(modelId).AsIChatClient();

// --- Tool definitions ---
[Description("Get the current weather for a location")]
static string GetWeather([Description("City name")] string city) =>
    city.ToLower() switch {
        "london" => "London: 15°C, cloudy",
        "tokyo" => "Tokyo: 28°C, sunny",
        _ => $"{city}: 22°C, partly cloudy"
    };

[Description("Run a shell command")]
static string RunCommand([Description("The command to execute")] string command)
{
    if (command.Contains("rm -rf", StringComparison.OrdinalIgnoreCase))
        return "BLOCKED: dangerous command refused";
    return $"Executed: {command}";
}

var tools = new List<AITool>
{
    AIFunctionFactory.Create(GetWeather),
    AIFunctionFactory.Create(RunCommand),
};

// --- Build pipeline: audit → function invocation → raw client ---
var auditClient = new AuditMiddleware(new FunctionInvokingChatClient(rawClient));

// --- Agent setup ---
var agent = new ChatClientAgent(auditClient,
    instructions: "You are a helpful assistant. Use tools when appropriate.",
    name: "assistant",
    description: "A helpful assistant",
    tools: tools);

// --- Run queries ---
var queries = new[]
{
    "What's the weather in London?",
    "Run the command 'ls -la'",
    "Run the command 'rm -rf /'",
};

foreach (var query in queries)
{
    Console.WriteLine($"\n>>> User: {query}");
    var response = await agent.RunAsync(query);
    Console.WriteLine($"<<< Agent: {response.Text}");
}

// --- Show audit log summary ---
Console.WriteLine("\n--- Audit Log ---");
foreach (var entry in auditClient.AuditLog)
    Console.WriteLine($"  {entry}");

// --- Audit middleware via DelegatingChatClient ---
sealed class AuditMiddleware(IChatClient inner) : DelegatingChatClient(inner)
{
    private readonly List<string> _auditLog = [];
    public IReadOnlyList<string> AuditLog => _auditLog;

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
    {
        var msgList = messages.ToList();
        Console.WriteLine($"[HOOK] Pre-call: {msgList.Count} messages, {options?.Tools?.Count ?? 0} tools registered");

        var response = await base.GetResponseAsync(messages, options, ct);

        foreach (var content in response.Messages.SelectMany(m => m.Contents))
        {
            if (content is FunctionCallContent fcc)
            {
                var args = fcc.Arguments is not null
                    ? string.Join(", ", fcc.Arguments.Select(kv => $"{kv.Key}={kv.Value}"))
                    : "none";
                var entry = $"Tool called: {fcc.Name}({args})";
                Console.WriteLine($"[HOOK] {entry}");
                _auditLog.Add(entry);
            }
            if (content is FunctionResultContent frc)
            {
                var resultText = frc.Result?.ToString() ?? "(null)";
                if (resultText.Length > 100) resultText = resultText[..100] + "...";
                var entry = $"Tool result: {resultText}";
                Console.WriteLine($"[HOOK] {entry}");
                _auditLog.Add(entry);
            }
        }
        return response;
    }
}
