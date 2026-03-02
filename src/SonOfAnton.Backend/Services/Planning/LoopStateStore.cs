using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Core;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Backend.Services.Planning;

/// <summary>
/// Ralph döngü durumlarının PostgreSQL + MemoryCache hybrid depolanması.
/// Okuma: Cache → DB fallback. Yazma: DB + Cache write-through.
/// </summary>
public class LoopStateStore : ILoopStateStore
{
    private readonly SonOfAntonDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LoopStateStore> _logger;

    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);
    private const string CacheKeyPrefix = "loop_state:";

    public LoopStateStore(
        SonOfAntonDbContext dbContext,
        IMemoryCache cache,
        ILogger<LoopStateStore> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LoopExecutionStateDto?> GetStateAsync(
        string loopId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyPrefix + loopId;

        // Önce cache'den dene
        if (_cache.TryGetValue(cacheKey, out LoopExecutionStateDto? cached))
        {
            _logger.LogDebug("Döngü durumu cache'den okundu: {LoopId}", loopId);
            return cached;
        }

        // Cache'de yoksa DB'den oku
        var entity = await _dbContext.LoopExecutionStates
            .FirstOrDefaultAsync(s => s.LoopId == loopId, cancellationToken);

        if (entity == null) return null;

        var dto = MapToDto(entity);

        // Cache'e yaz
        _cache.Set(cacheKey, dto, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheExpiration,
            Size = 1
        });

        _logger.LogDebug("Döngü durumu DB'den okundu ve cache'lendi: {LoopId}", loopId);
        return dto;
    }

    /// <inheritdoc />
    public async Task<LoopExecutionStateDto> SaveStateAsync(
        string loopId,
        LoopState state,
        int maxIterations,
        string? contextJson = null,
        CancellationToken cancellationToken = default)
    {
        var entity = new LoopExecutionStateEntity
        {
            Id = Guid.NewGuid(),
            LoopId = loopId,
            State = state,
            CurrentIteration = 0,
            MaxIterations = maxIterations,
            ContextJson = contextJson,
            StartedAt = DateTime.UtcNow,
            LastHeartbeatAt = DateTime.UtcNow
        };

        _dbContext.LoopExecutionStates.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = MapToDto(entity);
        InvalidateCache(loopId);
        SetCache(loopId, dto);

        _logger.LogInformation(
            "Yeni döngü durumu oluşturuldu: {LoopId}, State={State}, MaxIterations={Max}",
            loopId, state, maxIterations);

        return dto;
    }

    /// <inheritdoc />
    public async Task<LoopExecutionStateDto> UpdateStateAsync(
        string loopId,
        LoopState state,
        int currentIteration,
        string? contextJson = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.LoopExecutionStates
            .FirstOrDefaultAsync(s => s.LoopId == loopId, cancellationToken)
            ?? throw new InvalidOperationException($"Döngü bulunamadı: {loopId}");

        entity.State = state;
        entity.CurrentIteration = currentIteration;
        entity.LastHeartbeatAt = DateTime.UtcNow;

        if (contextJson != null)
        {
            entity.ContextJson = contextJson;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = MapToDto(entity);
        SetCache(loopId, dto);

        _logger.LogDebug(
            "Döngü durumu güncellendi: {LoopId}, State={State}, Iteration={Iteration}",
            loopId, state, currentIteration);

        return dto;
    }

    /// <inheritdoc />
    public async Task CompleteLoopAsync(
        string loopId,
        LoopExitReason reason,
        string? exitMessage = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.LoopExecutionStates
            .FirstOrDefaultAsync(s => s.LoopId == loopId, cancellationToken)
            ?? throw new InvalidOperationException($"Döngü bulunamadı: {loopId}");

        entity.State = reason == LoopExitReason.Error ? LoopState.Failed
            : reason == LoopExitReason.ManualStop ? LoopState.Cancelled
            : LoopState.Completed;
        entity.ExitReason = reason;
        entity.ExitMessage = exitMessage;
        entity.CompletedAt = DateTime.UtcNow;
        entity.LastHeartbeatAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCache(loopId);

        _logger.LogInformation(
            "Döngü tamamlandı: {LoopId}, Reason={Reason}, Message={Message}",
            loopId, reason, exitMessage);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LoopExecutionStateDto>> GetActiveLoopsAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.LoopExecutionStates
            .Where(s => s.State == LoopState.Running || s.State == LoopState.Paused)
            .OrderBy(s => s.StartedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task UpdateHeartbeatAsync(
        string loopId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.LoopExecutionStates
            .FirstOrDefaultAsync(s => s.LoopId == loopId, cancellationToken);

        if (entity == null) return;

        entity.LastHeartbeatAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Döngü heartbeat güncellendi: {LoopId}", loopId);
    }

    // ─── Cache Yardımcıları ───────────────────────────────────────

    private void SetCache(string loopId, LoopExecutionStateDto dto)
    {
        var cacheKey = CacheKeyPrefix + loopId;
        _cache.Set(cacheKey, dto, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheExpiration,
            Size = 1
        });
    }

    private void InvalidateCache(string loopId)
    {
        var cacheKey = CacheKeyPrefix + loopId;
        _cache.Remove(cacheKey);
    }

    // ─── Mapping ────────────────────────────────────────────────────

    private static LoopExecutionStateDto MapToDto(LoopExecutionStateEntity entity)
    {
        return new LoopExecutionStateDto(
            entity.Id,
            entity.LoopId,
            entity.State,
            entity.CurrentIteration,
            entity.MaxIterations,
            entity.ContextJson,
            entity.ExitReason,
            entity.ExitMessage,
            entity.StartedAt,
            entity.LastHeartbeatAt,
            entity.CompletedAt);
    }
}
