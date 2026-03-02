using System.Text.Json;
using Pgvector;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Shared.Entities;

/// <summary>
/// Bellek koleksiyonu — agent/workspace başına bellek grupları
/// </summary>
public class MemoryCollectionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Koleksiyon adı (örn: "project-alpha-memories")</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Koleksiyon açıklaması</summary>
    public string? Description { get; set; }

    /// <summary>Varsayılan bellek sektörü</summary>
    public MemorySectorType DefaultSectorType { get; set; } = MemorySectorType.Semantic;

    /// <summary>Embedding vektör boyutu (örn: 384 = all-MiniLM-L6-v2)</summary>
    public int EmbeddingDimension { get; set; } = 384;

    /// <summary>Çoklu kiracılık desteği</summary>
    public string? TenantId { get; set; }

    /// <summary>Workspace ilişkisi</summary>
    public Guid? WorkspaceId { get; set; }

    /// <summary>Oluşturulma zamanı</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Güncellenme zamanı</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ─── Navigation Properties ──────────────────────────────────
    public TenantEntity? Tenant { get; set; }
    public WorkspaceEntity? Workspace { get; set; }
    public ICollection<MemoryEntryEntity> Entries { get; set; } = [];
}

/// <summary>
/// Ana bellek kaydı — her bir bellek parçası (chunk) için bir satır
/// </summary>
public class MemoryEntryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Ait olduğu koleksiyon Id</summary>
    public Guid CollectionId { get; set; }

    /// <summary>Orijinal metin içeriği</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Metnin kısa özeti (opsiyonel, LLM tarafından oluşturulabilir)</summary>
    public string? Summary { get; set; }

    /// <summary>Embedding vektörü (pgvector)</summary>
    public Vector? Embedding { get; set; }

    /// <summary>Ek metadata (JSON) — kaynak dosya, sayfa no, etiketler vb.</summary>
    public JsonDocument? Metadata { get; set; }

    /// <summary>Bellek sektör tipi</summary>
    public MemorySectorType SectorType { get; set; } = MemorySectorType.Semantic;

    /// <summary>Önem derecesi (0.0–1.0 arası, 1.0 = çok önemli)</summary>
    public float Importance { get; set; } = 0.5f;

    /// <summary>Erişim sayısı — sık erişilen bilgi daha önemli</summary>
    public int AccessCount { get; set; }

    /// <summary>Son erişim zamanı</summary>
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>Son kullanma tarihi (opsiyonel, null = süresiz)</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Chunk sırası (büyük döküman parçası ise)</summary>
    public int ChunkIndex { get; set; }

    /// <summary>Kaynak döküman Id'si (aynı dökümanın chunk'larını gruplamak için)</summary>
    public string? SourceDocumentId { get; set; }

    /// <summary>Oluşturulma zamanı</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Güncellenme zamanı</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ─── Navigation Properties ──────────────────────────────────
    public MemoryCollectionEntity? Collection { get; set; }
}
