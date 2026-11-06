using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Backend.Engine;
using SonOfAnton.Backend.Execution;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;
using SonOfAnton.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using SonOfAnton.Shared.Core.Identity;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Permissions.WorkflowsRead)]
public class WorkflowsController : ControllerBase
{
    private readonly SonOfAntonDbContext _dbContext;
    private readonly IUserContext _user;
    private readonly WorkflowJobService _jobs;
    private readonly PauseTokenRegistry _pauseTokens;
    private readonly ISingleNodeExecutionService _singleNodeSvc;
    private readonly ILogger<WorkflowsController> _logger;

    public WorkflowsController(
        SonOfAntonDbContext dbContext,
        IUserContext user,
        WorkflowJobService jobs,
        PauseTokenRegistry pauseTokens,
        ISingleNodeExecutionService singleNodeSvc,
        ILogger<WorkflowsController> logger)
    {
        _dbContext = dbContext;
        _user = user;
        _jobs = jobs;
        _pauseTokens = pauseTokens;
        _singleNodeSvc = singleNodeSvc;
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
                WorkspaceId = w.WorkspaceId,
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
            WorkspaceId = workflow.WorkspaceId,
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
    [Authorize(Policy = Permissions.WorkflowsEdit)]
    public async Task<ActionResult<WorkflowDto>> Save([FromBody] SaveWorkflowRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Name is required.");
        if (request.GlobalErrorWorkflowId.HasValue) return BadRequest("Automatic error workflow chains are unavailable.");
        var graphError = WorkflowGraphValidation.Validate(request.Graph, HttpContext.RequestServices.GetServices<INodeHandler>());
        if (graphError is not null) return BadRequest(graphError);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        if (request.Id.HasValue) await _dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({request.Id.Value.ToString()}))");
        Guid? workspaceId = request.WorkspaceId;
        if (!workspaceId.HasValue && request.Id.HasValue) workspaceId = await _dbContext.Workflows.Where(w => w.Id == request.Id).Select(w => w.WorkspaceId).FirstOrDefaultAsync();
        if (!workspaceId.HasValue && Guid.TryParse(Request.Headers["X-Workspace-Id"].FirstOrDefault(), out var headerWorkspace)) workspaceId = headerWorkspace;
        workspaceId ??= await _dbContext.Workspaces.OrderByDescending(w => w.IsDefault).Select(w => (Guid?)w.Id).FirstOrDefaultAsync();
        if (!workspaceId.HasValue || !await _dbContext.Workspaces.AnyAsync(w => w.Id == workspaceId)) return Forbid();
        if (request.FolderId.HasValue && !await _dbContext.Folders.AnyAsync(f => f.Id == request.FolderId && f.WorkspaceId == workspaceId)) return BadRequest("Invalid folder.");
        if (request.GlobalErrorWorkflowId.HasValue && !await _dbContext.Workflows.AnyAsync(w => w.Id == request.GlobalErrorWorkflowId && w.WorkspaceId == workspaceId)) return BadRequest("Invalid error workflow.");
        WorkflowEntity? workflow;

        if (request.Id.HasValue)
        {
            // Güncelleme
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
            // Yeni oluştur
            workflow = new WorkflowEntity
            {
                Name = request.Name,
                Description = request.Description,
                TriggerType = request.TriggerType,
                IsActive = request.IsActive
            };
            workflow.TenantId = _user.TenantId;
            workflow.CreatedByUserId = _user.CurrentUserId;
            _dbContext.Workflows.Add(workflow);
        }

        workflow.WorkspaceId = workspaceId;
        workflow.FolderId = request.FolderId;
        workflow.GlobalErrorWorkflowId = request.GlobalErrorWorkflowId;
        bool stateChanged = true;
        JsonDocument? newGraphDoc = null;
        string newGraphJson = string.Empty;

        if (request.Graph != null)
        {
            newGraphJson = JsonSerializer.Serialize(request.Graph);
            newGraphDoc = JsonDocument.Parse(newGraphJson);
            
            // Eğer güncelleme ise önceki JSON ile karşılaştır
            if (request.Id.HasValue && workflow.GraphData != null)
            {
                string oldGraphJson = workflow.GraphData.RootElement.GetRawText();
                if (JsonElement.DeepEquals(workflow.GraphData.RootElement, newGraphDoc.RootElement))
                {
                    stateChanged = false; // JSON aynı, revizyon atmaya gerek yok
                }
            }
            
            workflow.GraphData = newGraphDoc;
        }

        // Önce workflow kaydedilsin (yeni ise Id'si oluşsun)
        await _dbContext.SaveChangesAsync();

        // Eğer grafikte bir değişiklik varsa yeni bir revizyon ekleyelim
        if (stateChanged && request.Graph != null)
        {
            var latestVersionNumber = await _dbContext.WorkflowRevisions
                .Where(r => r.WorkflowId == workflow.Id)
                .Select(r => (int?)r.Version)
                .MaxAsync() ?? 0;

            var revision = new WorkflowRevisionEntity
            {
                WorkflowId = workflow.Id,
                Version = latestVersionNumber + 1,
                WorkflowJson = newGraphDoc,
                CommitMessage = request.Id.HasValue ? "Güncelleme" : "İlk Sürüm"
            };
            _dbContext.WorkflowRevisions.Add(revision);
            await _dbContext.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        _logger.LogInformation("Workflow kaydedildi: {WorkflowId} - {Name}", workflow.Id, workflow.Name);

        return Ok(new WorkflowDto
        {
            Id = workflow.Id,
            WorkspaceId = workflow.WorkspaceId,
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
    [Authorize(Policy = Permissions.WorkflowsEdit)]
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
    [Authorize(Policy = Permissions.WorkflowsExecute)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("workflow-execution")]
    public async Task<ActionResult<ExecutionDto>> Execute(Guid id, [FromBody] StartExecutionRequest? request = null)
    {
        var workflow = await _dbContext.Workflows.FindAsync(id);
        if (workflow == null)
            return NotFound();

        var execution = await _jobs.StartAsync(id, request?.InitialData, stepMode: false);

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
    /// Workflow'u test modunda (adım adım) başlatır
    /// </summary>
    [HttpPost("{id:guid}/test-run")]
    [Authorize(Policy = Permissions.WorkflowsExecute)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("workflow-execution")]
    public async Task<ActionResult<ExecutionDto>> TestRun(Guid id, [FromBody] StartExecutionRequest? request = null)
    {
        var workflow = await _dbContext.Workflows.FindAsync(id);
        if (workflow == null) return NotFound();

        var execution = await _jobs.StartAsync(id, request?.InitialData, stepMode: true);

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
    /// Test modundaki execution'da bir sonraki adıma geçer
    /// </summary>
    [HttpPost("execution/{id:guid}/step")]
    [Authorize(Policy = Permissions.WorkflowsExecute)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("workflow-execution")]
    public async Task<IActionResult> StepExecution(Guid id)
    {
        var execution = await _dbContext.Executions.FindAsync(id);
        if (execution is null) return NotFound();
        if (execution.Status != ExecutionStatus.Paused)
            return BadRequest(new { error = "Only paused executions can be stepped" });
        var token = _pauseTokens.Get(id);
        if (token is null) return Conflict(new { error = "No active worker owns this paused execution" });
        token.IsStepMode = true;
        token.Resume();
        return Ok();
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
    [Authorize(Policy = Permissions.WorkflowsExecute)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("workflow-execution")]
    public async Task<IActionResult> CancelExecution(Guid executionId)
    {
        var execution = await _dbContext.Executions.FindAsync(executionId);
        if (execution == null)
            return NotFound();
        
        if (execution.Status != ExecutionStatus.Running && execution.Status != ExecutionStatus.Queued && execution.Status != ExecutionStatus.Paused)
            return BadRequest(new { error = "Only running or queued executions can be cancelled" });
        
        // Hangfire job'ı iptal et
        if (!string.IsNullOrEmpty(execution.HangfireJobId))
        {
            BackgroundJob.Delete(execution.HangfireJobId);
        }
        
        execution.Status = ExecutionStatus.Cancelled;
        execution.CompletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        
        // Persist cancellation before waking the worker, so it cannot enter the next node.
        _pauseTokens.ResumeExecution(executionId);
        _logger.LogInformation("Execution iptal edildi: {ExecutionId}", executionId);

        return Ok(new { message = "Execution cancelled" });
    }

    /// <summary>
    /// Workflow'u başka klasöre taşı
    /// </summary>
    [HttpPatch("{id:guid}/move")]
    [Authorize(Policy = Permissions.WorkflowsEdit)]
    public async Task<IActionResult> MoveWorkflow(Guid id, [FromBody] MoveWorkflowRequest request)
    {
        var workflow = await _dbContext.Workflows.FindAsync(id);
        if (workflow == null) return NotFound();

        if (request.FolderId.HasValue && !await _dbContext.Folders.AnyAsync(f => f.Id == request.FolderId && f.WorkspaceId == workflow.WorkspaceId)) return BadRequest("Invalid folder.");
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
    [Authorize(Policy = Permissions.WorkflowsExecute)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("workflow-execution")]
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
    [Authorize(Policy = Permissions.WorkflowsExecute)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("workflow-execution")]
    public async Task<IActionResult> ResumeExecution(Guid executionId)
    {
        var execution = await _dbContext.Executions.FindAsync(executionId);
        if (execution == null) return NotFound();

        if (execution.Status != ExecutionStatus.Paused)
            return BadRequest(new { error = "Only paused executions can be resumed" });

        var token = _pauseTokens.Get(executionId);
        if (token is null) return Conflict(new { error = "No active worker owns this paused execution" });
        execution.Status = ExecutionStatus.Running;
        await _dbContext.SaveChangesAsync();

        token.IsStepMode = false;
        _pauseTokens.ResumeExecution(executionId);

        _logger.LogInformation("Execution devam ettiriliyor: {ExecutionId}", executionId);
        return Ok(new { message = "Execution resuming" });
    }

    /// <summary>
    /// Execution'ı kaldığı Node'dan devam ettir (Saga/Checkpoint)
    /// </summary>
    [HttpPost("execution/{executionId:guid}/resume-from-node/{nodeId:guid}")]
    [Authorize(Policy = Permissions.WorkflowsExecute)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("workflow-execution")]
    public IActionResult ResumeExecutionFromNode(Guid executionId, Guid nodeId)
    {
        return StatusCode(501, new { error = "Checkpoint replay is not supported in the core release." });
    }

    /// <summary>
    /// Execution snapshot al
    /// </summary>
    [HttpPost("execution/{executionId:guid}/snapshot")]
    [Authorize(Policy = Permissions.WorkflowsExecute)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("workflow-execution")]
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
    [Authorize(Policy = Permissions.WorkflowsExecute)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("workflow-execution")]
    public ActionResult<ExecutionDto> RestoreFromSnapshot(Guid executionId, Guid snapshotId)
    {
        return StatusCode(501, new { error = "Checkpoint replay is not supported in the core release." });
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

    /// <summary>
    /// Workflow'un tüm revizyon geçmişini listele
    /// </summary>
    [HttpGet("{id:guid}/revisions")]
    public async Task<ActionResult<List<WorkflowRevisionDto>>> GetRevisions(Guid id)
    {
        var workflow = await _dbContext.Workflows.FindAsync(id);
        if (workflow == null) return NotFound();

        var revisions = await _dbContext.WorkflowRevisions
            .Where(r => r.WorkflowId == id)
            .OrderByDescending(r => r.Version)
            .Select(r => new WorkflowRevisionDto
            {
                Id = r.Id,
                WorkflowId = r.WorkflowId,
                Version = r.Version,
                CommitMessage = r.CommitMessage,
                CreatedAt = r.CreatedAt
                // Graph JSON parselamıyoruz ki liste ufak olsun
            })
            .ToListAsync();

        return Ok(revisions);
    }

    /// <summary>
    /// Workflow'un belirli bir revizyon detayını (grafiğini) getir
    /// </summary>
    [HttpGet("{id:guid}/revisions/{version:int}")]
    public async Task<ActionResult<WorkflowRevisionDto>> GetRevision(Guid id, int version)
    {
        var revision = await _dbContext.WorkflowRevisions
            .FirstOrDefaultAsync(r => r.WorkflowId == id && r.Version == version);

        if (revision == null) return NotFound();

        var dto = new WorkflowRevisionDto
        {
            Id = revision.Id,
            WorkflowId = revision.WorkflowId,
            Version = revision.Version,
            CommitMessage = revision.CommitMessage,
            CreatedAt = revision.CreatedAt
        };

        if (revision.WorkflowJson != null)
        {
            dto.Graph = JsonSerializer.Deserialize<WorkflowGraphDto>(
                revision.WorkflowJson.RootElement.GetRawText());
        }

        return Ok(dto);
    }

    /// <summary>
    /// Workflow içindeki tek bir node'u test/debug için çalıştır
    /// </summary>
    [HttpPost("{id:guid}/execute-node")]
    [Authorize(Policy = Permissions.WorkflowsExecute)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("workflow-execution")]
    public async Task<ActionResult<ExecuteSingleNodeResponse>> ExecuteSingleNode(Guid id, [FromBody] ExecuteSingleNodeRequest request)
    {
        var workflow = await _dbContext.Workflows.FindAsync(id);
        if (workflow == null) return NotFound();

        request.WorkflowId = id;
        var result = await _singleNodeSvc.ExecuteNodeAsync(request);

        return Ok(result);
    }

    /// <summary>
    /// Kaydedilmemiş bir workflow için (Wizard vb. içerisinde) tek node'u test et
    /// </summary>
    [HttpPost("execute-node-transient")]
    [Authorize(Policy = Permissions.WorkflowsExecute)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("workflow-execution")]
    public async Task<ActionResult<ExecuteSingleNodeResponse>> ExecuteSingleNodeTransient([FromBody] ExecuteSingleNodeRequest request)
    {
        var result = await _singleNodeSvc.ExecuteNodeAsync(request);
        return Ok(result);
    }
}


