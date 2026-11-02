using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Enums;
namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Permissions.WorkflowsRead)]
public sealed class LogsController(SonOfAntonDbContext db) : ControllerBase
{
    // Durable paged reading avoids the gap between history and a live subscription.
    [HttpGet("{executionId:guid}")]
    public async Task Stream(Guid executionId, CancellationToken ct)
    {
        if (!await db.Executions.AnyAsync(e => e.Id == executionId, ct)) { Response.StatusCode = 404; return; }
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        var timestamp = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
        var lastId = Guid.Empty;
        if (Request.Headers["Last-Event-ID"].FirstOrDefault() is string cursor)
        {
            var parts = cursor.Split('/');
            if (parts.Length == 2 && long.TryParse(parts[0], out var ticks) && ticks >= 0 && ticks <= DateTime.MaxValue.Ticks && Guid.TryParse(parts[1], out var id))
            { timestamp = new DateTime(ticks, DateTimeKind.Utc); lastId = id; }
        }
        bool terminal = false;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var logs = await db.ExecutionLogs.AsNoTracking().Where(l => l.ExecutionId == executionId &&
                    (l.Timestamp > timestamp || l.Timestamp == timestamp && l.Id.CompareTo(lastId) > 0))
                    .OrderBy(l => l.Timestamp).ThenBy(l => l.Id).Take(200).ToListAsync(ct);
                foreach (var log in logs)
                {
                    var item = new LogEventDto { ExecutionId = executionId, NodeId = log.NodeId, Level = log.Level,
                        Message = log.Message, Data = log.Data?.RootElement.Clone(), Timestamp = log.Timestamp };
                    timestamp = log.Timestamp; lastId = log.Id;
                    await Response.WriteAsync($"id: {timestamp.Ticks}/{lastId}\ndata: {JsonSerializer.Serialize(item)}\n\n", ct);
                }
                await Response.Body.FlushAsync(ct);
                if (logs.Count == 200) continue;
                if (terminal)
                {
                    await Response.WriteAsync("event: complete\ndata: done\n\n", ct);
                    await Response.Body.FlushAsync(ct); return;
                }
                var state = await db.Executions.AsNoTracking().Where(e => e.Id == executionId).Select(e => (ExecutionStatus?)e.Status).FirstOrDefaultAsync(ct);
                terminal = state is null or ExecutionStatus.Completed or ExecutionStatus.Failed or ExecutionStatus.Cancelled;
                await Response.WriteAsync(": heartbeat\n\n", ct);
                await Task.Delay(500, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }
}
