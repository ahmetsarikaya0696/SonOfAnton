using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Shared.DTOs;

/// <summary>
/// Planlama belgesi DTO'su
/// </summary>
public record PlanningDocumentDto(
    Guid Id,
    string AgentId,
    PlanFileType FileType,
    string Content,
    int Version,
    string? FilePath,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Döngü yürütme durumu DTO'su
/// </summary>
public record LoopExecutionStateDto(
    Guid Id,
    string LoopId,
    LoopState State,
    int CurrentIteration,
    int MaxIterations,
    string? ContextJson,
    LoopExitReason? ExitReason,
    string? ExitMessage,
    DateTime StartedAt,
    DateTime LastHeartbeatAt,
    DateTime? CompletedAt);

/// <summary>
/// Döngü durum özeti (dashboard/monitoring için)
/// </summary>
public record LoopStatusDto(
    string LoopId,
    LoopState State,
    int CurrentIteration,
    int MaxIterations,
    double ProgressPercentage,
    TimeSpan Elapsed,
    string? ExitReason);

/// <summary>
/// Döngü başlatma isteği
/// </summary>
public record LoopStartRequest
{
    /// <summary>Döngü kimliği (benzersiz)</summary>
    public required string LoopId { get; init; }

    /// <summary>Maksimum iterasyon sayısı</summary>
    public int MaxIterations { get; init; } = 100;

    /// <summary>Zaman aşımı (dakika)</summary>
    public int TimeoutMinutes { get; init; } = 60;

    /// <summary>Başlangıç bağlam verisi (JSON)</summary>
    public string? InitialContextJson { get; init; }

    /// <summary>Özel çıkış koşulu ifadesi (JSON expression)</summary>
    public string? CustomExitCondition { get; init; }
}

/// <summary>
/// Döngü durdurma isteği
/// </summary>
public record LoopStopRequest
{
    /// <summary>Döngü kimliği</summary>
    public required string LoopId { get; init; }

    /// <summary>Durdurma nedeni</summary>
    public string? Reason { get; init; }
}
