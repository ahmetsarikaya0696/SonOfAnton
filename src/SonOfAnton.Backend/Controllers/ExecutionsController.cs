using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExecutionsController : ControllerBase
{
    private readonly SonOfAntonDbContext _dbContext;

    public ExecutionsController(SonOfAntonDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// T�m execution'lar� listele
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ExecutionDto>>> GetAll([FromQuery] Guid? workflowId = null)
    {
        var query = _dbContext.Executions
            .Include(e => e.Workflow)
            .AsQueryable();

        if (workflowId.HasValue)
        {
            query = query.Where(e => e.WorkflowId == workflowId.Value);
        }

        var executions = await query
            .OrderByDescending(e => e.StartedAt)
            .Take(100)
            .Select(e => new ExecutionDto
            {
                Id = e.Id,
                WorkflowId = e.WorkflowId,
                WorkflowName = e.Workflow.Name,
                Status = e.Status,
                CurrentNodeId = e.CurrentNodeId,
                ErrorMessage = e.ErrorMessage,
                StartedAt = e.StartedAt,
                CompletedAt = e.CompletedAt
            })
            .ToListAsync();

        return Ok(executions);
    }

    /// <summary>
    /// Execution detay�n� getir
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExecutionDto>> Get(Guid id)
    {
        var execution = await _dbContext.Executions
            .Include(e => e.Workflow)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (execution == null)
            return NotFound();

        return Ok(new ExecutionDto
        {
            Id = execution.Id,
            WorkflowId = execution.WorkflowId,
            WorkflowName = execution.Workflow.Name,
            Status = execution.Status,
            CurrentNodeId = execution.CurrentNodeId,
            ErrorMessage = execution.ErrorMessage,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt
        });
    }

    /// <summary>
    /// Execution log'lar�n� getir
    /// </summary>
    [HttpGet("{id:guid}/logs")]
    public async Task<ActionResult<List<LogEventDto>>> GetLogs(Guid id)
    {
        var logs = await _dbContext.ExecutionLogs
            .Where(l => l.ExecutionId == id)
            .OrderBy(l => l.Timestamp)
            .Select(l => new LogEventDto
            {
                ExecutionId = l.ExecutionId,
                NodeId = l.NodeId,
                Level = l.Level,
                Message = l.Message,
                Timestamp = l.Timestamp
            })
            .ToListAsync();

        return Ok(logs);
    }

    /// <summary>
    /// Execution'� iptal et
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var execution = await _dbContext.Executions.FindAsync(id);
        if (execution == null)
            return NotFound();

        if (execution.Status != ExecutionStatus.Running && execution.Status != ExecutionStatus.Queued)
        {
            return BadRequest("Bu execution iptal edilemez");
        }

        // Hangfire job'�n� iptal etmek i�in job ID kullan�labilir
        // Burada sadece status'u g�ncelliyoruz
        execution.Status = ExecutionStatus.Cancelled;
        execution.CompletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return Ok();
    }
}

