using System.Collections.Concurrent;
using System.ClientModel;
using System.ComponentModel;
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
var baseUrl = config["baseUrl"] ?? "https://api.deepseek.com/v1";
var apiKey = config["apiKey"] ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? throw new InvalidOperationException("No API key. Set apiKey in appsettings.json or DEEPSEEK_API_KEY env var.");
var modelId = config["modelId"] ?? "deepseek-chat";

IChatClient chatClient = new OpenAIClient(new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(modelId).AsIChatClient();

// ── Background task store ──
// Shared state that tools close over. Tasks run via Task.Run; results are
// injected back into the conversation as <task_notification> user messages.
var backgroundTasks = new ConcurrentDictionary<string, Task<string>>();
var taskMetadata = new ConcurrentDictionary<string, (string Command, DateTime Started)>();

// ── Tools registered via AIFunctionFactory ──
[Description("Start a long-running background task. Returns immediately with a task ID.")]
static string StartBackgroundTask(
    [Description("Description of the task to run")] string command,
    ConcurrentDictionary<string, Task<string>> tasks,
    ConcurrentDictionary<string, (string, DateTime)> metadata)
{
    var id = $"bg_{Guid.NewGuid().ToString()[..8]}";
    metadata[id] = (command, DateTime.UtcNow);
    tasks[id] = Task.Run(async () =>
    {
        await Task.Delay(3000); // Simulate long-running work
        return $"Completed: {command}";
    });
    return $"Started background task {id}. Use check_task to poll status.";
}

[Description("Check the status of a background task by ID")]
static string CheckTask(
    [Description("The task ID to check")] string taskId,
    ConcurrentDictionary<string, Task<string>> tasks)
{
    if (!tasks.TryGetValue(taskId, out var task))
        return $"Task {taskId} not found";
    if (task.IsCompletedSuccessfully)
        return $"Task {taskId}: {task.Result}";
    if (task.IsFaulted)
        return $"Task {taskId}: FAILED - {task.Exception?.InnerException?.Message}";
    return $"Task {taskId}: still running";
}

[Description("List all background tasks with their current status")]
static string ListAllTasks(
    ConcurrentDictionary<string, Task<string>> tasks,
    ConcurrentDictionary<string, (string, DateTime)> metadata)
{
    if (tasks.IsEmpty) return "No background tasks.";
    return string.Join("\n", tasks.Select(kv =>
    {
        var (cmd, started) = metadata.GetValueOrDefault(kv.Key);
        var elapsed = DateTime.UtcNow - started;
        var status = kv.Value switch
        {
            { IsCompletedSuccessfully: true } => "done",
            { IsFaulted: true } => "failed",
            _ => $"running ({elapsed.TotalSeconds:F0}s)"
        };
        return $"  {kv.Key}: [{status}] {cmd}";
    }));
}

// Register tools with closure-captured state
var tools = new List<AITool>
{
    AIFunctionFactory.Create(
        (string command) => StartBackgroundTask(command, backgroundTasks, taskMetadata),
        name: "start_task",
        description: "Start a long-running background task. Returns a task ID immediately."),
    AIFunctionFactory.Create(
        (string taskId) => CheckTask(taskId, backgroundTasks),
        name: "check_task",
        description: "Check the status of a background task."),
    AIFunctionFactory.Create(
        () => ListAllTasks(backgroundTasks, taskMetadata),
        name: "list_tasks",
        description: "List all background tasks with status."),
};

// ── Agent with background-task tools ──
var agent = new ChatClientAgent(chatClient,
    instructions: "You are a helpful assistant that can start background tasks. " +
                  "When a task completes, you'll receive a <task_notification> with the result. " +
                  "Acknowledge task completions and summarize results for the user.",
    name: "background-agent",
    description: "Agent with background task execution",
    tools: tools);

// ── Multi-turn demo with notification injection ──
Console.WriteLine("s14: Background Tasks — async tool execution with notification injection\n");

AgentSession session = await agent.CreateSessionAsync();

// Turn 1: user asks to start background tasks
var query = "Start two background tasks: one to build the project and one to run tests.";
Console.WriteLine($">>> User: {query}");
var response = await agent.RunAsync(query, session);
Console.WriteLine($"<<< Agent: {response.Text}\n");

// Wait for tasks to complete, then inject notifications
Console.WriteLine("[waiting 4s for background tasks to complete...]\n");
await Task.Delay(4000);

// Collect completed task results and inject as <task_notification> messages
var completed = backgroundTasks
    .Where(kv => kv.Value.IsCompletedSuccessfully)
    .Select(kv => (Id: kv.Key, Result: kv.Value.Result))
    .ToList();

if (completed.Count > 0)
{
    var notification = string.Join("\n", completed.Select(t =>
        $"<task_notification id=\"{t.Id}\">{t.Result}</task_notification>"));
    Console.WriteLine($"[inject] {notification}\n");

    // Inject the notification as a user message — the agent sees completed results
    var notificationMessage = new ChatMessage(ChatRole.User,
        $"{notification}\n\nAll background tasks have completed. Please summarize the results.");
    Console.WriteLine(">>> [system notification injected]");
    response = await agent.RunAsync(notificationMessage, session);
    Console.WriteLine($"<<< Agent: {response.Text}\n");
}

// Show final task list
Console.WriteLine("═══ Final Task Status ═══");
Console.WriteLine(ListAllTasks(backgroundTasks, taskMetadata));

// ── MAF native Background Responses (reference) ──
// MAF also supports native background responses via the OpenAI Responses API:
//
//   AgentRunOptions options = new() { AllowBackgroundResponses = true };
//   AgentSession session = await agent.CreateSessionAsync();
//   AgentResponse response = await agent.RunAsync(prompt, session, options);
//   while (response.ContinuationToken is { } token)
//   {
//       await Task.Delay(TimeSpan.FromSeconds(2));
//       options.ContinuationToken = token;
//       response = await agent.RunAsync(session, options);
//   }
//
// This requires an OpenAI Responses-compatible provider (not chat-completion).
// The pattern above (AIFunctionFactory tools + <task_notification> injection)
// works with any OpenAI-compatible chat-completion provider.

Console.WriteLine("\nDone.");
