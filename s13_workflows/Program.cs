using Microsoft.Agents.AI.Workflows;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ── Function-based executor ──
Func<string, string> toUpper = s => s.ToUpperInvariant();
var upperExecutor = toUpper.BindAsExecutor("ToUpper");

// ── Custom executor class ──
var reverse = new ReverseExecutor();

// ── Build workflow graph ──
var workflow = new WorkflowBuilder(upperExecutor)
    .AddEdge(upperExecutor, reverse)
    .WithOutputFrom(reverse)
    .Build();

// ── Execute with streaming ──
Console.WriteLine("═══ Workflow: ToUpper → Reverse ═══");
await using var run = await InProcessExecution.RunStreamingAsync(workflow, "Hello, World!");
await foreach (var evt in run.WatchStreamAsync())
{
    switch (evt)
    {
        case ExecutorCompletedEvent completed:
            Console.WriteLine($"  {completed.ExecutorId}: {completed.Data}");
            break;
        case WorkflowErrorEvent error:
            Console.Error.WriteLine($"  ERROR: {error.Exception?.Message}");
            break;
    }
}

// ── Linear pipeline: three stages ──
Console.WriteLine("\n═══ Pipeline: Trim → Upper → Duplicate ═══");
Func<string, string> trim = s => s.Trim();
Func<string, string> duplicate = s => $"{s} | {s}";
var trimEx = trim.BindAsExecutor("Trim");
var upperEx = toUpper.BindAsExecutor("Upper2");
var dupEx = duplicate.BindAsExecutor("Duplicate");

var pipeline = new WorkflowBuilder(trimEx)
    .AddEdge(trimEx, upperEx)
    .AddEdge(upperEx, dupEx)
    .WithOutputFrom(dupEx)
    .Build();

await using var run2 = await InProcessExecution.RunStreamingAsync(pipeline, "  hello world  ");
await foreach (var evt in run2.WatchStreamAsync())
{
    if (evt is ExecutorCompletedEvent c)
        Console.WriteLine($"  {c.ExecutorId}: {c.Data}");
}

// ── Fan-out: one input to multiple executors ──
Console.WriteLine("\n═══ Fan-Out: ToUpper + ToLower ═══");
Func<string, string> toLower = s => s.ToLowerInvariant();
var lowerEx = toLower.BindAsExecutor("ToLower");

var fanOut = new WorkflowBuilder(upperExecutor)
    .AddEdge(upperExecutor, lowerEx)
    .WithOutputFrom(upperExecutor, lowerEx)
    .Build();

await using var run3 = await InProcessExecution.RunStreamingAsync(fanOut, "Hello");
await foreach (var evt in run3.WatchStreamAsync())
{
    if (evt is ExecutorCompletedEvent c)
        Console.WriteLine($"  {c.ExecutorId}: {c.Data}");
}

Console.WriteLine("\nDone.");

// ── Custom executor ──
sealed class ReverseExecutor() : Executor<string, string>("Reverse")
{
    public override ValueTask<string> HandleAsync(string message, IWorkflowContext context, CancellationToken ct = default)
        => ValueTask.FromResult(string.Concat(message.Reverse()));
}
