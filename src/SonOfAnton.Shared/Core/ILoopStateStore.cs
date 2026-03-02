using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Shared.Core;

/// <summary>
/// Ralph döngü durumlarının PostgreSQL + MemoryCache hybrid depolanması için arayüz.
/// Okuma işlemleri önce bellekten dener, yazma işlemleri hem belleğe hem DB'ye yazar.
/// </summary>
public interface ILoopStateStore
{
    /// <summary>
    /// Döngü durumunu okur. Önce cache'den, yoksa DB'den çeker.
    /// </summary>
    Task<LoopExecutionStateDto?> GetStateAsync(
        string loopId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Yeni döngü durumunu kaydeder (DB + cache).
    /// </summary>
    Task<LoopExecutionStateDto> SaveStateAsync(
        string loopId,
        LoopState state,
        int maxIterations,
        string? contextJson = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mevcut döngü durumunu günceller (iterasyon, state, heartbeat vb.).
    /// </summary>
    Task<LoopExecutionStateDto> UpdateStateAsync(
        string loopId,
        LoopState state,
        int currentIteration,
        string? contextJson = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Döngüyü tamamlandı olarak işaretler.
    /// </summary>
    Task CompleteLoopAsync(
        string loopId,
        LoopExitReason reason,
        string? exitMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tüm aktif (Running veya Paused) döngüleri listeler.
    /// </summary>
    Task<IReadOnlyList<LoopExecutionStateDto>> GetActiveLoopsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Döngünün heartbeat zamanını günceller.
    /// </summary>
    Task UpdateHeartbeatAsync(
        string loopId,
        CancellationToken cancellationToken = default);
}
