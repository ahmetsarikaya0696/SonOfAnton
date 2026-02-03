using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.SemanticKernel;
using SonOfAnton.Backend.Data;
using SonOfAnton.Backend.Engine;
using SonOfAnton.Backend.Engine.Handlers;
using SonOfAnton.Backend.Infrastructure.Embeddings;
using SonOfAnton.Backend.Infrastructure.VectorDatabase;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.Core.GraphEngine;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=sonofanton_db;Username=sonofanton;Password=sonofanton_dev_password";
var ollamaEndpoint = builder.Configuration.GetValue<string>("Ollama:Endpoint") ?? "http://localhost:11434";
var ollamaModel = builder.Configuration.GetValue<string>("Ollama:Model") ?? "qwen2.5-coder:14b";
var ollamaEmbeddingModel = builder.Configuration.GetValue<string>("Ollama:EmbeddingModel") ?? "nomic-embed-text";

// Add services
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

// Memory Cache (Embedding cache için)
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 10000; // Max cache items
});

// CORS for Blazor WASM
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorWasm", policy =>
    {
        policy.WithOrigins(
                "https://localhost:5001", 
                "http://localhost:5000",
                "https://localhost:7001",
                "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// EF Core + PostgreSQL + pgvector
builder.Services.AddDbContext<SonOfAntonDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.UseVector();
    });
});

// Hangfire
builder.Services.AddHangfire(config =>
{
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
          .UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings()
          .UsePostgreSqlStorage(options =>
          {
              options.UseNpgsqlConnection(connectionString);
          });
});
builder.Services.AddHangfireServer(options =>
{
    options.Queues = new[] { "default", "workflows", "critical" };
    options.WorkerCount = Environment.ProcessorCount * 2;
});

// Semantic Kernel with Ollama
builder.Services.AddSingleton(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    
    #pragma warning disable SKEXP0070
    kernelBuilder.AddOllamaChatCompletion(
        modelId: ollamaModel,
        endpoint: new Uri(ollamaEndpoint));
    
    kernelBuilder.AddOllamaTextEmbeddingGeneration(
        modelId: ollamaEmbeddingModel,
        endpoint: new Uri(ollamaEndpoint));
    #pragma warning restore SKEXP0070
    
    return kernelBuilder.Build();
});

// Ollama Startup Service - Uygulama balarken Ollama'y kontrol et/balat
builder.Services.AddHostedService<OllamaStartupService>();

// Log Stream Service (Singleton - SSE iin)
builder.Services.AddSingleton<LogStreamService>();

// SSE Event Service (Singleton - Workflow event broadcasting)
builder.Services.AddSingleton<SseEventService>();

// Embedding Cache Service
builder.Services.AddSingleton<EmbeddingCacheService>();

// Vector Database Service (pgvector)
builder.Services.AddSingleton<IVectorDatabaseService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<PgVectorService>>();
    return new PgVectorService(connectionString, logger);
});

// Node Handlers (Strategy Pattern)
builder.Services.AddScoped<INodeHandler, ManualTriggerHandler>();
builder.Services.AddScoped<INodeHandler, WebhookTriggerHandler>();
builder.Services.AddScoped<INodeHandler, ScheduleTriggerHandler>();
builder.Services.AddScoped<INodeHandler, HttpRequestHandler>();
builder.Services.AddScoped<INodeHandler, SetValueHandler>();
builder.Services.AddScoped<INodeHandler, FilterHandler>();
builder.Services.AddScoped<INodeHandler, CodeHandler>();
builder.Services.AddScoped<INodeHandler, IfHandler>();
builder.Services.AddScoped<INodeHandler, SwitchHandler>();
builder.Services.AddScoped<INodeHandler, MergeHandler>();
builder.Services.AddScoped<INodeHandler, DebugHandler>();
builder.Services.AddScoped<INodeHandler, EndHandler>();
builder.Services.AddScoped<INodeHandler, LLMHandler>();

// Vector Node Handlers
builder.Services.AddScoped<INodeHandler, GenerateEmbeddingHandler>();
builder.Services.AddScoped<INodeHandler, VectorStoreHandler>();
builder.Services.AddScoped<INodeHandler, VectorSearchHandler>();
builder.Services.AddScoped<INodeHandler, VectorDeleteHandler>();

// Workflow Runner
builder.Services.AddScoped<WorkflowRunner>();

// Workflow Job Service (Hangfire)
builder.Services.AddScoped<WorkflowJobService>();

// Model Download Service (ONNX models auto-download)
builder.Services.AddHostedService<ModelDownloadService>();
builder.Services.AddSingleton<ModelDownloadService>();

var app = builder.Build();

// Auto-migrate database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("BlazorWasm");
app.UseHttpsRedirection();

// Hangfire Dashboard (Basic Auth ile gvenli hale getirilebilir)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = app.Environment.IsDevelopment() 
        ? [new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter()]
        : []
});

// SSE Endpoint - Workflow execution events stream
app.MapGet("/api/workflow/execution/{executionId:guid}/stream", async (
    Guid executionId,
    SseEventService sseService,
    CancellationToken cancellationToken) =>
{
    var channel = sseService.GetChannel(executionId);
    if (channel == null)
    {
        return Results.NotFound(new { error = "Execution not found or already completed" });
    }
    
    return Results.Stream(async stream =>
    {
        var writer = new StreamWriter(stream) { AutoFlush = true };
        
        // Initial connection event
        await writer.WriteAsync(SseFormatter.FormatComment("connected"));
        
        // Heartbeat timer
        using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        var heartbeatTask = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!await heartbeatTimer.WaitForNextTickAsync(cancellationToken))
                    break;
                await writer.WriteAsync(SseFormatter.FormatHeartbeat());
            }
        }, cancellationToken);
        
        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken))
            {
                var sseFormatted = SseFormatter.FormatEvent(evt);
                await writer.WriteAsync(sseFormatted);
                
                // Workflow tamamlandıysa stream'i kapat
                if (evt is WorkflowCompletedEvent)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
    }, "text/event-stream");
}).WithName("StreamWorkflowExecution");

app.MapControllers();

app.Run();
