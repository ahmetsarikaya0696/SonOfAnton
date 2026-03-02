using SonOfAnton.Shared.Core;

namespace SonOfAnton.Shared.Entities;

/// <summary>
/// Sansür kuralı entity'si — PostgreSQL'de saklanır
/// </summary>
public class CensorshipRuleEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Kuralın ait olduğu tenant
    /// </summary>
    public string? TenantId { get; set; }
    public TenantEntity? Tenant { get; set; }

    /// <summary>
    /// Kural adı
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Kural açıklaması
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Kural türü: Regex, Keyword veya NerLabel
    /// </summary>
    public CensorshipRuleType RuleType { get; set; }

    /// <summary>
    /// Regex deseni veya anahtar kelime veya NER etiket adı
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// Maskeleme sırasında kullanılacak ikame metni. null = varsayılan maskeleme
    /// </summary>
    public string? ReplacementText { get; set; }

    /// <summary>
    /// Önem seviyesi (1-5). Yüksek = daha kritik
    /// </summary>
    public int Severity { get; set; } = 3;

    /// <summary>
    /// Kural aktif mi?
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Sansür denetim kaydı — hangi veri tespit edildi ve ne yapıldı
/// </summary>
public class CensorshipAuditLogEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Kayıtın ait olduğu tenant
    /// </summary>
    public string? TenantId { get; set; }
    public TenantEntity? Tenant { get; set; }

    /// <summary>
    /// İşlemi yapan kullanıcı
    /// </summary>
    public Guid? UserId { get; set; }
    public UserEntity? User { get; set; }

    /// <summary>
    /// Orijinal metnin SHA256 hash'i (gizlilik için metin saklanmaz)
    /// </summary>
    public string OriginalTextHash { get; set; } = string.Empty;

    /// <summary>
    /// Tespit edilen öğeler (JSON)
    /// </summary>
    public string DetectedItemsJson { get; set; } = "[]";

    /// <summary>
    /// Uygulanan eylem: Blocked, Redacted, Allowed
    /// </summary>
    public CensorshipAction Action { get; set; }

    /// <summary>
    /// Tetiklenen kuralların ID'leri (JSON)
    /// </summary>
    public string? TriggeredRuleIdsJson { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
