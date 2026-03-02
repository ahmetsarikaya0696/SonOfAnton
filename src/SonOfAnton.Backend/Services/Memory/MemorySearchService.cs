using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Backend.Services.Memory;

/// <summary>
/// Bellek arama seçenekleri
/// </summary>
public record MemorySearchOptions
{
    /// <summary>Döndürülecek sonuç sayısı</summary>
    public int TopK { get; init; } = 10;

    /// <summary>Minimum benzerlik skoru (0-1)</summary>
    public double MinSimilarity { get; init; } = 0.3;

    /// <summary>Belirli koleksiyon Id</summary>
    public Guid? CollectionId { get; init; }

    /// <summary>Belirli sektörlerde arama</summary>
    public List<MemorySectorType>? SectorTypes { get; init; }

    /// <summary>Süresi dolmuş kayıtları hariç tut</summary>
    public bool ExcludeExpired { get; init; } = true;
}

/// <summary>
/// Tek bir bellek arama sonucu
/// </summary>
public record MemorySearchResult
{
    public required MemoryEntryEntity Entry { get; init; }
    public double Score { get; init; }
    public double Distance { get; init; }
}

/// <summary>
/// Vektörel benzerlik arama servisi arayüzü
/// </summary>
public interface IMemorySearchService
{
    /// <summary>
    /// Metin sorgusu ile bellek araması yapar (embedding + cosine similarity)
    /// </summary>
    Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
        string query,
        MemorySearchOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Belirli bir sektörde arama yapar
    /// </summary>
    Task<IReadOnlyList<MemorySearchResult>> SearchBySectorAsync(
        string query,
        MemorySectorType sector,
        int topK = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ham vektör ile arama yapar
    /// </summary>
    Task<IReadOnlyList<MemorySearchResult>> SearchByVectorAsync(
        float[] queryVector,
        MemorySearchOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// EF Core + pgvector üzerinde cosine similarity arama servisi.
/// SonOfAntonDbContext üzerinden doğrudan LINQ + pgvector operatörleri kullanır.
/// </summary>
public class MemorySearchService : IMemorySearchService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMemoryEmbeddingService _embeddingService;
    private readonly ILogger<MemorySearchService> _logger;

    public MemorySearchService(
        IServiceProvider serviceProvider,
        IMemoryEmbeddingService embeddingService,
        ILogger<MemorySearchService> logger)
    {
        _serviceProvider = serviceProvider;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
        string query,
        MemorySearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new MemorySearchOptions();

        var queryVector = await _embeddingService.GenerateMemoryEmbeddingAsync(query, cancellationToken);
        return await SearchByVectorAsync(queryVector, options, cancellationToken);
    }

    public async Task<IReadOnlyList<MemorySearchResult>> SearchBySectorAsync(
        string query,
        MemorySectorType sector,
        int topK = 10,
        CancellationToken cancellationToken = default)
    {
        return await SearchAsync(query, new MemorySearchOptions
        {
            TopK = topK,
            SectorTypes = [sector]
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<MemorySearchResult>> SearchByVectorAsync(
        float[] queryVector,
        MemorySearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new MemorySearchOptions();

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

        var pgVector = new Vector(queryVector);

        // Temel sorgu
        IQueryable<MemoryEntryEntity> query = db.MemoryEntries
            .Include(e => e.Collection)
            .Where(e => e.Embedding != null);

        // Koleksiyon filtresi
        if (options.CollectionId.HasValue)
        {
            query = query.Where(e => e.CollectionId == options.CollectionId.Value);
        }

        // Sektör filtresi
        if (options.SectorTypes is { Count: > 0 })
        {
            query = query.Where(e => options.SectorTypes.Contains(e.SectorType));
        }

        // Süresi dolmuş kayıtları hariç tut
        if (options.ExcludeExpired)
        {
            var now = DateTime.UtcNow;
            query = query.Where(e => e.ExpiresAt == null || e.ExpiresAt > now);
        }

        // Cosine distance ile sıralama ve filtreleme
        // pgvector cosine distance: 1 - cosine_similarity, yani küçük = daha benzer
        var results = await query
            .Select(e => new
            {
                Entry = e,
                Distance = e.Embedding!.CosineDistance(pgVector)
            })
            .Where(x => (1.0 - x.Distance) >= options.MinSimilarity)
            .OrderBy(x => x.Distance)
            .Take(options.TopK)
            .ToListAsync(cancellationToken);

        var searchResults = results.Select(r => new MemorySearchResult
        {
            Entry = r.Entry,
            Distance = r.Distance,
            Score = 1.0 - r.Distance // Cosine similarity = 1 - cosine distance
        }).ToList();

        _logger.LogDebug(
            "Bellek araması: {ResultCount} sonuç bulundu (TopK={TopK}, MinSim={MinSim})",
            searchResults.Count, options.TopK, options.MinSimilarity);

        // Erişim sayısını güncelle
        foreach (var result in searchResults)
        {
            result.Entry.AccessCount++;
            result.Entry.LastAccessedAt = DateTime.UtcNow;
        }

        if (searchResults.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return searchResults;
    }
}
