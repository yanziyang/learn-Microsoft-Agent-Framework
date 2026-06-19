using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

using MAI = Microsoft.Extensions.AI;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ── Config ──
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var baseUrl = config["baseUrl"] ?? "https://api.deepseek.com/v1";
var apiKey = config["apiKey"] ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
    ?? throw new InvalidOperationException("Set apiKey in appsettings.json or DEEPSEEK_API_KEY env var.");
var modelId = config["modelId"] ?? "deepseek-chat";

IChatClient chatClient = new ChatClient(modelId, new System.ClientModel.ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .AsIChatClient();

// ── Create agents with different roles ──
var analyst = chatClient.AsAIAgent(
    instructions: "You analyze data and identify trends. Be concise, use bullet points.",
    name: "Analyst");

var reporter = chatClient.AsAIAgent(
    instructions: "You write executive summaries based on analysis. Be concise, 2-3 sentences max.",
    name: "Reporter");

// ── Sequential workflow: analyst output feeds reporter ──
Console.WriteLine("═══ Sequential Workflow: Analyst → Reporter ═══\n");
var sequential = AgentWorkflowBuilder.BuildSequential("analysis-pipeline", [analyst, reporter]);

await using var run1 = await InProcessExecution.RunStreamingAsync(sequential,
    new MAI.ChatMessage(MAI.ChatRole.User, "Analyze the trend: sales up 20% Q1, 15% Q2, 10% Q3"));
await run1.TrySendMessageAsync(new TurnToken(emitEvents: true));

await foreach (var evt in run1.WatchStreamAsync())
{
    switch (evt)
    {
        case AgentResponseUpdateEvent update:
            Console.Write(update.Data);
            break;
        case ExecutorCompletedEvent completed:
            Console.WriteLine($"\n  [{completed.ExecutorId} completed]\n");
            break;
    }
}

// ── Concurrent workflow: agents run in parallel ──
Console.WriteLine("═══ Concurrent Workflow: Parallel Agents ═══\n");
var parallel = AgentWorkflowBuilder.BuildConcurrent("parallel-review", [analyst, reporter]);

await using var run2 = await InProcessExecution.RunStreamingAsync(parallel,
    new MAI.ChatMessage(MAI.ChatRole.User, "Review: the team shipped 3 features this sprint with 2 bugs."));
await run2.TrySendMessageAsync(new TurnToken(emitEvents: true));

await foreach (var evt in run2.WatchStreamAsync())
{
    switch (evt)
    {
        case AgentResponseUpdateEvent update:
            Console.Write($"[{update.ExecutorId}] {update.Data}");
            break;
        case ExecutorCompletedEvent completed:
            Console.WriteLine($"\n  [{completed.ExecutorId} completed]");
            break;
        case WorkflowErrorEvent error:
            Console.Error.WriteLine($"  ERROR: {error.Exception?.Message}");
            break;
    }
}

Console.WriteLine("\nDone.");
