using System.ClientModel;
using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenAI;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

// ── OpenTelemetry Setup ──
var serviceName = "AgentTutorial";
var sourceName = "AgentTracing";
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
    .AddSource(sourceName)
    .AddHttpClientInstrumentation()
    .AddOtlpExporter()
    .Build();

// ── Register IChatClient ──
var baseUrl = builder.Configuration["baseUrl"] ?? "https://api.deepseek.com/v1";
var apiKey = builder.Configuration["apiKey"] ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? "";
var modelId = builder.Configuration["modelId"] ?? "deepseek-chat";

IChatClient baseClient = new OpenAIClient(new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })
    .GetChatClient(modelId).AsIChatClient();

// ── Instrument with OpenTelemetry ──
var instrumentedClient = baseClient.AsBuilder()
    .UseFunctionInvocation()
    .UseOpenTelemetry(sourceName: sourceName, configure: c => c.EnableSensitiveData = true)
    .Build();

builder.Services.AddSingleton<IChatClient>(instrumentedClient);

// ── Register Agent ──
builder.AddAIAgent("assistant", instructions: "You are a helpful assistant.");

var app = builder.Build();

// ── Minimal API ──
app.MapPost("/chat", async (string message, IChatClient client) =>
{
    using var activity = new ActivitySource(sourceName).StartActivity("ManualChat");
    var response = await client.GetResponseAsync(message);
    return Results.Ok(new { response = response.Text });
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
