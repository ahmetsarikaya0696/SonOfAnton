using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Backend.Controllers;

/// <summary>
/// Workflow execution (çalıştırma) yönetimi.
/// 
/// Bir workflow çalıştırıldığında bir "Execution" kaydı oluşur.
/// Bu controller ile execution'ları listeleyebilir, detaylarını görebilir,
/// loglarını okuyabilir ve yönetebilirsiniz.
/// 
/// <para><b>Execution Yaşam Döngüsü:</b></para>
/// <para>Queued → Running → Completed/Failed/Cancelled</para>
/// 
/// <para><b>Zamanlama Seçenekleri:</b></para>
/// <list type="bullet">
///   <item><description><b>Hemen çalıştır</b>: POST /api/executions/run/{workflowId}</description></item>
///   <item><description><b>Gecikmeli</b>: POST /api/executions/schedule/{workflowId}?delaySeconds=60</description></item>
///   <item><description><b>Tekrarlayan</b>: POST /api/executions/recurring/{workflowId}?cron=0 * * * *</description></item>
/// </list>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExecutionsController : ControllerBase
{
    private readonly SonOfAntonDbContext _dbContext;
    private readonly WorkflowJobService _jobService;

    public ExecutionsController(SonOfAntonDbContext dbContext, WorkflowJobService jobService)
    {
        _dbContext = dbContext;
        _jobService = jobService;
    }

    /// <summary>
    /// Tüm execution'ları listele
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
    /// Execution detayını getir
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
    /// Execution log'larını getir
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
    /// Execution'ı iptal et
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

        execution.Status = ExecutionStatus.Cancelled;
        execution.CompletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return Ok();
    }
    
    /// <summary>
    /// Workflow'u hemen çalıştırır (Hangfire job olarak)
    /// </summary>
    [HttpPost("run/{workflowId:guid}")]
    public ActionResult RunWorkflow(Guid workflowId, [FromBody] object? triggerData = null)
    {
        var executionId = _jobService.EnqueueWorkflow(workflowId, triggerData);
        return Ok(new { executionId, message = "Workflow kuyruğa eklendi" });
    }
    
    /// <summary>
    /// Workflow'u zamanlar
    /// </summary>
    [HttpPost("schedule/{workflowId:guid}")]
    public ActionResult ScheduleWorkflow(Guid workflowId, [FromQuery] int delaySeconds, [FromBody] object? triggerData = null)
    {
        var jobId = _jobService.ScheduleWorkflow(workflowId, TimeSpan.FromSeconds(delaySeconds), triggerData);
        return Ok(new { jobId, message = $"{delaySeconds} saniye sonra çalıştırılacak" });
    }
    
    /// <summary>
    /// Recurring workflow ekler
    /// </summary>
    [HttpPost("recurring/{workflowId:guid}")]
    public ActionResult AddRecurring(Guid workflowId, [FromQuery] string cron, [FromQuery] string? jobName = null)
    {
        var result = _jobService.AddRecurringWorkflow(workflowId, cron, jobName);
        if (!result.IsValid)
        {
            return BadRequest(new { error = result.Error });
        }
        return Ok(new { message = "Recurring workflow eklendi", description = result.Description });
    }
    
    /// <summary>
    /// Recurring workflow kaldırır
    /// </summary>
    [HttpDelete("recurring/{jobName}")]
    public ActionResult RemoveRecurring(string jobName)
    {
        _jobService.RemoveRecurringWorkflow(jobName);
        return Ok(new { message = "Recurring workflow kaldırıldı" });
    }
    
    /// <summary>
    /// Cron ifadesini doğrular
    /// </summary>
    [HttpPost("validate-cron")]
    public ActionResult ValidateCron([FromBody] CronValidationRequest request)
    {
        var result = WorkflowJobService.ValidateCronExpression(request.CronExpression);
        return Ok(result);
    }
    
    /// <summary>
    /// Job bilgisini getirir
    /// </summary>
    [HttpGet("job/{jobId}")]
    public ActionResult GetJobInfo(string jobId)
    {
        var info = _jobService.GetJobInfo(jobId);
        if (info == null)
            return NotFound();
        return Ok(info);
    }
}

public record CronValidationRequest
{
    public string CronExpression { get; init; } = string.Empty;
}

