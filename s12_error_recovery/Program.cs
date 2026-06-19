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
var baseUrl = config["baseUrl"] ?? "https://api.deepseek.com/v1";
var apiKey = config["apiKey"] ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? throw new InvalidOperationException("No API key. Set apiKey in appsettings.json or DEEPSEEK_API_KEY env var.");
var modelId = config["modelId"] ?? "deepseek-chat";

var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(modelId).AsIChatClient();

// ── Build pipeline ──────────────────────────────────────
var client = chatClient.AsBuilder()
    .Use(inner => new RetryMiddleware(inner))
    .UseFunctionInvocation()
    .Build();

Console.WriteLine("[init] Pipeline: RetryMiddleware → FunctionInvocation → LLM\n");

// ── Demo ────────────────────────────────────────────────
var messages = new List<ChatMessage>
{
    new(ChatRole.User, "Explain exponential backoff in one sentence."),
};

Console.WriteLine("[chat] Sending request with retry protection...");
var response = await client.GetResponseAsync(messages);
Console.WriteLine($"[assistant] {response.Text}");

Console.WriteLine("\n[done] Error recovery demo complete.");

// ── Middleware implementation ────────────────────────────
sealed class RetryMiddleware(IChatClient inner) : DelegatingChatClient(inner)
{
    public int MaxRetries { get; set; } = 5;
    public int BaseDelayMs { get; set; } = 500;
    public int MaxTokens { get; set; } = 4096;

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var response = await base.GetResponseAsync(messages, options, ct);

                if (response.FinishReason == ChatFinishReason.Length && MaxTokens < 32768)
                {
                    MaxTokens = Math.Min(MaxTokens * 4, 32768);
                    Console.WriteLine($"[ESCALATE] max_tokens → {MaxTokens}");
                    options = options?.Clone() ?? new ChatOptions();
                    options.MaxOutputTokens = MaxTokens;
                    response = await base.GetResponseAsync(messages, options, ct);
                }

                return response;
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                var delay = BaseDelayMs * Math.Pow(2, attempt) + Random.Shared.Next(0, 250);
                Console.WriteLine($"[RETRY] Attempt {attempt + 1} after {delay}ms: {ex.Message}");
                await Task.Delay((int)delay, ct);
            }
            catch (Exception ex) when (ex.Message.Contains("prompt_too_long") || ex.Message.Contains("context_length_exceeded"))
            {
                Console.WriteLine("[RECOVERY] prompt_too_long — dropping oldest messages and retrying");
                var msgList = messages.ToList();
                if (msgList.Count > 3)
                {
                    var trimmed = msgList.Take(1).Concat(msgList.TakeLast(2)).ToList();
                    return await base.GetResponseAsync(trimmed, options, ct);
                }
                throw;
            }
        }
        throw new Exception("Max retries exceeded for transient errors");
    }

    static bool IsTransient(Exception ex) =>
        ex.Message.Contains("429") || ex.Message.Contains("529") || ex.Message.Contains("503");
}
