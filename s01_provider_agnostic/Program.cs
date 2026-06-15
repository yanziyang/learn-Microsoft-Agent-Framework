using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ── Config ──
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var baseUrl = config["baseUrl"] ?? "https://api.openai.com/v1"; // Replace with your provider's endpoint
var apiKey = config["apiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new Exception("No API key found. Set apiKey in appsettings.json or OPENAI_API_KEY env var.");
var modelId = config["modelId"] ?? "gpt-4o-mini";

// ── Create IChatClient (provider-agnostic) ──
// IChatClient is the core abstraction of Microsoft.Extensions.AI.
// Any provider that implements it can be swapped in without changing consumer code.
IChatClient chatClient = new ChatClient(modelId, new System.ClientModel.ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .AsIChatClient();

// ── Non-streaming call ──
Console.WriteLine("═══ Non-Streaming ═══");
var response = await chatClient.GetResponseAsync("What is .NET? Answer in one sentence.");
Console.WriteLine(response.Text);

// ── Streaming call ──
Console.WriteLine("\n═══ Streaming ═══");
await foreach (var update in chatClient.GetStreamingResponseAsync("Name three benefits of C#."))
{
    Console.Write(update);
}
Console.WriteLine();

// ── Middleware pipeline ──
// ChatClientBuilder lets you wrap the base client with middleware layers.
// Each layer can inspect/modify requests and responses.
// The pipeline executes outside-in on requests, inside-out on responses.
//
//   Request  → [Logging] → [Timing] → [OpenAI Client]
//   Response ← [Logging] ← [Timing] ← [OpenAI Client]
var pipelineClient = chatClient
    .AsBuilder()
    .Use(async (messages, options, next, cancellationToken) =>
    {
        Console.Write("[middleware] → ");
        await next(messages, options, cancellationToken);
        Console.Write(" ← [middleware]");
    })
    .Build();

Console.WriteLine("\n═══ Through Pipeline ═══");
var pipedResponse = await pipelineClient.GetResponseAsync("Say hello in one word.");
Console.WriteLine($"\n{pipedResponse.Text}");

// ── Provider switch (conceptual) ──
// To switch providers, just change the IChatClient construction above.
// Examples:
//   Ollama:    new OllamaApiClient("http://localhost:11434", "llama3").AsIChatClient()
//   Anthropic: new AnthropicClient(apiKey).Messages.AsIChatClient()
//   Azure:     new AzureOpenAIClient(endpoint, credential).GetChatClient(model).AsIChatClient()
// All return IChatClient — your code downstream never changes.
