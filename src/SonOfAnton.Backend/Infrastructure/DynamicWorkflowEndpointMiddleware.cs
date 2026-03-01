using Microsoft.AspNetCore.Authentication;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Backend.Services;

namespace SonOfAnton.Backend.Infrastructure;

/// <summary>
/// /api/published/* rotalarına gelen istekleri yakalayıp workflow tetikleyen dinamik middleware.
/// </summary>
public class DynamicWorkflowEndpointMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DynamicWorkflowEndpointMiddleware> _logger;

    public DynamicWorkflowEndpointMiddleware(RequestDelegate next, ILogger<DynamicWorkflowEndpointMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Sadece /api/published ile başlayan istekleri işle
        if (!path.StartsWith("/api/published/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Setup services
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();
        var jobService = scope.ServiceProvider.GetRequiredService<WorkflowJobService>();

        var route = path.Substring("/api/published/".Length).Trim('/');
        var method = context.Request.Method.ToUpper();

        // 1. Endpoint'i bul (normalize edilmiş route ile karşılaştır)
        var endpoint = await dbContext.WorkflowApiEndpoints
            .FirstOrDefaultAsync(e => e.IsActive && e.HttpMethod == method && (e.Route == route || e.Route == "/" + route));

        if (endpoint == null)
        {
            _logger.LogWarning("Published API endpoint bulunamadı: {Method} {Route}", method, route);
            
            // Eğer route aynı ama method farklıysa 405 Method Not Allowed dönelim
            var anyEndpoint = await dbContext.WorkflowApiEndpoints.AnyAsync(e => e.IsActive && (e.Route == route || e.Route == "/" + route));
            if (anyEndpoint)
            {
                context.Response.StatusCode = 405; // Method Not Allowed
                await context.Response.WriteAsJsonAsync(new { error = $"Method {method} not allowed for this route." });
                return;
            }

            context.Response.StatusCode = 404; // Not Found
            await context.Response.WriteAsJsonAsync(new { error = "Endpoint not found." });
            return;
        }

        // 2. Authentication Kontrolü (RequireApiKey)
        if (endpoint.RequireApiKey)
        {
            var authResult = await context.AuthenticateAsync(Identity.ApiKeyAuthenticationOptions.DefaultScheme);
            if (!authResult.Succeeded)
            {
                context.Response.StatusCode = 401; // Unauthorized
                await context.Response.WriteAsJsonAsync(new { error = "Valid API Key required." });
                return;
            }
            context.User = authResult.Principal;
        }

        // 3. Payload'u oku
        JsonElement? payload = null;
        if (method == "POST" || method == "PUT" || method == "PATCH")
        {
            try
            {
                // Allow multiple reads in case downstream needs the body
                context.Request.EnableBuffering();
                using var bodyReader = new StreamReader(context.Request.Body, leaveOpen: true);
                var bodyString = await bodyReader.ReadToEndAsync();
                context.Request.Body.Position = 0;
                if (!string.IsNullOrWhiteSpace(bodyString))
                {
                    payload = JsonSerializer.Deserialize<JsonElement>(bodyString);
                }
            }
            catch (JsonException ex)
            {
                context.Response.StatusCode = 400; // Bad Request
                await context.Response.WriteAsJsonAsync(new { error = "Invalid JSON body.", details = ex.Message });
                return;
            }
        }
        else if (method == "GET" || method == "DELETE")
        {
            // GET/DELETE parametrelerini querystring'den al
            var queryDict = context.Request.Query
                .ToDictionary(q => q.Key, q => (object?)q.Value.ToString());
            
            if (queryDict.Any())
            {
                payload = JsonSerializer.SerializeToElement(queryDict);
            }
        }

        // TODO: InputSchema Validation could be added here

        // 4. Trigger Data Hazırla
        var triggerData = new Dictionary<string, object?>
        {
            ["route"] = route,
            ["method"] = method,
            ["triggered_at"] = DateTime.UtcNow,
            ["source_ip"] = context.Connection.RemoteIpAddress?.ToString(),
            ["headers"] = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
            ["payload"] = payload.HasValue ? JsonSerializer.Deserialize<object>(payload.Value.GetRawText()) : null
        };

        if (context.User.Identity?.IsAuthenticated == true)
        {
            triggerData["user_id"] = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        }

        // 5. Workflow'u Tetikle
        var executionId = jobService.EnqueueWorkflow(endpoint.WorkflowId, triggerData);
        _logger.LogInformation("API trigger successful: Endpoint {EndpointId}, Execution {ExecutionId}", endpoint.Id, executionId);

        // 6. Yanıt Dön
        if (!endpoint.WaitForResult)
        {
            context.Response.StatusCode = 202; // Accepted
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Workflow execution triggered.",
                executionId = executionId
            });
            return;
        }

        // Sonucu bekle
        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(endpoint.WaitTimeoutSeconds));
            var isCompleted = await WaitForResultAsync(dbContext, Guid.Parse(executionId), cts.Token);

            if (!isCompleted)
            {
                context.Response.StatusCode = 504; // Gateway Timeout
                await context.Response.WriteAsJsonAsync(new { error = "Execution timeout", executionId = executionId });
                return;
            }

            var execution = await dbContext.Executions
                .FirstOrDefaultAsync(e => e.Id == Guid.Parse(executionId));

            if (execution == null)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = "Execution not found after completion." });
                return;
            }

            if (!string.IsNullOrWhiteSpace(endpoint.OutputTemplate))
            {
                var contextData = new Dictionary<string, object?>
                {
                    ["executionId"] = execution.Id.ToString(),
                    ["status"] = execution.Status.ToString(),
                    ["memory"] = execution.Memory,
                    ["error"] = execution.ErrorMessage
                };

                var renderedContent = ResponseTemplateEngine.Render(endpoint.OutputTemplate, contextData);
                context.Response.ContentType = endpoint.ResponseContentType ?? "application/json";
                await context.Response.WriteAsync(renderedContent);
                return;
            }

            // Default response
            context.Response.StatusCode = execution.Status == Shared.Enums.ExecutionStatus.Completed ? 200 : 500;
            await context.Response.WriteAsJsonAsync(new
            {
                executionId = execution.Id,
                status = execution.Status.ToString(),
                memory = execution.Memory,
                error = execution.ErrorMessage
            });
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = 504; // Gateway Timeout
            await context.Response.WriteAsJsonAsync(new { error = "Execution timeout", executionId = executionId });
        }
    }

    private async Task<bool> WaitForResultAsync(SonOfAntonDbContext dbContext, Guid executionId, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var status = await dbContext.Executions
                .Where(e => e.Id == executionId)
                .Select(e => e.Status)
                .FirstOrDefaultAsync(cancellationToken);

            if (status == Shared.Enums.ExecutionStatus.Completed ||
                status == Shared.Enums.ExecutionStatus.Failed ||
                status == Shared.Enums.ExecutionStatus.Cancelled)
            {
                return true;
            }

            await Task.Delay(500, cancellationToken);
        }
        return false;
    }
}
