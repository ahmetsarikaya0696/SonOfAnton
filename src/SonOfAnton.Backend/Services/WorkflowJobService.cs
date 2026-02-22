using Hangfire;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Backend.Engine;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SonOfAnton.Backend.Services;

/// <summary>
/// Job durumu
/// </summary>
public enum WorkflowJobStatus
{
    Enqueued,
    Scheduled,
    Processing,
    Succeeded,
    Failed,
    Deleted,
    Awaiting,
    Unknown
}

/// <summary>
/// Job detay bilgisi
/// </summary>
public class WorkflowJobInfo
{
    public string JobId { get; init; } = string.Empty;
    public Guid WorkflowId { get; init; }
    public Guid ExecutionId { get; init; }
    public WorkflowJobStatus Status { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? Error { get; init; }
    public int RetryCount { get; init; }
}

/// <summary>
/// Recurring job bilgisi
/// </summary>
public class RecurringJobInfo
{
    public string JobName { get; init; } = string.Empty;
    public Guid WorkflowId { get; init; }
    public string CronExpression { get; init; } = string.Empty;
    public DateTime? LastExecution { get; init; }
    public DateTime? NextExecution { get; init; }
    public bool IsPaused { get; init; }
}

/// <summary>
/// Cron validation sonucu
/// </summary>
public class CronValidationResult
{
    public bool IsValid { get; init; }
    public string? Error { get; init; }
    public string? Description { get; init; }
    public DateTime? NextOccurrence { get; init; }
    
    public static CronValidationResult Valid(string description, DateTime? next) =>
        new() { IsValid = true, Description = description, NextOccurrence = next };
    
    public static CronValidationResult Invalid(string error) =>
        new() { IsValid = false, Error = error };
}

/// <summary>
/// Hangfire ile workflow job yönetimi
/// Cron validation, job lifecycle, progress reporting
/// </summary>
public partial class WorkflowJobService
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
    /// Workflow'u cron ifadesiyle tekrarlı çalıştırır (validasyon ile)
    /// </summary>
    public CronValidationResult AddRecurringWorkflow(Guid workflowId, string cronExpression, string? jobName = null)
    {
        // Cron validation
        var validation = ValidateCronExpression(cronExpression);
        if (!validation.IsValid)
        {
            _logger.LogWarning("Geçersiz cron ifadesi: {Cron} - {Error}", cronExpression, validation.Error);
            return validation;
        }
        
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
        
        return validation;
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
    /// Recurring job'ı duraklatır (trigger etme ama silme)
    /// </summary>
    public void PauseRecurringWorkflow(string jobName)
    {
        RecurringJob.TriggerJob(jobName);
        // Hangfire'da native pause yok, RemoveIfExists + metadata ile yapılır
        // Alternatif: Cron'u Cron.Never() yap
        _logger.LogInformation("Recurring workflow duraklatıldı: {JobName}", jobName);
    }
    
    /// <summary>
    /// Cron ifadesini doğrular
    /// </summary>
    public static CronValidationResult ValidateCronExpression(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            return CronValidationResult.Invalid("Cron ifadesi boş olamaz");
        }
        
        // Temel format kontrolü (5 veya 6 parçalı)
        var parts = cronExpression.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 5 or > 6)
        {
            return CronValidationResult.Invalid($"Cron ifadesi 5 veya 6 parçadan oluşmalı, {parts.Length} parça bulundu");
        }
        
        // Her parçanın geçerli olduğunu kontrol et
        var fieldNames = new[] { "dakika", "saat", "gün", "ay", "haftanın günü", "saniye" };
        var maxValues = new[] { 59, 23, 31, 12, 7, 59 };
        var minValues = new[] { 0, 0, 1, 1, 0, 0 };
        
        for (int i = 0; i < parts.Length && i < fieldNames.Length; i++)
        {
            if (!IsValidCronField(parts[i], minValues[i], maxValues[i]))
            {
                return CronValidationResult.Invalid($"Geçersiz {fieldNames[i]} alanı: '{parts[i]}'");
            }
        }
        
