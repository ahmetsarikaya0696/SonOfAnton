using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.Orchestrator;
using SonOfAnton.Shared.Models;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly ProjectOrchestrator _orchestrator;
    private readonly ILogger<AgentController> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public AgentController(ProjectOrchestrator orchestrator, ILogger<AgentController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// SSE endpoint - Agent event'lerini stream eder
    /// </summary>
    [HttpPost("stream")]
    public async Task StreamEvents([FromBody] UserRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Starting SSE stream for prompt: {Prompt}", 
            request.Prompt[..Math.Min(50, request.Prompt.Length)]);

        // SSE i�in response header'lar�
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-store";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no"; // nginx buffering'i kapat
        
        // Response buffering'i kapat
        var feature = HttpContext.Features.Get<IHttpResponseBodyFeature>();
        feature?.DisableBuffering();

        try
        {
            await foreach (var evt in _orchestrator.ProcessRequestAsync(request.Prompt, ct))
            {
                var eventData = JsonSerializer.Serialize(evt, _jsonOptions);
                
                // SSE format: "data: {...}\n\n"
                await Response.WriteAsync($"data: {eventData}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }

            // Stream sonu
            await Response.WriteAsync("data: [DONE]\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SSE stream cancelled by client");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSE stream error");
            
            var errorEvent = JsonSerializer.Serialize(new { type = "Error", payload = ex.Message }, _jsonOptions);
            await Response.WriteAsync($"data: {errorEvent}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    /// <summary>
    /// VFS i�eri�ini diske yazar
    /// </summary>
    [HttpPost("flush")]
    public async Task<IActionResult> FlushToDisk([FromBody] FlushRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.BasePath))
        {
            return BadRequest(new { error = "BasePath is required" });
        }

        var success = await _orchestrator.FlushToDiskAsync(request.BasePath, ct);
        
        if (success)
        {
            return Ok(new { message = "Files written to disk successfully" });
        }
        
        return StatusCode(500, new { error = "Failed to write files to disk" });
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}

public class FlushRequest
{
    public required string BasePath { get; set; }
}

