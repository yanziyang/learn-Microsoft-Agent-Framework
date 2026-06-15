using System.ClientModel;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ── Config (s01) ──
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();
var baseUrl = config["baseUrl"] ?? "https://api.openai.com/v1";
var apiKey = config["apiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new Exception("No API key");
var modelId = config["modelId"] ?? "gpt-4o-mini";

// ── Base Client (s01: provider-agnostic IChatClient) ──
IChatClient baseClient = new OpenAIClient(new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(modelId).AsIChatClient();

// ── Middleware Pipeline (s02: layered middleware) ──
//   Request  → Audit → Retry → Reducer → FunctionInvocation → LLM
//   Response ← Audit ← Retry ← Reducer ← FunctionInvocation ← LLM
var client = baseClient.AsBuilder()
    .Use(inner => new AuditMiddleware(inner))
    .Use(inner => new RetryMiddleware(inner))
    .UseChatReducer(new MessageCountingChatReducer(50))
    .UseFunctionInvocation()
    .Build();

// ── Tools (s04: AIFunctionFactory) ──
[Description("Get weather for a city")]
static string GetWeather([Description("City name")] string city) =>
    city.ToLower() switch
    {
        "london" => "London: 15°C, cloudy",
        "tokyo" => "Tokyo: 28°C, sunny",
        _ => $"{city}: 22°C, partly cloudy"
    };

[Description("Run a shell command")]
static string RunBash([Description("Command to execute")] string command) =>
    $"Executed: {command}";

var tools = new List<AITool>
{
    AIFunctionFactory.Create(GetWeather),
    new ApprovalRequiredAIFunction(AIFunctionFactory.Create(RunBash)),  // s05: approval gate
};

// ── TodoWrite Tool (s07: planning state) ──
var todoState = new List<(string content, string status)>();
[Description("Add a todo item")]
static string TodoWrite(
    [Description("List of todos as content|status lines")] string items,
    List<(string, string)> state)  // captured via closure below
{
    state.Clear();
    foreach (var line in items.Split('\n', StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = line.Split('|', 2);
        state.Add((parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : "pending"));
    }
    return $"Updated: {state.Count} items";
}

// We register a closure-captured version
var todoTool = AIFunctionFactory.Create(
    (string items) => TodoWrite(items, todoState),
    name: "todo_write",
    description: "Manage a todo list. Format: one 'content|status' per line.");
tools.Add(todoTool);

// ── Skill Loading (s09: on-demand from skills/) ──
var skillsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "skills"));
var skillCatalog = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
if (Directory.Exists(skillsDir))
{
    foreach (var dir in Directory.GetDirectories(skillsDir))
    {
        var skillFile = Path.Combine(dir, "SKILL.md");
        if (File.Exists(skillFile))
        {
            var name = Path.GetFileName(dir);
            skillCatalog[name] = skillFile;
        }
    }
}

var loadSkill = AIFunctionFactory.Create(
    (string name) =>
    {
        if (skillCatalog.TryGetValue(name, out var path))
            return File.ReadAllText(path);
        return $"Skill '{name}' not found. Available: {string.Join(", ", skillCatalog.Keys)}";
    },
    name: "load_skill",
    description: "Load a skill by name for detailed instructions.");
tools.Add(loadSkill);

// ── Agent-as-Tool (s08: specialist delegation) ──
AIAgent researcher = client.AsAIAgent(
    instructions: "You research topics concisely. Return key facts only.",
    name: "Researcher",
    description: "Researches topics and returns facts");
AIAgent writer = client.AsAIAgent(
    instructions: "You write clear summaries from research notes.",
    name: "Writer",
    description: "Writes summaries from notes");
tools.Add(researcher.AsAIFunction());
tools.Add(writer.AsAIFunction());

// ── Main Agent (s03: ChatClientAgent with all tools) ──
var agent = new ChatClientAgent(client,
    instructions: "You are a comprehensive coding assistant. Use tools as needed. " +
                  "Delegate research to Researcher, writing to Writer. " +
                  "Use todo_write to plan complex tasks. Use load_skill for domain guidance.",
    name: "ComprehensiveAgent",
    description: "Capstone agent with all MAF/MEAI features",
    tools: tools);

// ── Dynamic System Prompt (s11: sections + caching) ──
var sections = new Dictionary<string, string>
{
    ["identity"] = "You are a comprehensive agent with all mechanisms enabled.",
    ["tools"] = $"Available: {string.Join(", ", tools.Select(t => t.Name))}",
    ["workspace"] = $"Working directory: {Directory.GetCurrentDirectory()}",
};
string? cachedPrompt = null;
string GetPrompt()
{
    if (cachedPrompt is not null) return cachedPrompt;
    cachedPrompt = string.Join("\n\n", sections.Values);
    if (todoState.Count > 0)
        cachedPrompt += "\n\nCurrent todos:\n" + string.Join("\n", todoState.Select(t => $"- [{t.status}] {t.content}"));
    return cachedPrompt;
}

// ── Interactive REPL ──
Console.WriteLine("═══ Comprehensive Agent (MAF/MEAI) ═══");
Console.WriteLine("Features: middleware pipeline, tools, approval, audit, todo, skills, agent-as-tool, dynamic prompt, retry");
Console.WriteLine("Type 'q' to quit.\n");

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();
    if (input is null or "q" or "quit") break;

    var prompt = GetPrompt();
    Console.WriteLine($"[system] Prompt: {prompt.Length} chars, Todos: {todoState.Count}");

    try
    {
        var response = await agent.RunAsync(input);
        Console.WriteLine(response.Text);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[error] {ex.Message}");
    }
}

// ── Retry Middleware (s12: exponential backoff + token escalation) ──
sealed class RetryMiddleware(IChatClient inner) : DelegatingChatClient(inner)
{
    public int MaxRetries { get; set; } = 3;
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
                    return await base.GetResponseAsync(messages, options, ct);
                }
                return response;
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                var delay = BaseDelayMs * Math.Pow(2, attempt) + Random.Shared.Next(0, 250);
                Console.WriteLine($"[RETRY] Attempt {attempt + 1} after {delay}ms: {ex.Message}");
                await Task.Delay((int)delay, ct);
            }
        }
        throw new Exception("Max retries exceeded");
    }

    static bool IsTransient(Exception ex) =>
        ex.Message.Contains("429") || ex.Message.Contains("529") || ex.Message.Contains("503");
}

// ── Audit Middleware (s06: logging hooks) ──
sealed class AuditMiddleware(IChatClient inner) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
    {
        var lastUser = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "(no message)";
        Console.WriteLine($"[AUDIT] → {lastUser[..Math.Min(60, lastUser.Length)]}...");
        var sw = Stopwatch.StartNew();
        var response = await base.GetResponseAsync(messages, options, ct);
        sw.Stop();
        Console.WriteLine($"[AUDIT] ← {response.Text?.Length ?? 0} chars in {sw.ElapsedMilliseconds}ms");
        return response;
    }
}
