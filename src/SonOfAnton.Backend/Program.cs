using Scalar.AspNetCore;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.SemanticKernel;
using SonOfAnton.Backend.A2A;
using SonOfAnton.Backend.Agents;
using SonOfAnton.Backend.Data;
using SonOfAnton.Backend.Engine;
using SonOfAnton.Backend.Engine.Handlers;
using SonOfAnton.Backend.Execution;
using SonOfAnton.Backend.Infrastructure.Embeddings;
using SonOfAnton.Backend.Infrastructure.VectorDatabase;
using SonOfAnton.Backend.Orchestrator;
using SonOfAnton.Backend.Services;
using SonOfAnton.Backend.Services.DataStore;
using SonOfAnton.Backend.Services.Search;
using SonOfAnton.Backend.Services.Snapshots;
using SonOfAnton.Shared.Core.GraphEngine;
using SonOfAnton.Shared.Serialization;
using SonOfAnton.Shared.VirtualFileSystem;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Backend.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=sonofanton_db;Username=sonofanton;Password=sonofanton_dev_password";
var ollamaEndpoint = builder.Configuration.GetValue<string>("Ollama:Endpoint") ?? "http://localhost:11434";
var ollamaModel = builder.Configuration.GetValue<string>("Ollama:Model") ?? "qwen2.5-coder:14b";
var ollamaEmbeddingModel = builder.Configuration.GetValue<string>("Ollama:EmbeddingModel") ?? "nomic-embed-text";

// Add services
builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "SonOfAnton Workflow Engine API",
            Version = "v1",
            Description = """
                SonOfAnton, görsel workflow oluşturma ve çalıştırma platformudur.
                
                ## Temel Kavramlar
                - **Workflow**: Node'lardan oluşan iş akışı şablonu
                - **Node**: Tek bir işlem birimi (tetikleyici, dönüşüm, mantık vb.)
                - **Execution**: Bir workflow'un tek seferlik çalıştırılması
                - **Edge**: İki node arasındaki veri bağlantısı
                
                ## Hızlı Başlangıç
                1. `GET /api/nodes` ile kullanılabilir node tiplerini keşfedin
                2. `POST /api/workflow` ile bir workflow oluşturun
                3. `POST /api/workflow/{id}/execute` ile çalıştırın
                4. `GET /api/executions/{id}/logs` ile sonuçları görün
                """,
            Contact = new() { Name = "SonOfAnton Ekibi" },
            License = new() { Name = "MIT" }
        };
        return Task.CompletedTask;
    });

    // Dinamik endpointleri dokümante eden transformer
    options.AddDocumentTransformer<SonOfAnton.Backend.Services.WorkflowApiDocsTransformer>();
});
builder.Services.AddHttpClient();

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    // Genel API rate limit
    options.AddPolicy("api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
    
    // Workflow execution rate limit (daha kısıtlı)
    options.AddPolicy("workflow-execution", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 5
            }));
            
    // API Key bazlı dinamik rate limit
    options.AddPolicy("apikey", context =>
    {
        var apiKeyId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(apiKeyId))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1)
                });
        }
        
        var limitStr = context.User.FindFirst("RateLimit")?.Value;
        int limit = int.TryParse(limitStr, out var l) && l > 0 ? l : 60;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: apiKeyId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit,
                Window = TimeSpan.FromMinutes(1)
            });
    });
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql", tags: ["db", "ready"])
    .AddCheck("hangfire", () =>
    {
        try
        {
            var storage = Hangfire.JobStorage.Current;
            return storage != null
                ? HealthCheckResult.Healthy("Hangfire bağlı")
                : HealthCheckResult.Degraded("Hangfire storage yok");
        }
        catch
        {
            return HealthCheckResult.Degraded("Hangfire kontrol edilemiyor");
        }
    }, tags: ["ready"]);

// Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

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
                "http://localhost:5173",
                "http://localhost:8080")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ─── Identity & Authentication ─────────────────────────────────

// HttpContext accessor (IUserContext dependency)
builder.Services.AddHttpContextAccessor();

// IUserContext — Domain katmanın identity soyutlaması
builder.Services.AddScoped<IUserContext, HttpContextUserContext>();

// Tenant çözümleme
builder.Services.AddScoped<ITenantResolver, TenantResolver>();

