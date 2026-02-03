using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Backend.Engine;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkflowsController : ControllerBase
{
    private readonly SonOfAntonDbContext _dbContext;
    private readonly IBackgroundJobClient _jobClient;
    private readonly ILogger<WorkflowsController> _logger;

    public WorkflowsController(
        SonOfAntonDbContext dbContext,
        IBackgroundJobClient jobClient,
        ILogger<WorkflowsController> logger)
    {
        _dbContext = dbContext;
        _jobClient = jobClient;
        _logger = logger;
    }

    /// <summary>
    /// T�m workflow'lar� listele
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<WorkflowDto>>> GetAll()
    {
        var workflows = await _dbContext.Workflows
            .OrderByDescending(w => w.UpdatedAt)
            .Select(w => new WorkflowDto
            {
                Id = w.Id,
                Name = w.Name,
                Description = w.Description,
                TriggerType = w.TriggerType,
                IsActive = w.IsActive,
                CreatedAt = w.CreatedAt,
                UpdatedAt = w.UpdatedAt
            })
            .ToListAsync();

        return Ok(workflows);
    }

    /// <summary>
    /// Workflow detay�n� getir (graph dahil)
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkflowDto>> Get(Guid id)
    {
        var workflow = await _dbContext.Workflows.FindAsync(id);
        if (workflow == null)
            return NotFound();

        var dto = new WorkflowDto
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Description = workflow.Description,
            TriggerType = workflow.TriggerType,
            IsActive = workflow.IsActive,
            CreatedAt = workflow.CreatedAt,
            UpdatedAt = workflow.UpdatedAt
        };

        if (workflow.GraphData != null)
        {
            dto.Graph = JsonSerializer.Deserialize<WorkflowGraphDto>(
                workflow.GraphData.RootElement.GetRawText());
        }

        return Ok(dto);
    }

    /// <summary>
    /// Workflow kaydet (olu�tur veya g�ncelle)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<WorkflowDto>> Save([FromBody] SaveWorkflowRequest request)
    {
        WorkflowEntity workflow;

        if (request.Id.HasValue)
        {
            // G�ncelleme
            workflow = await _dbContext.Workflows.FindAsync(request.Id.Value);
            if (workflow == null)
                return NotFound();

            workflow.Name = request.Name;
            workflow.Description = request.Description;
            workflow.TriggerType = request.TriggerType;
            workflow.IsActive = request.IsActive;
            workflow.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Yeni olu�tur
            workflow = new WorkflowEntity
            {
                Name = request.Name,
                Description = request.Description,
                TriggerType = request.TriggerType,
                IsActive = request.IsActive
            };
            _dbContext.Workflows.Add(workflow);
        }

        if (request.Graph != null)
        {
            workflow.GraphData = JsonDocument.Parse(JsonSerializer.Serialize(request.Graph));
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Workflow kaydedildi: {WorkflowId} - {Name}", workflow.Id, workflow.Name);

        return Ok(new WorkflowDto
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Description = workflow.Description,
            TriggerType = workflow.TriggerType,
            IsActive = workflow.IsActive,
            CreatedAt = workflow.CreatedAt,
            UpdatedAt = workflow.UpdatedAt,
            Graph = request.Graph
        });
    }

    /// <summary>
    /// Workflow sil
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var workflow = await _dbContext.Workflows.FindAsync(id);
        if (workflow == null)
            return NotFound();

        _dbContext.Workflows.Remove(workflow);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Workflow silindi: {WorkflowId}", id);

        return NoContent();
    }

    /// <summary>
    /// Workflow'u �al��t�r
    /// </summary>
    [HttpPost("{id:guid}/execute")]
    public async Task<ActionResult<ExecutionDto>> Execute(Guid id, [FromBody] StartExecutionRequest? request = null)
    {
        var workflow = await _dbContext.Workflows.FindAsync(id);
        if (workflow == null)
            return NotFound();

        // Execution kayd� olu�tur
        var execution = new ExecutionEntity
        {
            WorkflowId = id,
            Status = ExecutionStatus.Queued
        };

        if (request?.InitialData != null)
        {
            execution.Memory = JsonDocument.Parse(JsonSerializer.Serialize(request.InitialData));
        }

        _dbContext.Executions.Add(execution);
        await _dbContext.SaveChangesAsync();

        // Hangfire job'� ba�lat
        var jobId = _jobClient.Enqueue<WorkflowRunner>(runner => 
            runner.RunAsync(execution.Id, CancellationToken.None));

        execution.HangfireJobId = jobId;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Workflow �al��t�r�l�yor: {WorkflowId} -> Execution: {ExecutionId}", id, execution.Id);

        return Ok(new ExecutionDto
        {
            Id = execution.Id,
            WorkflowId = id,
            WorkflowName = workflow.Name,
            Status = execution.Status,
            StartedAt = execution.StartedAt
        });
    }
    
    /// <summary>
    /// Execution durumunu getir
    /// </summary>
    [HttpGet("execution/{executionId:guid}")]
    public async Task<ActionResult<ExecutionDto>> GetExecution(Guid executionId)
    {
        var execution = await _dbContext.Executions
            .Include(e => e.Workflow)
            .FirstOrDefaultAsync(e => e.Id == executionId);
            
        if (execution == null)
            return NotFound();
        
        return Ok(new ExecutionDto
        {
            Id = execution.Id,
            WorkflowId = execution.WorkflowId,
            WorkflowName = execution.Workflow?.Name ?? "Unknown",
            Status = execution.Status,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            ErrorMessage = execution.ErrorMessage
        });
    }
    
    /// <summary>
    /// Workflow execution geçmişini getir
    /// </summary>
    [HttpGet("{id:guid}/executions")]
    public async Task<ActionResult<List<ExecutionDto>>> GetExecutionHistory(Guid id, [FromQuery] int limit = 20)
    {
        var executions = await _dbContext.Executions
            .Where(e => e.WorkflowId == id)
            .OrderByDescending(e => e.StartedAt)
            .Take(limit)
            .Include(e => e.Workflow)
            .Select(e => new ExecutionDto
            {
                Id = e.Id,
                WorkflowId = e.WorkflowId,
                WorkflowName = e.Workflow != null ? e.Workflow.Name : "Unknown",
                Status = e.Status,
                StartedAt = e.StartedAt,
                CompletedAt = e.CompletedAt,
                ErrorMessage = e.ErrorMessage
            })
            .ToListAsync();
        
        return Ok(executions);
    }
    
    /// <summary>
    /// Çalışan execution'ı iptal et
    /// </summary>
    [HttpPost("execution/{executionId:guid}/cancel")]
    public async Task<IActionResult> CancelExecution(Guid executionId)
    {
        var execution = await _dbContext.Executions.FindAsync(executionId);
        if (execution == null)
            return NotFound();
        
        if (execution.Status != ExecutionStatus.Running && execution.Status != ExecutionStatus.Queued)
            return BadRequest(new { error = "Only running or queued executions can be cancelled" });
        
        // Hangfire job'ı iptal et
        if (!string.IsNullOrEmpty(execution.HangfireJobId))
        {
            BackgroundJob.Delete(execution.HangfireJobId);
        }
        
        execution.Status = ExecutionStatus.Cancelled;
        execution.CompletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        
        _logger.LogInformation("Execution iptal edildi: {ExecutionId}", executionId);
        
        return Ok(new { message = "Execution cancelled" });
    }
}