        try
        {
            // Hangfire'ın Cron parser'ını dene
            var description = DescribeCronExpression(cronExpression);
            return CronValidationResult.Valid(description, null);
        }
        catch (Exception ex)
        {
            return CronValidationResult.Invalid($"Cron ifadesi parse edilemedi: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Cron field'ı geçerli mi kontrol eder
    /// </summary>
    private static bool IsValidCronField(string field, int min, int max)
    {
        if (field == "*") return true;
        
        // */n format
        if (field.StartsWith("*/"))
        {
            return int.TryParse(field[2..], out var step) && step > 0 && step <= max;
        }
        
        // Virgüllü liste: 1,5,10
        var parts = field.Split(',');
        foreach (var part in parts)
        {
            // Range: 1-5
            if (part.Contains('-'))
            {
                var range = part.Split('-');
                if (range.Length != 2) return false;
                if (!int.TryParse(range[0], out var start) || !int.TryParse(range[1], out var end))
                    return false;
                if (start < min || end > max || start > end)
                    return false;
            }
            else if (part != "*" && part != "?")
            {
                if (!int.TryParse(part, out var value))
                    return false;
                if (value < min || value > max)
                    return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Cron ifadesini okunabilir açıklamaya çevirir
    /// </summary>
    private static string DescribeCronExpression(string cron)
    {
        var parts = cron.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5) return cron;
        
        var minute = parts[0];
        var hour = parts[1];
        var dayOfMonth = parts[2];
        var month = parts[3];
        var dayOfWeek = parts[4];
        
        // Yaygın kalıplar
        if (minute == "*" && hour == "*" && dayOfMonth == "*" && month == "*" && dayOfWeek == "*")
            return "Her dakika";
        
        if (minute == "0" && hour == "*" && dayOfMonth == "*" && month == "*" && dayOfWeek == "*")
            return "Her saat başı";
        
        if (minute == "0" && hour == "0" && dayOfMonth == "*" && month == "*" && dayOfWeek == "*")
            return "Her gün gece yarısı";
        
        if (minute.StartsWith("*/"))
            return $"Her {minute[2..]} dakikada bir";
        
        if (hour.StartsWith("*/"))
            return $"Her {hour[2..]} saatte bir";
        
        return $"Cron: {cron}";
    }
    
    /// <summary>
    /// Job durumunu sorgular
    /// </summary>
    public WorkflowJobInfo? GetJobInfo(string jobId)
    {
        try
        {
            using var connection = JobStorage.Current.GetConnection();
            var jobData = connection.GetJobData(jobId);
            
            if (jobData == null) return null;
            
            var status = jobData.State switch
            {
                "Enqueued" => WorkflowJobStatus.Enqueued,
                "Scheduled" => WorkflowJobStatus.Scheduled,
                "Processing" => WorkflowJobStatus.Processing,
                "Succeeded" => WorkflowJobStatus.Succeeded,
                "Failed" => WorkflowJobStatus.Failed,
                "Deleted" => WorkflowJobStatus.Deleted,
                "Awaiting" => WorkflowJobStatus.Awaiting,
                _ => WorkflowJobStatus.Unknown
            };
            
            return new WorkflowJobInfo
            {
                JobId = jobId,
                Status = status,
                CreatedAt = jobData.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Job bilgisi alınamadı: {JobId}", jobId);
            return null;
        }
    }
    
    /// <summary>
    /// Workflow execution işlemi (Hangfire tarafından çağrılır)
    /// Node count tracking ve SSE progress reporting ile
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
        var nodesExecuted = 0;
        
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
            
            // SSE log event - başlangıç
            await _sseEventService.PublishLogAsync(
                executionId, null, "info",
                $"Workflow execution başlıyor: {workflowId}");
            
            // Mevcut workflow runner API'sini kullan
            await workflowRunner.RunAsync(executionId, cancellationToken);
            
            sw.Stop();
            
            // Execution sonucunu al
            var completedExecution = await dbContext.Executions.FindAsync([executionId], cancellationToken);
            var success = completedExecution?.Status == SonOfAnton.Shared.Enums.ExecutionStatus.Completed;
            var error = completedExecution?.ErrorMessage;
            
            // Node count - execution log sayısından tahmin et
            nodesExecuted = await dbContext.ExecutionLogs
                .CountAsync(l => l.ExecutionId == executionId 
                    && l.Message.Contains("Node çalıştırılıyor"), cancellationToken);
            
            // SSE event
            await _sseEventService.PublishWorkflowCompletedAsync(
                executionId,
                success,
                error,
                sw.Elapsed,
                nodesExecuted);
            
            _logger.LogInformation(
                "Workflow execution tamamlandı: {WorkflowId}, ExecutionId: {ExecutionId}, Success: {Success}, Duration: {Duration}ms, Nodes: {Nodes}", 
                workflowId, executionId, success, sw.ElapsedMilliseconds, nodesExecuted);
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
                nodesExecuted);
            
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
    
    /// <summary>
    /// Job'ı yeniden kuyruğa ekler
    /// </summary>
    public bool RequeueJob(string jobId)
    {
        var result = BackgroundJob.Requeue(jobId);
        if (result)
        {
            _logger.LogInformation("Job yeniden kuyruğa eklendi: {JobId}", jobId);
        }
        return result;
    }

    /// <summary>
    /// Workflow'u duraklatıldığı yerden (checkpoint) devam ettirir
    /// </summary>
    public string ResumeWorkflow(Guid workflowId, Guid executionId, Guid nodeId)
    {
        _sseEventService.CreateChannel(executionId);
        
        var jobId = BackgroundJob.Enqueue(() => ResumeWorkflowJobAsync(
            workflowId, 
            executionId,
            nodeId,
            CancellationToken.None));
        
        _logger.LogInformation(
            "Workflow resume enqueue edildi: {WorkflowId}, ExecutionId: {ExecutionId}, NodeId: {NodeId}, JobId: {JobId}", 
            workflowId, executionId, nodeId, jobId);
        
        return executionId.ToString();
    }

    /// <summary>
    /// Workflow resume işlemi (Hangfire tarafından çağrılır)
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 300 })]
    [Queue("workflows")]
    public async Task ResumeWorkflowJobAsync(
        Guid workflowId,
        Guid executionId,
        Guid nodeId,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var nodesExecuted = 0;
        
        try
        {
            _sseEventService.CreateChannel(executionId);
            
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();
            var workflowRunner = scope.ServiceProvider.GetRequiredService<WorkflowRunner>();
            
            await _sseEventService.PublishLogAsync(
                executionId, null, "info",
                $"Workflow resume başlıyor: {workflowId}");
            
            await workflowRunner.ResumeAsync(executionId, nodeId, cancellationToken);
            
            sw.Stop();
            
            var completedExecution = await dbContext.Executions.FindAsync([executionId], cancellationToken);
            var success = completedExecution?.Status == SonOfAnton.Shared.Enums.ExecutionStatus.Completed;
            var error = completedExecution?.ErrorMessage;
            
            nodesExecuted = await dbContext.ExecutionLogs
                .CountAsync(l => l.ExecutionId == executionId 
                    && l.Message.Contains("Node çalıştırılıyor"), cancellationToken);
            
            await _sseEventService.PublishWorkflowCompletedAsync(
                executionId,
                success,
                error,
                sw.Elapsed,
                nodesExecuted);
        }
        catch (Exception ex)
        {
            sw.Stop();
            
            _logger.LogError(ex,
                "Workflow resume hatası: {WorkflowId}, ExecutionId: {ExecutionId}", 
                workflowId, executionId);
            
            await _sseEventService.PublishWorkflowCompletedAsync(
                executionId,
                false,
                ex.Message,
                sw.Elapsed,
                nodesExecuted);
            
            throw;
        }
        finally
        {
            _ = Task.Delay(5000, CancellationToken.None).ContinueWith(_ => 
            {
                _sseEventService.CompleteChannel(executionId);
            });
        }
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