// JIT User Provisioning
builder.Services.AddScoped<JitUserProvisioningService>();

// Dynamic OIDC Events
builder.Services.AddScoped<DynamicOidcEvents>();

// IClaimsTransformation — rol → izin dönüşümü
builder.Services.AddScoped<IClaimsTransformation, PermissionClaimsTransformation>();

// SCIM Service
builder.Services.AddScoped<ScimService>();

// Authentication: OIDC + JwtBearer (Escape Hatch) + Policy scheme seçici
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "MultiScheme";
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddOpenIdConnect(options =>
{
    var authConfig = builder.Configuration.GetSection("Authentication");
    options.Authority = authConfig["DefaultAuthority"] ?? "https://login.microsoftonline.com/common/v2.0";
    options.ClientId = authConfig["DefaultClientId"] ?? "not-configured";
    options.ClientSecret = authConfig["DefaultClientSecret"];
    options.ResponseType = "code";
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

    // Dinamik tenant OIDC events
    options.EventsType = typeof(DynamicOidcEvents);
})
.AddJwtBearer("ExternalBearer", options =>
{
    // Kaçış kapağı: Sadece JWT kabul eden dış kurumlar için
    var authConfig = builder.Configuration.GetSection("Authentication");
    options.Authority = authConfig["DefaultAuthority"] ?? "https://login.microsoftonline.com/common/v2.0";
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = false, // Keycloak 'aud' claim'i farklı format — dev'de devre dışı
        ValidateLifetime = true,
        NameClaimType = "preferred_username",
        RoleClaimType = "roles"
    };
})
.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.DefaultScheme, options => { })
.AddPolicyScheme("MultiScheme", "Multi-Auth Scheme Selector", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        // 1. ApiKey Header varsa
        if (context.Request.Headers.ContainsKey("X-Api-Key"))
        {
            return ApiKeyAuthenticationOptions.DefaultScheme;
        }

        // 2. Authorization header'ında Bearer token varsa → ExternalBearer
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return "ExternalBearer";
        }

        // Aksi halde → OIDC (interactive login)
        return OpenIdConnectDefaults.AuthenticationScheme;
    };
});

// Authorization: Permission-based policy provider
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

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
builder.Services.AddScoped<INodeHandler, ErrorTriggerHandler>();
builder.Services.AddScoped<INodeHandler, ManualTriggerHandler>();
builder.Services.AddScoped<INodeHandler, WebhookTriggerHandler>();
builder.Services.AddScoped<INodeHandler, ScheduleTriggerHandler>();
builder.Services.AddScoped<INodeHandler, HttpRequestHandler>();
builder.Services.AddScoped<INodeHandler, SetValueHandler>();
builder.Services.AddScoped<INodeHandler, FilterHandler>();
builder.Services.AddScoped<INodeHandler, CodeHandler>();
builder.Services.AddScoped<INodeHandler, TemplateHandler>();
builder.Services.AddScoped<INodeHandler, AggregateHandler>();
builder.Services.AddScoped<INodeHandler, IfHandler>();
builder.Services.AddScoped<INodeHandler, SwitchHandler>();
builder.Services.AddScoped<INodeHandler, MergeHandler>();
builder.Services.AddScoped<INodeHandler, DelayHandler>();
builder.Services.AddScoped<INodeHandler, LoopHandler>();
builder.Services.AddScoped<INodeHandler, DebugHandler>();
builder.Services.AddScoped<INodeHandler, EndHandler>();
builder.Services.AddScoped<INodeHandler, LLMHandler>();
builder.Services.AddScoped<INodeHandler, DatabaseHandler>();
builder.Services.AddScoped<INodeHandler, EmailHandler>();
builder.Services.AddScoped<INodeHandler, SlackHandler>();

// Vector Node Handlers
builder.Services.AddScoped<INodeHandler, GenerateEmbeddingHandler>();
builder.Services.AddScoped<INodeHandler, VectorStoreHandler>();
builder.Services.AddScoped<INodeHandler, VectorSearchHandler>();
builder.Services.AddScoped<INodeHandler, VectorDeleteHandler>();

// Workflow Runner
builder.Services.AddScoped<WorkflowRunner>();
builder.Services.AddScoped<ISingleNodeExecutionService, SingleNodeExecutionService>();

