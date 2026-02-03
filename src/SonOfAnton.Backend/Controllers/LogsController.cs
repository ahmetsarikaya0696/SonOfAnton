using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly LogStreamService _logStream;
    private readonly SonOfAntonDbContext _dbContext;
    private readonly ILogger<LogsController> _logger;

    public LogsController(LogStreamService logStream, SonOfAntonDbContext dbContext, ILogger<LogsController> logger)
    {
        _logStream = logStream;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// SSE endpoint - Execution log'lar�n� stream eder
    /// �nce veritaban�ndaki ge�mi� loglar� g�nderir, sonra canl� loglar� dinler
    /// </summary>
    [HttpGet("{executionId:guid}")]
    public async Task Stream(Guid executionId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("SSE stream ba�lat�ld�: {ExecutionId}", executionId);

        // SSE headers
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        // Note: Connection header removed - not valid for HTTP/2 and HTTP/3
        
        // Proxy buffering'i a�mak i�in
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            // �nce veritaban�ndaki ge�mi� loglar� g�nder
            var historicalLogs = await _dbContext.ExecutionLogs
                .Where(l => l.ExecutionId == executionId)
                .OrderBy(l => l.Timestamp)
                .ToListAsync(cancellationToken);

            foreach (var log in historicalLogs)
            {
                var logEvent = new LogEventDto
                {
                    ExecutionId = log.ExecutionId,
                    NodeId = log.NodeId,
                    Level = log.Level,
                    Message = log.Message,
                    Data = log.Data != null ? JsonSerializer.Deserialize<object>(log.Data.RootElement.GetRawText()) : null,
                    Timestamp = log.Timestamp
                };

                var json = JsonSerializer.Serialize(logEvent);
                var data = $"data: {json}\n\n";
                
                if (data.Length < 512)
                {
                    data += new string(' ', 512 - data.Length);
                }
                
                await Response.WriteAsync(data, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            // Execution zaten tamamland�ysa stream'i bitir
            var execution = await _dbContext.Executions.FindAsync([executionId], cancellationToken);
            if (execution != null && (execution.Status == Shared.Enums.ExecutionStatus.Completed || 
                                       execution.Status == Shared.Enums.ExecutionStatus.Failed))
            {
                await Response.WriteAsync("event: complete\ndata: done\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
                return;
            }

            // Canl� loglar� dinle
            await foreach (var logEvent in _logStream.ReadAsync(executionId, cancellationToken))
            {
                var json = JsonSerializer.Serialize(logEvent);
                
                // SSE format: data: {json}\n\n
                var data = $"data: {json}\n\n";
                
                if (data.Length < 512)
                {
                    data += new string(' ', 512 - data.Length);
                }
                
                await Response.WriteAsync(data, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            
            // Stream tamamland�
            await Response.WriteAsync("event: complete\ndata: done\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SSE stream iptal edildi: {ExecutionId}", executionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSE stream hatas�: {ExecutionId}", executionId);
        }
    }
}

