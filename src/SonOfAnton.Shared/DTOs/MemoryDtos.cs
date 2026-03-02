using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Shared.DTOs;

// ─── Collection DTOs ────────────────────────────────────────────

/// <summary>
/// Bellek koleksiyonu DTO
/// </summary>
public class MemoryCollectionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public MemorySectorType DefaultSectorType { get; set; }
    public int EmbeddingDimension { get; set; }
    public int EntryCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Koleksiyon oluşturma isteği
/// </summary>
public class CreateMemoryCollectionRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public MemorySectorType DefaultSectorType { get; set; } = MemorySectorType.Semantic;
    public int EmbeddingDimension { get; set; } = 384;
    public Guid? WorkspaceId { get; set; }
}

// ─── Entry DTOs ─────────────────────────────────────────────────

/// <summary>
/// Bellek kaydı DTO
/// </summary>
public class MemoryEntryDto
{
    public Guid Id { get; set; }
    public Guid CollectionId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public MemorySectorType SectorType { get; set; }
    public float Importance { get; set; }
    public int AccessCount { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public int ChunkIndex { get; set; }
    public string? SourceDocumentId { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Belleğe metin kaydetme isteği (chunking + embedding otomatik yapılır)
/// </summary>
public class MemoryStoreRequestDto
{
    /// <summary>Hedef koleksiyon Id</summary>
    public Guid CollectionId { get; set; }

    /// <summary>Kaydedilecek metin</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Bellek sektörü</summary>
    public MemorySectorType SectorType { get; set; } = MemorySectorType.Semantic;

    /// <summary>Önem derecesi (0-1)</summary>
    public float Importance { get; set; } = 0.5f;

    /// <summary>Ek metadata</summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>Chunking stratejisi (null ise varsayılan kullanılır)</summary>
    public ChunkingStrategy? ChunkingStrategy { get; set; }

    /// <summary>Kaynak döküman Id (chunk gruplamak için)</summary>
    public string? SourceDocumentId { get; set; }
}

// ─── Search DTOs ────────────────────────────────────────────────

/// <summary>
/// Bellek arama isteği
/// </summary>
public class MemorySearchRequestDto
{
    /// <summary>Arama sorgusu</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>Koleksiyon Id (null = tüm koleksiyonlar)</summary>
    public Guid? CollectionId { get; set; }

    /// <summary>Belirli sektörlerde ara (boş = tümü)</summary>
    public List<MemorySectorType> SectorTypes { get; set; } = [];

    /// <summary>Döndürülecek sonuç sayısı</summary>
    public int TopK { get; set; } = 10;

    /// <summary>Minimum benzerlik skoru (0-1)</summary>
    public double MinSimilarity { get; set; } = 0.3;
}

/// <summary>
/// Bellek arama sonucu
/// </summary>
public class MemorySearchResultDto
{
    /// <summary>Bulunan bellek kayıtları</summary>
    public List<MemorySearchHitDto> Hits { get; set; } = [];

    /// <summary>Toplam sonuç sayısı</summary>
    public int TotalCount { get; set; }

    /// <summary>Sorgu süresi</summary>
    public TimeSpan QueryDuration { get; set; }
}

/// <summary>
/// Tek bir arama sonucu
/// </summary>
public class MemorySearchHitDto
{
    /// <summary>Bellek kaydı</summary>
    public MemoryEntryDto Entry { get; set; } = new();

    /// <summary>Benzerlik skoru (0-1, 1 = tam eşleşme)</summary>
    public double Score { get; set; }

    /// <summary>Uzaklık değeri</summary>
    public double Distance { get; set; }
}
