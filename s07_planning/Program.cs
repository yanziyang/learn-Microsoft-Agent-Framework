using System.ClientModel;
using System.ComponentModel;
using System.Text.Json;
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

var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(modelId).AsIChatClient();

// ── Todo State ──
var todoItems = new List<TodoItem>();

// ── Todo Tool ──
[Description("Update the todo list. Provide a JSON array of items with content and status (pending, in_progress, completed).")]
string todo_write(
    [Description("JSON array of objects: [{\"content\": \"...\", \"status\": \"pending|in_progress|completed\"}]")] string items)
{
    var parsed = JsonSerializer.Deserialize<List<TodoItem>>(items, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    if (parsed is null || parsed.Count == 0) return "No items provided.";

    // Merge by content: update existing, add new
    foreach (var item in parsed)
    {
        var existing = todoItems.FirstOrDefault(t => t.Content == item.Content);
        if (existing is not null)
            existing.Status = item.Status;
        else
            todoItems.Add(item);
    }
    return $"Todo list updated ({todoItems.Count} items). Current state:\n" +
           string.Join("\n", todoItems.Select(t => $"  [{t.Status}] {t.Content}"));
}

var tools = new List<AITool> { AIFunctionFactory.Create(todo_write) };

// ── Agent ──
var agent = new ChatClientAgent(
    new FunctionInvokingChatClient(chatClient),
    instructions: "You are a planning agent. When given a task, break it down into steps using the todo_write tool. " +
                  "Mark items as in_progress when working on them, completed when done. Always plan before acting.",
    name: "planner",
    description: "An agent that plans tasks using a todo list",
    tools: tools);

// ── Run ──
Console.WriteLine("s07: Planning via TodoWrite — break tasks into tracked steps\n");
var response = await agent.RunAsync("Plan the steps to build a simple REST API with a health endpoint and a users CRUD endpoint. Use todo_write to track your plan.");
Console.WriteLine($"Agent: {response.Text}");

Console.WriteLine("\n── Final Todo State ──");
foreach (var item in todoItems)
    Console.WriteLine($"  [{item.Status}] {item.Content}");

// ── Model ──
record TodoItem
{
    public string Content { get; set; } = "";
    public string Status { get; set; } = "pending";
}
