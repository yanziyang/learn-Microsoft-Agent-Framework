using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ── Config ──────────────────────────────────────────────
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();
var baseUrl = config["baseUrl"] ?? "https://api.openai.com/v1";
var apiKey = config["apiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new Exception("No API key");
var modelId = config["modelId"] ?? "gpt-4o-mini";

var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(modelId).AsIChatClient();

// ── Prompt sections ─────────────────────────────────────
var sections = new Dictionary<string, string>
{
    ["identity"] = "You are a helpful coding assistant.",
    ["tools"] = "You have access to: bash, read_file, write_file, edit_file, glob.",
    ["workspace"] = $"Working directory: {Directory.GetCurrentDirectory()}",
    ["environment"] = $"OS: {Environment.OSVersion}, .NET: {Environment.Version}",
};

// ── Deterministic caching ───────────────────────────────
string? cachedContext = null;
string? cachedPrompt = null;

string BuildPrompt()
{
    var ctx = $"{Environment.OSVersion}|{Directory.GetCurrentDirectory()}";
    if (ctx == cachedContext && cachedPrompt is not null)
    {
        Console.WriteLine("  [cache hit] system prompt unchanged");
        return cachedPrompt;
    }
    cachedContext = ctx;
    cachedPrompt = string.Join("\n\n", sections.Values);
    Console.WriteLine($"  [assembled] sections: {string.Join(", ", sections.Keys)}");
    return cachedPrompt;
}

// ── Use dynamic prompt ──────────────────────────────────
var systemPrompt = BuildPrompt();
Console.WriteLine("── System Prompt ──");
Console.WriteLine(systemPrompt);
Console.WriteLine("───────────────────\n");

var messages = new List<ChatMessage>
{
    new(ChatRole.System, systemPrompt),
    new(ChatRole.User, "What tools do you have?"),
};

Console.WriteLine("[chat] Asking about available tools...");
var response = await chatClient.GetResponseAsync(messages);
Console.WriteLine($"[assistant] {response.Text}");

Console.WriteLine();
var prompt2 = BuildPrompt();
Console.WriteLine($"[info] Prompt length: {prompt2.Length} chars");

Console.WriteLine("\n[done] System prompt assembly demo complete.");