// Workflow Job Service (Hangfire)
builder.Services.AddScoped<WorkflowJobService>();

// Performance Metrics Service (Singleton - uygulama ömrü boyunca yaşar)
builder.Services.AddSingleton<PerformanceMetricsService>();

// Model Download Service (ONNX models auto-download)
builder.Services.AddHostedService<ModelDownloadService>();
builder.Services.AddSingleton<ModelDownloadService>();

// ─── New Feature Services ──────────────────────────────────────

// YAML Serializer (Shared)
builder.Services.AddSingleton<WorkflowYamlSerializer>();

// Virtual File System
builder.Services.AddSingleton<IVirtualFileSystem, InMemoryFileSystem>();

// Agent Services
builder.Services.AddScoped<AgentService>();
builder.Services.AddScoped<WorkflowAssistantService>();

// Orchestrator
builder.Services.AddScoped<ProjectOrchestrator>();

// Workflow Stats Updater
builder.Services.AddScoped<WorkflowStatsUpdater>();

// Search Services (Full-text + Hybrid)
builder.Services.AddScoped<FullTextSearchService>();
builder.Services.AddScoped<HybridSearchService>();

// Snapshot Service
builder.Services.AddScoped<SnapshotService>();

// State Recovery Repository
builder.Services.AddScoped<SonOfAnton.Backend.Services.State.IStateRepository, SonOfAnton.Backend.Services.State.WorkflowStateRepository>();

// Pause/Resume Registry (Singleton)
builder.Services.AddSingleton<PauseTokenRegistry>();

// HNSW Config
builder.Services.AddSingleton(HnswConfig.FromConfiguration(builder.Configuration));

// A2A Protocol Services
builder.Services.AddSingleton<AgentRegistry>();
builder.Services.AddSingleton<A2ATaskManager>();

// Subflow Node Handlers
builder.Services.AddScoped<INodeHandler, ForEachLoopHandler>();
builder.Services.AddScoped<INodeHandler, WhileLoopHandler>();
builder.Services.AddScoped<INodeHandler, ParallelHandler>();
builder.Services.AddScoped<INodeHandler, BreakHandler>();
builder.Services.AddScoped<INodeHandler, ContinueHandler>();
builder.Services.AddScoped<INodeHandler, TryCatchHandler>();

// Custom Node & Marketplace Services
builder.Services.AddScoped<INodeHandler, CustomNodeHandler>();
builder.Services.AddScoped<CustomNodeService>();
builder.Services.AddScoped<MarketplaceService>();
builder.Services.AddSingleton<PluginLoaderService>();

// Data Store Service
builder.Services.AddScoped<IDataStoreService, DataStoreService>();

// Data Store Node Handlers
builder.Services.AddScoped<INodeHandler, StateSetHandler>();
builder.Services.AddScoped<INodeHandler, StateGetHandler>();
builder.Services.AddScoped<INodeHandler, StateDeleteHandler>();
builder.Services.AddScoped<INodeHandler, StateGetAllHandler>();

// Workflow API Publishing Service
builder.Services.AddScoped<WorkflowApiService>();
builder.Services.AddTransient<SonOfAnton.Backend.Services.WorkflowApiDocsTransformer>();

// Workspace Service
builder.Services.AddScoped<WorkspaceService>();

var app = builder.Build();

// Docker servislerini otomatik baslat (PostgreSQL vb.)
if (app.Environment.IsDevelopment())
{
    var dockerLogger = app.Services.GetRequiredService<ILogger<Program>>();
    await SonOfAnton.Backend.Infrastructure.DockerHelper.EnsureDockerServicesRunningAsync(dockerLogger);
}

// Auto-migrate database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

    // A2A Agent'ları kaydet
    var registry = app.Services.GetRequiredService<AgentRegistry>();
    var agentService = scope.ServiceProvider.GetRequiredService<AgentService>();
    registry.Register(new AnalystA2AAgent(agentService));
    registry.Register(new CoderA2AAgent(agentService));
    registry.Register(new AuditorA2AAgent(agentService));
    registry.Register(new VisualizerA2AAgent(agentService));
}

// Plugin node'larını tara (startup)
var pluginLoader = app.Services.GetRequiredService<PluginLoaderService>();
pluginLoader.ScanPlugins();

