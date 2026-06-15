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
var client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(modelId).AsIChatClient();

// --- Dangerous tool definition ---
[Description("Delete a file from the filesystem")]
static string DeleteFile([Description("Path to the file to delete")] string path) =>
    $"File '{path}' has been deleted successfully.";

// --- Wrap with approval requirement ---
var dangerousTool = new ApprovalRequiredAIFunction(AIFunctionFactory.Create(DeleteFile));

// --- Build pipeline with function invocation ---
var chatClient = new FunctionInvokingChatClient(client);

// --- Agent setup ---
var agent = new ChatClientAgent(chatClient,
    instructions: "You are a file management assistant. You can delete files when asked.",
    name: "file-manager",
    description: "A file management assistant",
    tools: [dangerousTool]);

// --- Interactive approval loop ---
Console.WriteLine("s05: Permission — tool approval with ApprovalRequiredAIFunction");
Console.WriteLine("The agent will ask for permission before deleting files.\n");

var query = "Please delete the file 'temp.txt' and then delete 'old_data.csv'";
Console.WriteLine($">>> User: {query}\n");

var messages = new List<ChatMessage> { new(ChatRole.User, query) };
var options = new ChatOptions { Tools = [dangerousTool] };

while (true)
{
    var response = await chatClient.GetResponseAsync(messages, options);
    messages.AddMessages(response);

    // Check for approval requests
    var approvalRequests = response.Messages
        .SelectMany(m => m.Contents)
        .OfType<FunctionCallContent>()
        .Where(fc => dangerousTool.Name.Equals(fc.Name, StringComparison.OrdinalIgnoreCase))
        .ToList();

    if (approvalRequests.Count == 0)
    {
        // No more approval requests — show final response
        Console.WriteLine($"<<< Agent: {response.Text}");
        break;
    }

    // Process each approval request
    foreach (var request in approvalRequests)
    {
        var filePath = request.Arguments?["path"]?.ToString() ?? "unknown";
        Console.Write($"[APPROVAL] Allow deletion of '{filePath}'? (y/n): ");
        var answer = Console.ReadLine()?.Trim().ToLower();

        if (answer is "y" or "yes")
        {
            // Execute the tool and return result
            var result = DeleteFile(filePath);
            Console.WriteLine($"[APPROVED] {result}");
            messages.Add(new ChatMessage(ChatRole.Tool, result));
        }
        else
        {
            // Reject the tool call
            var rejection = $"Tool call '{request.Name}' for '{filePath}' was denied by the user.";
            Console.WriteLine($"[REJECTED] {rejection}");
            messages.Add(new ChatMessage(ChatRole.Tool, rejection));
        }
    }
}
