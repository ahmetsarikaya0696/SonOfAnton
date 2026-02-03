using Hangfire;
using SonOfAnton.Backend.Data;
using SonOfAnton.Backend.Engine;
using System.Text.Json;

namespace SonOfAnton.Backend.Services;

/// <summary>
/// Hangfire ile workflow job yönetimi
/// </summary>
public class WorkflowJobService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkflowJobService> _logger;
    private readonly SseEventService _sseEventService;
    
    public WorkflowJobService(
        IServiceProvider serviceProvider,
        ILogger<WorkflowJobService> logger,
        SseEventService sseEventService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _sseEventService = sseEventService;
    }
    
    /// <summary>
    /// Workflow'u hemen çalıştırır (fire-and-forget)
    /// </summary>
    public string EnqueueWorkflow(Guid workflowId, object? triggerData = null)
    {
        var executionId = Guid.NewGuid();
        
        // SSE channel oluştur
        _sseEventService.CreateChannel(executionId);
        
        var jobId = BackgroundJob.Enqueue(() => ExecuteWorkflowAsync(
            workflowId, 
            executionId,
            JsonSerializer.Serialize(triggerData),
            CancellationToken.None));
        
        _logger.LogInformation(
            "Workflow enqueue edildi: {WorkflowId}, ExecutionId: {ExecutionId}, JobId: {JobId}", 
            workflowId, executionId, jobId);
        
        return executionId.ToString();
    }
    
    /// <summary>
    /// Workflow'u belirtilen süre sonra çalıştırır
    /// </summary>
    public string ScheduleWorkflow(Guid workflowId, TimeSpan delay, object? triggerData = null)
    {
        var executionId = Guid.NewGuid();
        
        var jobId = BackgroundJob.Schedule(() => ExecuteWorkflowAsync(
            workflowId,
            executionId,
            JsonSerializer.Serialize(triggerData),
            CancellationToken.None), delay);
        
        _logger.LogInformation(
            "Workflow zamanlandı: {WorkflowId}, Delay: {Delay}, JobId: {JobId}", 
            workflowId, delay, jobId);
        
        return jobId;
    }
    
    /// <summary>
    /// Workflow'u cron ifadesiyle tekrarlı çalıştırır
    /// </summary>
    public void AddRecurringWorkflow(Guid workflowId, string cronExpression, string? jobName = null)
    {
        jobName ??= $"workflow_{workflowId}";
        
        RecurringJob.AddOrUpdate(
            jobName,
            () => ExecuteWorkflowAsync(
                workflowId,
                Guid.NewGuid(),
                null,
                CancellationToken.None),
            cronExpression);
        
        _logger.LogInformation(
            "Recurring workflow eklendi: {WorkflowId}, Cron: {Cron}, JobName: {JobName}", 
            workflowId, cronExpression, jobName);
    }
    
    /// <summary>
    /// Recurring job'ı kaldırır
    /// </summary>
    public void RemoveRecurringWorkflow(string jobName)
    {
        RecurringJob.RemoveIfExists(jobName);
        _logger.LogInformation("Recurring workflow kaldırıldı: {JobName}", jobName);
    }
    
    /// <summary>
    /// Workflow execution işlemi (Hangfire tarafından çağrılır)
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 300 })]
    [Queue("workflows")]
    public async Task ExecuteWorkflowAsync(
        Guid workflowId,
        Guid executionId,
        string? triggerDataJson,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        _logger.LogInformation(
            "Workflow execution başlıyor: {WorkflowId}, ExecutionId: {ExecutionId}", 
            workflowId, executionId);
        
        try
        {
            // SSE channel oluştur (eğer yoksa)
            _sseEventService.CreateChannel(executionId);
            
            // Scoped servisler al
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();
            var workflowRunner = scope.ServiceProvider.GetRequiredService<WorkflowRunner>();
            
            // Execution kaydı oluştur
            var execution = new SonOfAnton.Shared.Entities.ExecutionEntity
            {
                Id = executionId,
                WorkflowId = workflowId,
                Status = SonOfAnton.Shared.Enums.ExecutionStatus.Queued
            };
            
            // Trigger data'yı memory'e ekle
            if (!string.IsNullOrEmpty(triggerDataJson))
            {
                execution.Memory = JsonDocument.Parse(triggerDataJson);
            }
            
            dbContext.Executions.Add(execution);
            await dbContext.SaveChangesAsync(cancellationToken);
            
            // Mevcut workflow runner API'sini kullan
            await workflowRunner.RunAsync(executionId, cancellationToken);
            
            sw.Stop();
            
            // Execution sonucunu al
            var completedExecution = await dbContext.Executions.FindAsync([executionId], cancellationToken);
            var success = completedExecution?.Status == SonOfAnton.Shared.Enums.ExecutionStatus.Completed;
            var error = completedExecution?.ErrorMessage;
            
            // SSE event
            await _sseEventService.PublishWorkflowCompletedAsync(
                executionId,
                success,
                error,
                sw.Elapsed,
                nodesExecuted: 0); // Node count tracking ayrı yapılacak
            
            _logger.LogInformation(
                "Workflow execution tamamlandı: {WorkflowId}, ExecutionId: {ExecutionId}, Success: {Success}, Duration: {Duration}ms", 
                workflowId, executionId, success, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            
            _logger.LogError(ex,
                "Workflow execution hatası: {WorkflowId}, ExecutionId: {ExecutionId}", 
                workflowId, executionId);
            
            // Hata event'i
            await _sseEventService.PublishWorkflowCompletedAsync(
                executionId,
                success: false,
                error: ex.Message,
                sw.Elapsed,
                nodesExecuted: 0);
            
            throw;
        }
        finally
        {
            // SSE channel'ı kapat (biraz gecikmeyle, client'ların alması için)
            _ = Task.Delay(5000, CancellationToken.None).ContinueWith(_ => 
            {
                _sseEventService.CompleteChannel(executionId);
            });
        }
    }
    
    /// <summary>
    /// Çalışan job'ı iptal eder
    /// </summary>
    public bool CancelJob(string jobId)
    {
        var result = BackgroundJob.Delete(jobId);
        if (result)
        {
            _logger.LogInformation("Job iptal edildi: {JobId}", jobId);
        }
        return result;
    }
}

/// <summary>
/// Workflow execution sonucu
/// </summary>
public class WorkflowExecutionResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int NodesExecuted { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<Guid, object?> NodeOutputs { get; set; } = new();
}
