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
using SonOfAnton.Backend.Services.Planning;
using SonOfAnton.Shared.Core;

using SonOfAnton.Backend.Infrastructure.Security;


using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var aiEnabled = builder.Configuration.GetValue<bool>("Features:AI");

// Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
var ollamaEndpoint = builder.Configuration.GetValue<string>("Ollama:Endpoint") ?? "http://localhost:11434";
var ollamaModel = builder.Configuration.GetValue<string>("Ollama:Model") ?? "qwen2.5-coder:14b";
var ollamaEmbeddingModel = builder.Configuration.GetValue<string>("Ollama:EmbeddingModel") ?? "nomic-embed-text";

// Add services
builder.Services.AddControllers(options => options.Filters.Add<WorkspaceAccessFilter>())
    .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new CoreControllerFeatureProvider()));
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
                2. `POST /api/workflows` ile bir workflow oluşturun
                3. `POST /api/workflows/{id}/execute` ile çalıştırın
                4. `GET /api/executions/{id}/logs` ile sonuçları görün
                """,
            Contact = new() { Name = "SonOfAnton Ekibi" }
        };
        return Task.CompletedTask;
    });

    // Dinamik endpointleri dokümante eden transformer

});
builder.Services.AddHttpClient();

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.User.FindFirst("sonofanton_user_id")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions {
                PermitLimit = int.TryParse(context.User.FindFirst("RateLimit")?.Value, out var limit) ? Math.Clamp(limit, 1, 1000) : 100,
                Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("workflow-execution", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sonofanton_user_id")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
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


// IClaimsTransformation — rol → izin dönüşümü
builder.Services.AddScoped<IClaimsTransformation, PermissionClaimsTransformation>();

// SCIM Service
builder.Services.AddScoped<ScimService>();

// Authentication: JwtBearer + ApiKey + Policy scheme seçici
// Not: Backend tamamen API olarak çalışır, OIDC interactive login frontend (Blazor WASM) tarafından yönetilir.
// Backend sadece JWT Bearer token doğrulaması yapar.
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "MultiScheme";
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var authConfig = builder.Configuration.GetSection("Authentication");
    options.Authority = authConfig["DefaultAuthority"] ?? throw new InvalidOperationException("Authentication:DefaultAuthority is required.");
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.Audience = authConfig["Audience"] ?? "sonofanton-backend";
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = "preferred_username",
        RoleClaimType = "roles"
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var jit = context.HttpContext.RequestServices.GetRequiredService<JitUserProvisioningService>();
            if (context.Principal is not null)
            {
                try { await jit.ProvisionUserAsync(context.Principal, context.Principal.FindFirst("tenant_id")?.Value ?? "default"); }
                catch (InvalidOperationException) { context.Fail("User or tenant is not authorized."); }
            }
        }
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

        // 2. Authorization header'ında Bearer token varsa veya default → JwtBearer
        return JwtBearerDefaults.AuthenticationScheme;
    };
});

// Authorization: Permission-based policy provider
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

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
    options.WorkerCount = 4;
});

// Semantic Kernel with Ollama
builder.Services.AddSingleton(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    
    #pragma warning disable SKEXP0070
    kernelBuilder.AddOllamaChatCompletion(
        modelId: ollamaModel,
        endpoint: new Uri(ollamaEndpoint));
    
    kernelBuilder.AddOllamaEmbeddingGenerator(
        modelId: ollamaEmbeddingModel,
        endpoint: new Uri(ollamaEndpoint));
    #pragma warning restore SKEXP0070
    
    return kernelBuilder.Build();
});

// Ollama Startup Service - Uygulama balarken Ollama'y kontrol et/balat
// AI is opt-in; core startup never launches external processes or downloads models.

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

builder.Services.AddScoped<INodeHandler, ScheduleTriggerHandler>();

builder.Services.AddScoped<INodeHandler, SetValueHandler>();
builder.Services.AddScoped<INodeHandler, FilterHandler>();
// Arbitrary scripts are excluded from the supported core.
builder.Services.AddScoped<INodeHandler, TemplateHandler>();
builder.Services.AddScoped<INodeHandler, AggregateHandler>();
builder.Services.AddScoped<INodeHandler, IfHandler>();
builder.Services.AddScoped<INodeHandler, SwitchHandler>();

builder.Services.AddScoped<INodeHandler, DelayHandler>();

builder.Services.AddScoped<INodeHandler, DebugHandler>();
builder.Services.AddScoped<INodeHandler, EndHandler>();
if (aiEnabled)
{
    builder.Services.AddScoped<INodeHandler, LLMHandler>();
    builder.Services.AddScoped<INodeHandler, SentimentAnalysisHandler>();
    builder.Services.AddScoped<INodeHandler, TextClassificationHandler>();
    builder.Services.AddScoped<INodeHandler, ImageAnalysisHandler>();
    builder.Services.AddScoped<INodeHandler, AIAgentHandler>();
    builder.Services.AddScoped<ITextEmbeddingService, OllamaTextEmbeddingService>();
}
// Arbitrary database connections are excluded from the supported core.



// Vector nodes require the configured Ollama embedding model (768 dimensions).
if (aiEnabled)
{
    builder.Services.AddScoped<INodeHandler, GenerateEmbeddingHandler>();
    
    
    
}

// Workflow Runner
builder.Services.AddScoped<WorkflowRunner>();
builder.Services.AddScoped<ISingleNodeExecutionService, SingleNodeExecutionService>();

// Workflow Job Service (Hangfire)
builder.Services.AddScoped<WorkflowJobService>();

// Performance Metrics Service (Singleton - uygulama ömrü boyunca yaşar)
builder.Services.AddSingleton<PerformanceMetricsService>();

// Supported core services. Experimental source and migrations remain preserved.
builder.Services.AddSingleton<WorkflowYamlSerializer>();
builder.Services.AddScoped<WorkflowStatsUpdater>();
builder.Services.AddScoped<SnapshotService>();
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<SonOfAnton.Backend.Services.State.IStateRepository, SonOfAnton.Backend.Services.State.WorkflowStateRepository>();
builder.Services.AddSingleton<PauseTokenRegistry>();
builder.Services.AddScoped<WorkspaceService>();
builder.Services.AddScoped<IQuotaService, QuotaService>();
builder.Services.AddSingleton<NotificationStreamService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

var app = builder.Build();

// Fail startup on schema errors instead of serving a partially initialized application.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline

// Response Compression (en üstte)
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options.Title = "SonOfAnton Workflow Engine API";
        options.Theme = ScalarTheme.Purple;
        options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
        options.Authentication = new ScalarAuthenticationOptions
        {
            PreferredSecuritySchemes = ["ApiKey"]
        };
    }).AllowAnonymous();
}

// Global exception handler
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        context.Response.StatusCode = error is UnauthorizedAccessException ? 403 : 500;
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


// Multi-tenant çözümleme (Authentication'dan ÖNCE)


// Authentication & Authorization
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var requestedTenant = context.Request.Headers["X-Tenant-ID"].FirstOrDefault();
    var tenant = context.User.FindFirst("tenant_id")?.Value;
    if (context.User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(requestedTenant) && requestedTenant != tenant)
    { context.Response.StatusCode = 403; return; }
    await next();
});
app.UseRateLimiter();
app.UseAuthorization();

// Dinamik Endpoint Routing Middleware
// (Auth'dan sonra, Controller'lardan önce çalışmalı ki [Authorize] endpointleri engellenmesin
// ama kendi auth mantığını da yapabilsin)
// Published endpoints remain unavailable until their resource/scope contract is audited.

// Health Check Endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false, // Sadece canlılık kontrolü
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { status = "alive", timestamp = DateTime.UtcNow });
    }
}).AllowAnonymous();

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
}).AllowAnonymous();

// Hangfire dashboard is disabled by default.
// One bounded subscription and one writer per client. DB ownership precedes subscription.
app.MapGet("/api/workflow/execution/{executionId:guid}/stream", async (
    Guid executionId, SseEventService events, SonOfAntonDbContext db, HttpContext http, CancellationToken ct) =>
{
    if (!await db.Executions.AnyAsync(e => e.Id == executionId, ct)) return Results.NotFound();
    var session = events.RegisterSession(executionId);
    var channel = events.GetSessionChannel(session.SessionId);
    http.Response.Headers.CacheControl = "no-cache";
    http.Response.Headers["X-Accel-Buffering"] = "no";
    return Results.Stream(async stream =>
    {
        await using var writer = new StreamWriter(stream, leaveOpen: true);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                while (channel.Reader.TryRead(out var evt))
                    await writer.WriteAsync(SseFormatter.FormatPrioritizedEvent(evt, session));
                var execution = await db.Executions.AsNoTracking().FirstOrDefaultAsync(e => e.Id == executionId, ct);
                if (execution is null) break;
                if (execution.Status is SonOfAnton.Shared.Enums.ExecutionStatus.Completed or SonOfAnton.Shared.Enums.ExecutionStatus.Failed or SonOfAnton.Shared.Enums.ExecutionStatus.Cancelled)
                {
                    var result = JsonSerializer.Serialize(new { executionId, success = execution.Status == SonOfAnton.Shared.Enums.ExecutionStatus.Completed, error = execution.ErrorMessage, timestamp = execution.CompletedAt });
                    await writer.WriteAsync($"event: workflow-completed\ndata: {result}\n\n");
                    break;
                }
                if (channel.Reader.Completion.IsCompleted) break;
                await writer.WriteAsync(SseFormatter.FormatComment("heartbeat"));
                await writer.FlushAsync(ct);
                await Task.Delay(500, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally { events.UnregisterSession(session.SessionId); }
    }, "text/event-stream");
}).RequireAuthorization(Permissions.WorkflowsRead);

// SSE Statistics endpoint
app.MapGet("/api/sse/stats", (SseEventService sseService) =>
{
    return Results.Ok(sseService.GetStatistics());
}).WithName("GetSseStatistics").RequireAuthorization(Permissions.SystemAdmin);

// Performance Metrics endpoint
app.MapGet("/api/metrics", (PerformanceMetricsService metricsService) =>
{
    return Results.Ok(metricsService.GetSummary());
}).WithName("GetPerformanceMetrics").RequireAuthorization(Permissions.SystemAdmin);

app.MapControllers();

app.MapGet("/api/capabilities", () => new { ai = aiEnabled, chat = false, experimental = false })
    .RequireAuthorization();

app.Run();

public partial class Program { }
