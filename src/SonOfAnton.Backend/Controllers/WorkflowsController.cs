using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Backend.Engine;
using SonOfAnton.Backend.Execution;
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
    private readonly PauseTokenRegistry _pauseTokens;
    private readonly ILogger<WorkflowsController> _logger;

    public WorkflowsController(
        SonOfAntonDbContext dbContext,
        IBackgroundJobClient jobClient,
        PauseTokenRegistry pauseTokens,
        ILogger<WorkflowsController> logger)
    {
        _dbContext = dbContext;
        _jobClient = jobClient;
        _pauseTokens = pauseTokens;
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
                UpdatedAt = w.UpdatedAt,
                RunCount = w.RunCount,
                SuccessCount = w.SuccessCount,
                FailureCount = w.FailureCount,
                AvgDurationMs = w.AvgDurationMs,
                LastRunAt = w.LastRunAt,
                FolderId = w.FolderId,
                FolderName = w.Folder != null ? w.Folder.Name : null
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

    /// <summary>
    /// Workflow'u başka klasöre taşı
    /// </summary>
    [HttpPatch("{id:guid}/move")]
    public async Task<IActionResult> MoveWorkflow(Guid id, [FromBody] MoveWorkflowRequest request)
    {
        var workflow = await _dbContext.Workflows.FindAsync(id);
        if (workflow == null) return NotFound();

        workflow.FolderId = request.FolderId;
        workflow.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Workflow moved" });
    }

    /// <summary>
    /// Dashboard istatistikleri
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetDashboardStats()
    {
        var today = DateTime.UtcNow.Date;

        var totalWorkflows = await _dbContext.Workflows.CountAsync();
        var activeWorkflows = await _dbContext.Workflows.CountAsync(w => w.IsActive);
        var totalExecutions = await _dbContext.Executions.CountAsync();
        var todayExecutions = await _dbContext.Executions.CountAsync(e => e.StartedAt >= today);
        var successfulExecutions = await _dbContext.Executions.CountAsync(e => e.Status == ExecutionStatus.Completed);

        var topWorkflows = await _dbContext.Workflows
            .Where(w => w.RunCount > 0)
            .OrderByDescending(w => w.RunCount)
            .Take(10)
            .Select(w => new WorkflowStatsDto
            {
                WorkflowId = w.Id,
                WorkflowName = w.Name,
                TotalRuns = w.RunCount,
                SuccessCount = w.SuccessCount,
                FailureCount = w.FailureCount,
                SuccessRate = w.RunCount > 0 ? (double)w.SuccessCount / w.RunCount * 100 : 0,
                AvgDurationMs = w.AvgDurationMs,
                LastRunAt = w.LastRunAt
            })
            .ToListAsync();

        return Ok(new DashboardStatsDto
        {
            TotalWorkflows = totalWorkflows,
            ActiveWorkflows = activeWorkflows,
            TotalExecutions = totalExecutions,
            TodayExecutions = todayExecutions,
            OverallSuccessRate = totalExecutions > 0 ? (double)successfulExecutions / totalExecutions * 100 : 0,
            TopWorkflows = topWorkflows
        });
    }

    /// <summary>
    /// Tek workflow istatistik detayı
    /// </summary>
    [HttpGet("{id:guid}/stats")]
    public async Task<ActionResult<WorkflowStatsDto>> GetWorkflowStats(Guid id, [FromQuery] int days = 30)
    {
        var workflow = await _dbContext.Workflows.FindAsync(id);
        if (workflow == null) return NotFound();

        var since = DateTime.UtcNow.AddDays(-days).Date;

        var dailyStats = await _dbContext.Executions
            .Where(e => e.WorkflowId == id && e.StartedAt >= since)
            .GroupBy(e => e.StartedAt.Date)
            .Select(g => new DailyStatsDto
            {
                Date = DateOnly.FromDateTime(g.Key),
                RunCount = g.Count(),
                SuccessCount = g.Count(e => e.Status == ExecutionStatus.Completed),
                FailureCount = g.Count(e => e.Status == ExecutionStatus.Failed)
            })
            .OrderBy(d => d.Date)
            .ToListAsync();

        return Ok(new WorkflowStatsDto
        {
            WorkflowId = workflow.Id,
            WorkflowName = workflow.Name,
            TotalRuns = workflow.RunCount,
            SuccessCount = workflow.SuccessCount,
            FailureCount = workflow.FailureCount,
            SuccessRate = workflow.RunCount > 0 ? (double)workflow.SuccessCount / workflow.RunCount * 100 : 0,
            AvgDurationMs = workflow.AvgDurationMs,
            LastRunAt = workflow.LastRunAt,
            DailyStats = dailyStats
        });
    }

    /// <summary>
    /// Execution'ı duraklat
    /// </summary>
    [HttpPost("execution/{executionId:guid}/pause")]
    public async Task<IActionResult> PauseExecution(Guid executionId)
    {
        var execution = await _dbContext.Executions.FindAsync(executionId);
        if (execution == null) return NotFound();

        if (execution.Status != ExecutionStatus.Running)
            return BadRequest(new { error = "Only running executions can be paused" });

        execution.Status = ExecutionStatus.Paused;
        await _dbContext.SaveChangesAsync();

        _pauseTokens.PauseExecution(executionId);

        _logger.LogInformation("Execution duraklatıldı: {ExecutionId}", executionId);
        return Ok(new { message = "Execution paused" });
    }

    /// <summary>
    /// Execution'ı devam ettir
    /// </summary>
    [HttpPost("execution/{executionId:guid}/resume")]
    public async Task<IActionResult> ResumeExecution(Guid executionId)
    {
        var execution = await _dbContext.Executions.FindAsync(executionId);
        if (execution == null) return NotFound();

        if (execution.Status != ExecutionStatus.Paused)
            return BadRequest(new { error = "Only paused executions can be resumed" });

        execution.Status = ExecutionStatus.Resuming;
        await _dbContext.SaveChangesAsync();

        _pauseTokens.ResumeExecution(executionId);

        _logger.LogInformation("Execution devam ettiriliyor: {ExecutionId}", executionId);
        return Ok(new { message = "Execution resuming" });
    }

    /// <summary>
    /// Execution snapshot al
    /// </summary>
    [HttpPost("execution/{executionId:guid}/snapshot")]
    public async Task<ActionResult<SnapshotDto>> TakeSnapshot(Guid executionId, [FromBody] CreateSnapshotRequest? request = null)
    {
        var execution = await _dbContext.Executions.FindAsync(executionId);
        if (execution == null) return NotFound();

        var snapshot = new Shared.Entities.ExecutionSnapshotEntity
        {
            ExecutionId = executionId,
            Label = request?.Label,
            MemorySnapshot = execution.Memory,
            AtNodeId = execution.CurrentNodeId,
            IsAutomatic = false
        };

        _dbContext.ExecutionSnapshots.Add(snapshot);
        await _dbContext.SaveChangesAsync();

        return Ok(new SnapshotDto
        {
            Id = snapshot.Id,
            ExecutionId = snapshot.ExecutionId,
            Label = snapshot.Label,
            AtNodeId = snapshot.AtNodeId,
            IsAutomatic = snapshot.IsAutomatic,
            CreatedAt = snapshot.CreatedAt
        });
    }

    /// <summary>
    /// Execution snapshot'larını listele
    /// </summary>
    [HttpGet("execution/{executionId:guid}/snapshots")]
    public async Task<ActionResult<List<SnapshotDto>>> GetSnapshots(Guid executionId)
    {
        var snapshots = await _dbContext.ExecutionSnapshots
            .Where(s => s.ExecutionId == executionId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SnapshotDto
            {
                Id = s.Id,
                ExecutionId = s.ExecutionId,
                Label = s.Label,
                AtNodeId = s.AtNodeId,
                IsAutomatic = s.IsAutomatic,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();

        return Ok(snapshots);
    }

    /// <summary>
    /// Snapshot'tan yeni execution başlat
    /// </summary>
    [HttpPost("execution/{executionId:guid}/snapshot/{snapshotId:guid}/restore")]
    public async Task<ActionResult<ExecutionDto>> RestoreFromSnapshot(Guid executionId, Guid snapshotId)
    {
        var snapshot = await _dbContext.ExecutionSnapshots
            .Include(s => s.Execution)
            .FirstOrDefaultAsync(s => s.Id == snapshotId && s.ExecutionId == executionId);

        if (snapshot == null) return NotFound();

        var newExecution = new Shared.Entities.ExecutionEntity
        {
            WorkflowId = snapshot.Execution.WorkflowId,
            Status = ExecutionStatus.Queued,
            Memory = snapshot.MemorySnapshot,
            CurrentNodeId = snapshot.AtNodeId
        };

        _dbContext.Executions.Add(newExecution);
        await _dbContext.SaveChangesAsync();

        var jobId = _jobClient.Enqueue<WorkflowRunner>(r => r.RunAsync(newExecution.Id, CancellationToken.None));
        newExecution.HangfireJobId = jobId;
        await _dbContext.SaveChangesAsync();

        return Ok(new ExecutionDto
        {
            Id = newExecution.Id,
            WorkflowId = newExecution.WorkflowId,
            Status = newExecution.Status,
            StartedAt = newExecution.StartedAt
        });
    }

    /// <summary>
    /// Workflow artifact'larını getir (YAML, Mermaid vb.)
    /// </summary>
    [HttpGet("{id:guid}/artifacts")]
    public async Task<ActionResult<List<PlanArtifactDto>>> GetArtifacts(Guid id, [FromQuery] string? type = null)
    {
        var workflow = await _dbContext.Workflows.FindAsync(id);
        if (workflow == null) return NotFound();

        if (workflow.GraphData == null)
            return Ok(new List<PlanArtifactDto>());

        var graph = System.Text.Json.JsonSerializer.Deserialize<WorkflowGraphDto>(
            workflow.GraphData.RootElement.GetRawText());

        if (graph == null)
            return Ok(new List<PlanArtifactDto>());

        var artifacts = new List<PlanArtifactDto>();

        if (type == null || type == "json")
        {
            artifacts.Add(new PlanArtifactDto
            {
                Type = Shared.Enums.ArtifactType.Json,
                Content = System.Text.Json.JsonSerializer.Serialize(graph, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
                FileName = $"{workflow.Name}.json"
            });
        }

        if (type == null || type == "yaml")
        {
            var yamlSerializer = HttpContext.RequestServices.GetService<Shared.Serialization.WorkflowYamlSerializer>();
            if (yamlSerializer != null)
            {
                artifacts.Add(new PlanArtifactDto
                {
                    Type = Shared.Enums.ArtifactType.Yaml,
                    Content = yamlSerializer.Serialize(graph),
                    FileName = $"{workflow.Name}.yaml"
                });
            }
        }

        return Ok(artifacts);
    }
}


