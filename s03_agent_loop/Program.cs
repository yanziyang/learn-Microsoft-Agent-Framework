using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Agents.AI;
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

// ── Create IChatClient → AIAgent ──
// ChatClientAgent wraps an IChatClient with agent capabilities:
//   - System instructions (persona, rules)
//   - Session-based conversation state
//   - Managed message history per session
IChatClient chatClient = new ChatClient(modelId, new System.ClientModel.ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .AsIChatClient();

AIAgent agent = chatClient.AsAIAgent(
    instructions: "You are a concise, helpful assistant. Answer in 1-2 sentences.",
    name: "TutorialAgent");

// ── Non-streaming run (single turn) ──
Console.WriteLine("═══ Single Turn ═══");
var result = await agent.RunAsync("What is the capital of France?");
Console.WriteLine(result);

// ── Multi-turn conversation with AgentSession ──
// AgentSession preserves conversation history across multiple RunAsync calls.
// The framework feeds the full history back to the model each turn.
Console.WriteLine("\n═══ Multi-Turn Conversation ═══");
AgentSession session = await agent.CreateSessionAsync();

var r1 = await agent.RunAsync("My name is Alice.", session);
Console.WriteLine($"Agent: {r1}");

var r2 = await agent.RunAsync("What is my name?", session);
Console.WriteLine($"Agent: {r2}");

// ── Streaming with a new session ──
// RunStreamingAsync returns IAsyncEnumerable<ChatResponseUpdate>.
// Each update is a token/chunk of the response.
Console.WriteLine("\n═══ Streaming ═══");
AgentSession streamSession = await agent.CreateSessionAsync();

Console.Write("Agent: ");
await foreach (var update in agent.RunStreamingAsync("Tell me a fun fact about C#.", streamSession))
{
    Console.Write(update);
}
Console.WriteLine();

Console.Write("Agent: ");
await foreach (var update in agent.RunStreamingAsync("Why is that interesting?", streamSession))
{
    Console.Write(update);
}
Console.WriteLine();

// ── REPL (interactive) ──
// Uncomment to run an interactive chat loop:
//
// Console.WriteLine("\n═══ Interactive (type 'q' to quit) ═══");
// var replSession = await agent.CreateSessionAsync();
// while (true)
// {
//     Console.Write("You: ");
//     var input = Console.ReadLine();
//     if (input is null or "q") break;
//     Console.Write("Agent: ");
//     await foreach (var update in agent.RunStreamingAsync(input, replSession))
//         Console.Write(update);
//     Console.WriteLine();
// }