// Configure the HTTP request pipeline

// Response Compression (en üstte)
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "SonOfAnton Workflow Engine API";
        options.Theme = ScalarTheme.Purple;
        options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
        options.Authentication = new ScalarAuthenticationOptions
        {
            PreferredSecurityScheme = "ApiKey"
        };
    });
}

// Global exception handler
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        
        var response = new { error = "Sunucu hatası oluştu", requestId = context.TraceIdentifier };
        await context.Response.WriteAsJsonAsync(response);
    });
});

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseCors("BlazorWasm");
app.UseHttpsRedirection();
app.UseRateLimiter();

// Multi-tenant çözümleme (Authentication'dan ÖNCE)
app.UseTenantResolution();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Dinamik Endpoint Routing Middleware
// (Auth'dan sonra, Controller'lardan önce çalışmalı ki [Authorize] endpointleri engellenmesin
// ama kendi auth mantığını da yapabilsin)
app.UseMiddleware<SonOfAnton.Backend.Infrastructure.DynamicWorkflowEndpointMiddleware>();

// Health Check Endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false, // Sadece canlılık kontrolü
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { status = "alive", timestamp = DateTime.UtcNow });
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});

// Hangfire Dashboard (Basic Auth ile güvenli hale getirilebilir)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = app.Environment.IsDevelopment() 
        ? [new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter()]
        : []
});

// SSE Endpoint - Workflow execution events stream (enhanced with session tracking, topic filtering)
app.MapGet("/api/workflow/execution/{executionId:guid}/stream", async (
    Guid executionId,
    SseEventService sseService,
    [Microsoft.AspNetCore.Mvc.FromQuery] string? topics,
    CancellationToken cancellationToken) =>
{
    var channel = sseService.GetChannel(executionId);
    if (channel == null)
    {
        return Results.NotFound(new { error = "Execution not found or already completed" });
    }
    
    // Client session oluştur
    var topicList = topics?.Split(',', StringSplitOptions.RemoveEmptyEntries);
    var session = sseService.RegisterSession(executionId, topicList);
    
    return Results.Stream(async stream =>
    {
        var writer = new StreamWriter(stream) { AutoFlush = true };
        
        // Initial connection event
        await writer.WriteAsync(SseFormatter.FormatComment($"connected session={session.SessionId}"));
        
        // Heartbeat timer
        using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        var heartbeatTask = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!await heartbeatTimer.WaitForNextTickAsync(cancellationToken))
                    break;
                await writer.WriteAsync(SseFormatter.FormatHeartbeat());
                sseService.UpdateSessionHeartbeat(session.SessionId);
            }
        }, cancellationToken);
        
        try
        {
            await foreach (var prioritizedEvent in channel.Reader.ReadAllAsync(cancellationToken))
            {
                var sseFormatted = SseFormatter.FormatPrioritizedEvent(prioritizedEvent, session);
                
                // Topic filtresi tarafından atlandıysa boş string döner
                if (string.IsNullOrEmpty(sseFormatted))
                    continue;
                
                await writer.WriteAsync(sseFormatted);
                sseService.TrackBytesTransferred(sseFormatted.Length * 2); // UTF-16
                
                // Workflow tamamlandıysa stream'i kapat
                if (prioritizedEvent.Event is WorkflowCompletedEvent)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
        finally
        {
            sseService.UnregisterSession(session.SessionId);
        }
    }, "text/event-stream");
}).WithName("StreamWorkflowExecution");

// SSE Statistics endpoint
app.MapGet("/api/sse/stats", (SseEventService sseService) =>
{
    return Results.Ok(sseService.GetStatistics());
}).WithName("GetSseStatistics");

// Performance Metrics endpoint
app.MapGet("/api/metrics", (PerformanceMetricsService metricsService) =>
{
    return Results.Ok(metricsService.GetSummary());
}).WithName("GetPerformanceMetrics");

app.MapControllers();

// A2A Well-known endpoint
app.MapGet("/.well-known/agent.json", (AgentRegistry agentRegistry) =>
{
    var cards = agentRegistry.GetAllCards();
    return Results.Ok(new
    {
        agents = cards,
        protocol = "a2a",
        version = "1.0",
        endpoint = "/api/a2a"
    });
}).WithName("A2AAgentCard");

app.Run();
