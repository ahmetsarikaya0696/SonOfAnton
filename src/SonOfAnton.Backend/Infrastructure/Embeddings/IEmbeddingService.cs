namespace SonOfAnton.Backend.Infrastructure.Embeddings;

/// <summary>
/// Embedding model tipi
/// </summary>
public enum EmbeddingModelType
{
    /// <summary>
    /// Metin embedding - all-MiniLM-L6-v2 (384 dimension)
    /// </summary>
    TextSmall,
    
    /// <summary>
    /// Metin embedding - bge-small-en (384 dimension)
    /// </summary>
    TextMedium,
    
    /// <summary>
    /// Görüntü embedding - CLIP Vision (512 dimension)
    /// </summary>
    ClipVision
}

/// <summary>
/// Embedding sonucu
/// </summary>
public class EmbeddingResult
{
    /// <summary>
    /// Embedding vektörü
    /// </summary>
    public required float[] Vector { get; init; }
    
    /// <summary>
    /// Kullanılan model
    /// </summary>
    public EmbeddingModelType Model { get; init; }
    
    /// <summary>
    /// Vektör boyutu
    /// </summary>
    public int Dimension => Vector.Length;
    
    /// <summary>
    /// İşlem süresi
    /// </summary>
    public TimeSpan ProcessingTime { get; init; }
}

/// <summary>
/// Metin embedding servisi interface'i
/// </summary>
public interface ITextEmbeddingService
{
    /// <summary>
    /// Metni vektöre dönüştürür
    /// </summary>
    Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Birden fazla metni batch olarak vektöre dönüştürür
    /// </summary>
    Task<IReadOnlyList<EmbeddingResult>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Model boyutu
    /// </summary>
    int Dimension { get; }
    
    /// <summary>
    /// Model tipi
    /// </summary>
    EmbeddingModelType ModelType { get; }
}

/// <summary>
/// Görüntü embedding servisi interface'i
/// </summary>
public interface IImageEmbeddingService
{
    /// <summary>
    /// Görüntüyü vektöre dönüştürür
    /// </summary>
    Task<EmbeddingResult> GenerateEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Görüntüyü base64'ten vektöre dönüştürür
    /// </summary>
    Task<EmbeddingResult> GenerateEmbeddingFromBase64Async(string base64Image, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Model boyutu
    /// </summary>
    int Dimension { get; }
}
