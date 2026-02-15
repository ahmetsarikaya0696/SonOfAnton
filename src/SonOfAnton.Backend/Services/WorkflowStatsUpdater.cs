using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;

namespace SonOfAnton.Backend.Services;

/// <summary>
/// Workflow istatistiklerini atomik olarak güncelleyen servis
/// </summary>
public class WorkflowStatsUpdater
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkflowStatsUpdater> _logger;

    public WorkflowStatsUpdater(IServiceProvider serviceProvider, ILogger<WorkflowStatsUpdater> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Execution tamamlandığında çağrılır — istatistikleri günceller
    /// </summary>
    public async Task RecordExecutionAsync(Guid workflowId, bool success, double durationMs, CancellationToken ct = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

            var workflow = await db.Workflows.FindAsync([workflowId], ct);
            if (workflow == null) return;

            workflow.RunCount++;
            if (success)
                workflow.SuccessCount++;
            else
                workflow.FailureCount++;

            // Running average
            workflow.AvgDurationMs = workflow.RunCount == 1
                ? durationMs
                : (workflow.AvgDurationMs * (workflow.RunCount - 1) + durationMs) / workflow.RunCount;

            workflow.LastRunAt = DateTime.UtcNow;
            workflow.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            _logger.LogDebug("Workflow stats güncellendi: {WorkflowId}, RunCount={RunCount}", workflowId, workflow.RunCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow stats güncellenemedi: {WorkflowId}", workflowId);
        }
    }
}
