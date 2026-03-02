using SonOfAnton.Backend.Infrastructure.Embeddings;

namespace SonOfAnton.Backend.Services.Memory;

/// <summary>
/// Bellek operasyonları için embedding servisi arayüzü.
/// Mevcut ITextEmbeddingService'i bellek bağlamında sarmalar.
/// </summary>
public interface IMemoryEmbeddingService
{
    /// <summary>
    /// Metni vektöre dönüştürür
    /// </summary>
    Task<float[]> GenerateMemoryEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Birden fazla metni batch olarak vektöre dönüştürür
    /// </summary>
    Task<IReadOnlyList<float[]>> GenerateMemoryEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Embedding boyutu
    /// </summary>
    int Dimension { get; }
}

/// <summary>
/// Mevcut ITextEmbeddingService üzerine inşa edilen bellek embedding servisi.
/// Fallback mantığı: ONNX → Ollama (IServiceProvider ile çözümlenir).
/// </summary>
public class MemoryEmbeddingService : IMemoryEmbeddingService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MemoryEmbeddingService> _logger;

    public int Dimension { get; }

    public MemoryEmbeddingService(IServiceProvider serviceProvider, ILogger<MemoryEmbeddingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Mevcut servis üzerinden boyutu al, yoksa varsayılan 384
        var embeddingService = serviceProvider.GetService<ITextEmbeddingService>();
        Dimension = embeddingService?.Dimension ?? 384;
    }

    public async Task<float[]> GenerateMemoryEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var service = GetEmbeddingService();
        var result = await service.GenerateEmbeddingAsync(text, cancellationToken);
        return result.Vector;
    }

    public async Task<IReadOnlyList<float[]>> GenerateMemoryEmbeddingsAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var service = GetEmbeddingService();
        var results = await service.GenerateEmbeddingsAsync(texts, cancellationToken);
        return results.Select(r => r.Vector).ToArray();
    }

    private ITextEmbeddingService GetEmbeddingService()
    {
        var service = _serviceProvider.GetService<ITextEmbeddingService>();
        if (service is null)
        {
            _logger.LogError("ITextEmbeddingService bulunamadı. DI kaydı kontrol edilmeli.");
            throw new InvalidOperationException(
                "Text embedding servisi yapılandırılmamış. ONNX veya Ollama embedding servisi DI'a kaydedilmelidir.");
        }
        return service;
    }
}
