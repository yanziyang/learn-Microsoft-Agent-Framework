using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ── Config ──
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var baseUrl = config["baseUrl"] ?? "https://api.openai.com/v1"; // Replace with your provider's endpoint
var apiKey = config["apiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new Exception("No API key found. Set apiKey in appsettings.json or OPENAI_API_KEY env var.");
var modelId = config["modelId"] ?? "gpt-4o-mini";

// ── Base IChatClient ──
IChatClient innerClient = new OpenAI.Chat.ChatClient(modelId, new System.ClientModel.ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .AsIChatClient();

// ── Build the pipeline ──
// Pipeline order (outermost → innermost):
//   Request  → Timing → Logging → OpenAI Client
//   Response ← Timing ← Logging ← OpenAI Client
//
// Each Use() wraps the inner client with a DelegatingChatClient subclass.
// Timing measures total round-trip including logging overhead.
var client = innerClient
    .AsBuilder()
    .Use(inner => new TimingChatClient(inner))
    .Use(inner => new LoggingChatClient(inner))
    .Build();

// ── Test: Non-streaming ──
Console.WriteLine("═══ Non-Streaming ═══");
var response = await client.GetResponseAsync("What is dependency injection? One sentence.");
Console.WriteLine($"  Answer: {response.Text}\n");

// ── Test: Streaming ──
Console.WriteLine("═══ Streaming ═══");
Console.Write("  Answer: ");
await foreach (var update in client.GetStreamingResponseAsync("Name two design patterns in C#."))
{
    Console.Write(update);
}
Console.WriteLine("\n");

// ── Pipeline composition notes ──
// The decorator pattern means each middleware wraps the one below it.
// To add OpenTelemetry, insert .UseOpenTelemetry() at any position:
//   .Use(inner => new TimingChatClient(inner))
//   .UseOpenTelemetry()    ← built-in MEAI extension
//   .Use(inner => new LoggingChatClient(inner))
// Middleware order matters: put cross-cutting concerns (logging, tracing)
// on the outside, business logic middleware closer to the client.

// ── Custom middleware: TimingChatClient ──
// DelegatingChatClient is the base class for middleware.
// Override GetResponseAsync / GetStreamingResponseAsync to wrap behavior.
// Call base.* to pass through to the next layer in the pipeline.
class TimingChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        sw.Stop();
        Console.WriteLine($"  [timing] Non-streaming took {sw.ElapsedMilliseconds}ms");
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            yield return update;
        }
        sw.Stop();
        Console.WriteLine($"\n  [timing] Streaming took {sw.ElapsedMilliseconds}ms");
    }
}

// ── Custom middleware: LoggingChatClient ──
// Logs the user's question before forwarding, and the response length after.
class LoggingChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var lastUserMsg = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "(no user message)";
        Console.WriteLine($"  [logging] Question: {lastUserMsg[..Math.Min(50, lastUserMsg.Length)]}...");
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        Console.WriteLine($"  [logging] Response length: {response.Text?.Length ?? 0} chars");
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var lastUserMsg = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "(no user message)";
        Console.WriteLine($"  [logging] Question: {lastUserMsg[..Math.Min(50, lastUserMsg.Length)]}...");
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            yield return update;
        }
    }
}
