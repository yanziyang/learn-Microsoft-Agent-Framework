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

// ── IChatClient ──
IChatClient chatClient = new OpenAIClient(new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(modelId).AsIChatClient();

// ── Safe tool: executes immediately, no approval needed ──
[Description("Get the current weather for a city")]
static string GetWeather([Description("City name")] string city) =>
    city.ToLower() switch
    {
        "london" => "London: 15°C, cloudy",
        "tokyo" => "Tokyo: 28°C, sunny",
        _ => $"{city}: 22°C, partly cloudy"
    };

// ── Dangerous tool: wrapped with ApprovalRequiredAIFunction ──
// The framework intercepts calls to this tool and emits ToolApprovalRequestContent
// instead of executing. The caller must approve/deny via CreateResponse().
[Description("Delete a file from the filesystem")]
static string DeleteFile([Description("Path to the file to delete")] string path) =>
    $"File '{path}' has been deleted successfully.";

// ── Agent with both safe and gated tools ──
// AsAIAgent registers tools so the model can call them.
// ApprovalRequiredAIFunction wraps DeleteFile so it pauses for human approval.
AIAgent agent = chatClient.AsAIAgent(
    instructions: "You are a file management assistant. You can check weather and delete files when asked.",
    name: "file-manager",
    tools:
    [
        AIFunctionFactory.Create(GetWeather),                              // safe — no approval
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(DeleteFile)) // gated — requires approval
    ]);

// ── Approval loop using ToolApprovalRequestContent ──
Console.WriteLine("s05: Permission — tool approval with ApprovalRequiredAIFunction");
Console.WriteLine("Safe tools (GetWeather) execute immediately; dangerous tools (DeleteFile) require approval.\n");

var query = "What's the weather in Tokyo? Also delete the file 'temp.txt'.";
Console.WriteLine($">>> User: {query}\n");

// Create a session and run the first turn
AgentSession session = await agent.CreateSessionAsync();
AgentResponse response = await agent.RunAsync(query, session);

// Check for approval requests in the response
List<ToolApprovalRequestContent> approvalRequests =
    response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToList();

// Loop until all approval requests are resolved
while (approvalRequests.Count > 0)
{
    // Ask the user to approve each pending function call
    List<ChatMessage> approvalResponses = approvalRequests.ConvertAll(req =>
    {
        var toolCall = (FunctionCallContent)req.ToolCall;
        var args = string.Join(", ", toolCall.Arguments?.Select(a => $"{a.Key}={a.Value}") ?? []);
        Console.Write($"[APPROVAL] Allow {toolCall.Name}({args})? (y/n): ");
        var approved = Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) ?? false;
        Console.WriteLine(approved ? "  → Approved" : "  → Denied");
        return new ChatMessage(ChatRole.User, [req.CreateResponse(approved)]);
    });

    // Feed approval/denial back to the agent for continued processing
    response = await agent.RunAsync(approvalResponses, session);
    approvalRequests = response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToList();
}

// Final response
Console.WriteLine($"\n<<< Agent: {response.Text}");

// ── Streaming variant (commented) ──
// For streaming, collect updates and check for approval requests:
//
// var updates = await agent.RunStreamingAsync(query, session).ToListAsync();
// approvalRequests = updates.SelectMany(u => u.Contents).OfType<ToolApprovalRequestContent>().ToList();
// while (approvalRequests.Count > 0) { ... same approval loop ... }
// Console.WriteLine($"\nAgent: {updates.ToAgentResponse()}");
