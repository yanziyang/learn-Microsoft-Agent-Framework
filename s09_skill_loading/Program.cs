using System.ClientModel;
using System.Text.RegularExpressions;
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
var baseUrl = config["baseUrl"] ?? "https://api.openai.com/v1";
var apiKey = config["apiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new Exception("No API key. Set OPENAI_API_KEY or configure appsettings.json");
var modelId = config["modelId"] ?? "gpt-4o-mini";

var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(modelId).AsIChatClient();

// ── Skill Registry ──
var skillsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "skills"));
var skillEntries = new List<SkillEntry>();

if (Directory.Exists(skillsDir))
{
    foreach (var dir in Directory.GetDirectories(skillsDir))
    {
        var skillFile = Path.Combine(dir, "SKILL.md");
        if (!File.Exists(skillFile)) continue;

        var lines = File.ReadAllLines(skillFile);
        var (name, description) = ParseFrontmatter(lines);
        if (name is not null)
            skillEntries.Add(new SkillEntry(name, description ?? "", skillFile));
    }
}

Console.WriteLine($"Loaded {skillEntries.Count} skills from {skillsDir}");
foreach (var s in skillEntries)
    Console.WriteLine($"  - {s.Name}: {s.Description[..Math.Min(60, s.Description.Length)]}...");

// ── Load Skill Tool ──
var load_skill = AIFunctionFactory.Create(
    (string name) =>
    {
        var entry = skillEntries.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return $"Skill '{name}' not found. Available: {string.Join(", ", skillEntries.Select(s => s.Name))}";
        return File.ReadAllText(entry.FilePath);
    },
    name: "load_skill",
    description: "Load the full content of a skill by name. Use when you need detailed instructions for a task.");

var tools = new List<AITool> { load_skill };

// ── Agent ──
var catalog = string.Join("\n", skillEntries.Select(s => $"- {s.Name}: {s.Description}"));
var agent = new ChatClientAgent(chatClient,
    instructions: $"You are a helpful assistant with access to skills.\n\nAvailable skills:\n{catalog}\n\n" +
                  "When a user's request matches a skill, call load_skill to get full instructions before responding.",
    name: "skill-agent",
    description: "An agent that loads skills on demand",
    tools: tools);

// ── Run ──
Console.WriteLine("\ns09: Skill Loading — catalog in prompt, full content on demand\n");
var response = await agent.RunAsync("I want to build an MCP server that provides a weather lookup tool. Guide me through it.");
Console.WriteLine(response.Text);

// ── Helpers ──
static (string? name, string? description) ParseFrontmatter(string[] lines)
{
    if (lines.Length < 1 || lines[0].Trim() != "---") return (null, null);
    var yaml = new System.Text.StringBuilder();
    for (var i = 1; i < lines.Length; i++)
    {
        if (lines[i].Trim() == "---") break;
        yaml.AppendLine(lines[i]);
    }
    var nameMatch = Regex.Match(yaml.ToString(), @"^name:\s*(.+)$", RegexOptions.Multiline);
    var descMatch = Regex.Match(yaml.ToString(), @"^description:\s*(.+)$", RegexOptions.Multiline);
    return (nameMatch.Groups[1].Value.Trim(), descMatch.Success ? descMatch.Groups[1].Value.Trim() : null);
}

record SkillEntry(string Name, string Description, string FilePath);
