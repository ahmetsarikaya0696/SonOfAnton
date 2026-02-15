using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;

namespace SonOfAnton.Backend.Services.Snapshots;

/// <summary>
/// Execution snapshot yönetim servisi
/// </summary>
public class SnapshotService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SnapshotService> _logger;

    public SnapshotService(IServiceProvider serviceProvider, ILogger<SnapshotService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Manuel snapshot al
    /// </summary>
    public async Task<SnapshotDto?> TakeAsync(Guid executionId, string? label = null, CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

        var execution = await db.Executions.FindAsync([executionId], ct);
        if (execution == null) return null;

        var snapshot = new ExecutionSnapshotEntity
        {
            ExecutionId = executionId,
            Label = label,
            MemorySnapshot = execution.Memory,
            AtNodeId = execution.CurrentNodeId,
            IsAutomatic = false
        };

        db.ExecutionSnapshots.Add(snapshot);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Snapshot alındı: {SnapshotId} -> Execution: {ExecutionId}", snapshot.Id, executionId);

        return ToDto(snapshot);
    }

    /// <summary>
    /// Otomatik snapshot al (hata oluştuğunda WorkflowRunner tarafından çağrılır)
    /// </summary>
    public async Task TakeAutomaticAsync(
        Guid executionId, Guid? atNodeId,
        Dictionary<string, object?> memory,
        CancellationToken ct = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

            var snapshot = new ExecutionSnapshotEntity
            {
                ExecutionId = executionId,
                Label = $"Auto-snapshot at error (Node: {atNodeId})",
                MemorySnapshot = JsonDocument.Parse(JsonSerializer.Serialize(memory)),
                AtNodeId = atNodeId,
                IsAutomatic = true
            };

            db.ExecutionSnapshots.Add(snapshot);
            await db.SaveChangesAsync(ct);

            _logger.LogDebug("Otomatik snapshot alındı: {ExecutionId}", executionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Otomatik snapshot alınamadı: {ExecutionId}", executionId);
        }
    }

    /// <summary>
    /// Snapshot listesi
    /// </summary>
    public async Task<List<SnapshotDto>> ListAsync(Guid executionId, CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

        return await db.ExecutionSnapshots
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
            .ToListAsync(ct);
    }

    /// <summary>
    /// Snapshot sil
    /// </summary>
    public async Task<bool> DeleteAsync(Guid snapshotId, CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

        var snapshot = await db.ExecutionSnapshots.FindAsync([snapshotId], ct);
        if (snapshot == null) return false;

        db.ExecutionSnapshots.Remove(snapshot);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static SnapshotDto ToDto(ExecutionSnapshotEntity entity) => new()
    {
        Id = entity.Id,
        ExecutionId = entity.ExecutionId,
        Label = entity.Label,
        AtNodeId = entity.AtNodeId,
        IsAutomatic = entity.IsAutomatic,
        CreatedAt = entity.CreatedAt
    };
}
