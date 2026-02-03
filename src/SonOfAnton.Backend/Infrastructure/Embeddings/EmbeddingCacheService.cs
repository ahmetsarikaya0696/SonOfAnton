using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace SonOfAnton.Backend.Infrastructure.Embeddings;

/// <summary>
/// Embedding cache servisi - aynı metin/görüntü için tekrar hesaplama yapmaz
/// LRU (Least Recently Used) stratejisi kullanır
/// </summary>
public class EmbeddingCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<EmbeddingCacheService> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    
    private const int DefaultCacheMinutes = 60;
    private const int MaxCacheItems = 10000;
    
    public EmbeddingCacheService(IMemoryCache cache, ILogger<EmbeddingCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }
    
    /// <summary>
    /// Cache'den embedding alır veya hesaplar
    /// </summary>
    public async Task<EmbeddingResult> GetOrCreateAsync(
        string key,
        Func<Task<EmbeddingResult>> factory,
        TimeSpan? absoluteExpiration = null)
    {
        var cacheKey = $"embedding:{key}";
        
        // Önce cache'e bak
        if (_cache.TryGetValue(cacheKey, out EmbeddingResult? cached) && cached != null)
        {
            _logger.LogDebug("Embedding cache hit: {Key}", key);
            return cached;
        }
        
        // Lock al (aynı key için paralel hesaplama önleme)
        var lockObj = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        
        await lockObj.WaitAsync();
        try
        {
            // Double-check
            if (_cache.TryGetValue(cacheKey, out cached) && cached != null)
            {
                return cached;
            }
            
            // Hesapla
            _logger.LogDebug("Embedding cache miss, hesaplanıyor: {Key}", key);
            var result = await factory();
            
            // Cache'e ekle
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = absoluteExpiration ?? TimeSpan.FromMinutes(DefaultCacheMinutes),
                SlidingExpiration = TimeSpan.FromMinutes(15),
                Size = 1,
                Priority = CacheItemPriority.Normal
            };
            
            _cache.Set(cacheKey, result, options);
            
            return result;
        }
        finally
        {
            lockObj.Release();
        }
    }
    
    /// <summary>
    /// Metin için cache key oluşturur
    /// </summary>
    public static string CreateTextKey(string text, EmbeddingModelType model)
    {
        // SHA256 hash kullan
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
        var hashString = Convert.ToHexString(hash)[..16]; // İlk 16 karakter yeterli
        return $"text:{model}:{hashString}";
    }
    
    /// <summary>
    /// Görüntü için cache key oluşturur
    /// </summary>
    public static string CreateImageKey(byte[] imageBytes)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(imageBytes);
        var hashString = Convert.ToHexString(hash)[..16];
        return $"image:clip:{hashString}";
    }
    
    /// <summary>
    /// Belirli bir key'i cache'den siler
    /// </summary>
    public void Remove(string key)
    {
        _cache.Remove($"embedding:{key}");
    }
    
    /// <summary>
    /// Tüm embedding cache'ini temizler
    /// </summary>
    public void Clear()
    {
        // IMemoryCache tüm cache'i temizleme desteği yok
        // Bu yüzden sadece log yazıyoruz
        _logger.LogWarning("Cache clear istendi ama IMemoryCache bunu desteklemiyor. Yeni instance oluşturulmalı.");
    }
}
