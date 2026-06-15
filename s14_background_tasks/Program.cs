using System.Collections.Concurrent;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ── Background task store ──
var backgroundTasks = new ConcurrentDictionary<string, Task<string>>();
var taskMetadata = new ConcurrentDictionary<string, (string Command, DateTime Started)>();

// ── Tool: start background work ──
string RunInBackground(string command)
{
    var id = $"bg_{Guid.NewGuid().ToString()[..8]}";
    taskMetadata[id] = (command, DateTime.UtcNow);
    backgroundTasks[id] = Task.Run(async () =>
    {
        await Task.Delay(3000); // Simulate long-running work
        return $"Completed: {command}";
    });
    return $"Started background task {id}";
}

// ── Tool: check task status ──
string CheckTask(string taskId)
{
    if (!backgroundTasks.TryGetValue(taskId, out var task))
        return $"Task {taskId} not found";
    if (task.IsCompletedSuccessfully)
        return $"Task {taskId}: {task.Result}";
    if (task.IsFaulted)
        return $"Task {taskId}: FAILED - {task.Exception?.InnerException?.Message}";
    return $"Task {taskId}: still running";
}

// ── Tool: list all tasks ──
string ListTasks()
{
    if (backgroundTasks.IsEmpty) return "No background tasks.";
    return string.Join("\n", backgroundTasks.Select(kv =>
    {
        var (cmd, started) = taskMetadata[kv.Key];
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

// ── Simulate agent loop with background tasks ──
Console.WriteLine("═══ Background Task Execution ═══\n");

// Start some tasks
Console.WriteLine(RunInBackground("dotnet build"));
Console.WriteLine(RunInBackground("npm install"));
Console.WriteLine(RunInBackground("dotnet test"));
Console.WriteLine();

// Poll loop (simulates agent checking between LLM turns)
Console.WriteLine("Polling for completion...\n");
var allTasks = backgroundTasks.Values.ToArray();
while (!Task.WhenAll(allTasks).IsCompleted)
{
    Console.WriteLine($"  [{DateTime.Now:HH:mm:ss}] Checking...");
    foreach (var id in backgroundTasks.Keys)
        Console.WriteLine($"    {CheckTask(id)}");
    await Task.Delay(1000);
}
Console.WriteLine();

// Final status
Console.WriteLine("═══ Final Status ═══");
Console.WriteLine(ListTasks());

// ── Notification injection pattern ──
Console.WriteLine("\n═══ Notification Injection ═══");
var completed = backgroundTasks
    .Where(kv => kv.Value.IsCompletedSuccessfully)
    .Select(kv => (Id: kv.Key, Result: kv.Value.Result))
    .ToList();

foreach (var (id, result) in completed)
    Console.WriteLine($"  <task_notification id=\"{id}\">{result}</task_notification>");

Console.WriteLine("\nDone.");
