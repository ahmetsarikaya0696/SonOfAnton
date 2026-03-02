using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Shared.Entities;

/// <summary>
/// Ajanların markdown planlama belgelerini veritabanında saklamak için entity.
/// Dosya sistemiyle dual-write senaryosunda yedek/senkronizasyon kaynağı olarak kullanılır.
/// </summary>
public class PlanningDocumentEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Belgeyi oluşturan/sahip olan ajan veya oturum kimliği
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Belge tipi (TaskPlan, Findings, Progress)
    /// </summary>
    public PlanFileType FileType { get; set; }

    /// <summary>
    /// Markdown içeriği
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Belge versiyonu (her yazma işleminde artar)
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Belgenin disk üzerindeki dosya yolu (referans)
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Tenant kimliği (multi-tenant destek)
    /// </summary>
    public string? TenantId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public TenantEntity? Tenant { get; set; }
}

/// <summary>
/// Ralph döngü motorunun çalışma durumunu PostgreSQL'de kalıcı olarak saklar.
/// MemoryCache ile birlikte hybrid state management sağlar.
/// </summary>
public class LoopExecutionStateEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Döngünün benzersiz kimliği (bir ajan oturumu veya görev bağlamı)
    /// </summary>
    public string LoopId { get; set; } = string.Empty;

    /// <summary>
    /// Döngünün mevcut durumu
    /// </summary>
    public LoopState State { get; set; } = LoopState.Idle;

    /// <summary>
    /// Mevcut iterasyon numarası
    /// </summary>
    public int CurrentIteration { get; set; }

    /// <summary>
    /// Maksimum izin verilen iterasyon sayısı
    /// </summary>
    public int MaxIterations { get; set; } = 100;

    /// <summary>
    /// Döngü bağlam verisi (JSON formatında ek bilgiler)
    /// </summary>
    public string? ContextJson { get; set; }

    /// <summary>
    /// Döngüden çıkış nedeni (tamamlandığında set edilir)
    /// </summary>
    public LoopExitReason? ExitReason { get; set; }

    /// <summary>
    /// Çıkış mesajı veya hata detayı
    /// </summary>
    public string? ExitMessage { get; set; }

    /// <summary>
    /// Hangfire job kimliği (recurring job takibi için)
    /// </summary>
    public string? HangfireJobId { get; set; }

    /// <summary>
    /// Tenant kimliği (multi-tenant destek)
    /// </summary>
    public string? TenantId { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Son heartbeat zamanı (döngünün aktif olduğunu kanıtlar)
    /// </summary>
    public DateTime LastHeartbeatAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    // Navigation
    public TenantEntity? Tenant { get; set; }
}
