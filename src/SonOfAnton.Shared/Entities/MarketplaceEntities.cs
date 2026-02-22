using System.Text.Json;

namespace SonOfAnton.Shared.Entities;

/// <summary>
/// Marketplace öğe tipi
/// </summary>
public enum MarketplaceItemType
{
    /// <summary>Özel node tanımı</summary>
    CustomNode,
    /// <summary>Workflow şablonu</summary>
    WorkflowTemplate
}

/// <summary>
/// Marketplace öğesi — tenant-scoped.
/// Bir tenant'ın marketplace'i sadece o tenant'a ait öğeleri içerir.
/// </summary>
public class MarketplaceItemEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tenant kimliği</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Yayınlayan kullanıcı</summary>
    public Guid? PublishedByUserId { get; set; }

    /// <summary>Öğe tipi</summary>
    public MarketplaceItemType ItemType { get; set; }

    /// <summary>Görünen ad</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Açıklama</summary>
    public string? Description { get; set; }

    /// <summary>Icon</summary>
    public string Icon { get; set; } = "package";

    /// <summary>Kategori</summary>
    public string Category { get; set; } = "Genel";

    /// <summary>Etiketler (JSONB string array)</summary>
    public JsonDocument? Tags { get; set; }

    /// <summary>İçerik — node definition JSON veya workflow template JSON</summary>
    public JsonDocument? ContentJson { get; set; }

    /// <summary>Versiyon</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>İndirme sayısı</summary>
    public int DownloadCount { get; set; }

    /// <summary>Ortalama puan (1-5)</summary>
    public double AverageRating { get; set; }

    /// <summary>Yayında mı</summary>
    public bool IsPublished { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ─── Navigation ─────────────────────────────────────────────
    public TenantEntity? Tenant { get; set; }
    public UserEntity? PublishedByUser { get; set; }
    public ICollection<MarketplaceReviewEntity> Reviews { get; set; } = [];
    public ICollection<MarketplaceInstallEntity> Installs { get; set; } = [];
}

/// <summary>
/// Marketplace değerlendirmesi
/// </summary>
public class MarketplaceReviewEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ItemId { get; set; }

    public Guid UserId { get; set; }

    public string TenantId { get; set; } = string.Empty;

    /// <summary>Puan (1-5)</summary>
    public int Rating { get; set; }

    /// <summary>Yorum</summary>
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ─── Navigation ─────────────────────────────────────────────
    public MarketplaceItemEntity Item { get; set; } = null!;
    public UserEntity? User { get; set; }
}

/// <summary>
/// Marketplace kurulum kaydı — hangi kullanıcı hangi öğeyi kurdu
/// </summary>
public class MarketplaceInstallEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ItemId { get; set; }

    public Guid UserId { get; set; }

    public string TenantId { get; set; } = string.Empty;

    /// <summary>Kurulum sırasındaki sürüm</summary>
    public string InstalledVersion { get; set; } = "1.0.0";

    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;

    // ─── Navigation ─────────────────────────────────────────────
    public MarketplaceItemEntity Item { get; set; } = null!;
    public UserEntity? User { get; set; }
}
