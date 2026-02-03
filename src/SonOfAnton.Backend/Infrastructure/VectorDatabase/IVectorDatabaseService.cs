using System.Text.Json;
using Pgvector;

namespace SonOfAnton.Backend.Infrastructure.VectorDatabase;

/// <summary>
/// Vector arama sonucu
/// </summary>
public class VectorSearchResult
{
    /// <summary>
    /// Doküman ID
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// Benzerlik skoru (0-1 arası, 1 = tam eşleşme)
    /// </summary>
    public double Score { get; init; }
    
    /// <summary>
    /// Uzaklık değeri (metriğe göre değişir)
    /// </summary>
    public double Distance { get; init; }
    
    /// <summary>
    /// Doküman metadata'sı
    /// </summary>
    public JsonDocument? Metadata { get; init; }
    
    /// <summary>
    /// Orijinal içerik (opsiyonel)
    /// </summary>
    public string? Content { get; init; }
}

/// <summary>
/// Benzerlik metriği
/// </summary>
public enum SimilarityMetric
{
    /// <summary>
    /// Cosine similarity - Metin için en yaygın
    /// </summary>
    Cosine,
    
    /// <summary>
    /// Euclidean distance (L2)
    /// </summary>
    Euclidean,
    
    /// <summary>
    /// Inner product - Normalleştirilmiş vektörler için
    /// </summary>
    InnerProduct
}

/// <summary>
/// Vector database servisi interface'i
/// </summary>
public interface IVectorDatabaseService
{
    /// <summary>
    /// Vektör saklar
    /// </summary>
    Task StoreAsync(
        string collection,
        string id,
        float[] vector,
        JsonDocument? metadata = null,
        string? content = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Batch vektör saklar
    /// </summary>
    Task StoreBatchAsync(
        string collection,
        IEnumerable<(string Id, float[] Vector, JsonDocument? Metadata, string? Content)> items,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Vektör arar
    /// </summary>
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collection,
        float[] queryVector,
        int topK = 10,
        double minSimilarity = 0.0,
        SimilarityMetric metric = SimilarityMetric.Cosine,
        JsonDocument? metadataFilter = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// ID ile vektör getirir
    /// </summary>
    Task<VectorSearchResult?> GetByIdAsync(
        string collection,
        string id,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// ID ile vektör siler
    /// </summary>
    Task<bool> DeleteAsync(
        string collection,
        string id,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Collection oluşturur
    /// </summary>
    Task CreateCollectionAsync(
        string collection,
        int dimension,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Collection siler
    /// </summary>
    Task<bool> DeleteCollectionAsync(
        string collection,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Collection var mı kontrol eder
    /// </summary>
    Task<bool> CollectionExistsAsync(
        string collection,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// HNSW index oluşturur (arama performansı için)
    /// </summary>
    Task CreateIndexAsync(
        string collection,
        SimilarityMetric metric = SimilarityMetric.Cosine,
        int m = 16,
        int efConstruction = 64,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Document embedding entity (EF Core model)
/// </summary>
public class DocumentEmbedding
{
    public required string Id { get; set; }
    public required string Collection { get; set; }
    public required Vector Embedding { get; set; }
    public JsonDocument? Metadata { get; set; }
    public string? Content { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
