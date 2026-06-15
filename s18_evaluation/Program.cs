using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using OpenAI;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ── LLM client ──
var apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "sk-placeholder";
var baseUrl = Environment.GetEnvironmentVariable("LLM_BASE_URL") ?? "https://api.deepseek.com/v1";
var modelId = Environment.GetEnvironmentVariable("LLM_MODEL") ?? "deepseek-chat";

IChatClient chatClient = new OpenAIClient(new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(modelId).AsIChatClient();

// ── Create evaluators ──
IEvaluator coherenceEval = new CoherenceEvaluator();
IEvaluator relevanceEval = new RelevanceEvaluator();
IEvaluator fluencyEval = new FluencyEvaluator();
IEvaluator compositeEval = new CompositeEvaluator(coherenceEval, relevanceEval, fluencyEval);

// ChatConfiguration wraps IChatClient for the evaluation pipeline
var evalConfig = new ChatConfiguration(chatClient);

// ── Queries to evaluate ──
var queries = new[]
{
    "What is machine learning?",
    "Explain the difference between TCP and UDP.",
    "How does garbage collection work in .NET?",
};

Console.WriteLine("═══ Agent Response Evaluation ═══\n");

foreach (var query in queries)
{
    Console.WriteLine($">>> {query}");

    var messages = new List<ChatMessage> { new(ChatRole.User, query) };
    ChatResponse response = await chatClient.GetResponseAsync(messages);
    Console.WriteLine($"<<< {Truncate(response.Text, 120)}");

    // Evaluate with composite evaluator (all three at once)
    EvaluationResult result = await compositeEval.EvaluateAsync(messages, response, evalConfig);

    var coherence = result.Get<NumericMetric>(CoherenceEvaluator.CoherenceMetricName);
    var relevance = result.Get<NumericMetric>(RelevanceEvaluator.RelevanceMetricName);
    var fluency = result.Get<NumericMetric>(FluencyEvaluator.FluencyMetricName);

    Console.WriteLine($"  Coherence : {FormatScore(coherence)}");
    Console.WriteLine($"  Relevance : {FormatScore(relevance)}");
    Console.WriteLine($"  Fluency   : {FormatScore(fluency)}");
    Console.WriteLine();
}

// ── Individual evaluator (string-based shorthand) ──
Console.WriteLine("═══ Single Evaluator (string shorthand) ═══");
var quickResult = await relevanceEval.EvaluateAsync(
    "What is C#?", "C# is a modern object-oriented programming language.", evalConfig);
var quickScore = quickResult.Get<NumericMetric>(RelevanceEvaluator.RelevanceMetricName);
Console.WriteLine($"  Relevance: {FormatScore(quickScore)}");

// ── Evaluate with context (groundedness) ──
Console.WriteLine("\n═══ Groundedness with Context ═══");
var groundednessEval = new GroundednessEvaluator();
var groundedResult = await groundednessEval.EvaluateAsync(
    "How does GC work in .NET?",
    ".NET uses a generational garbage collector with three generations.",
    evalConfig);
var groundedScore = groundedResult.Get<NumericMetric>(GroundednessEvaluator.GroundednessMetricName);
Console.WriteLine($"  Groundedness: {FormatScore(groundedScore)}");

Console.WriteLine("\n═══ Available Evaluators ═══");
Console.WriteLine("  Coherence, Relevance, Fluency, Groundedness, Completeness,");
Console.WriteLine("  Equivalence, IntentResolution, TaskAdherence, Retrieval, ToolCallAccuracy");

static string FormatScore(NumericMetric m) =>
    $"{m.Value:F1}/5 ({m.Interpretation?.Rating})";

static string Truncate(string s, int max) =>
    s.Length <= max ? s : s[..max] + "...";
