using SonOfAnton.Backend.Infrastructure.Embeddings;
using SonOfAnton.Backend.Infrastructure.VectorDatabase;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Services.Search;

/// <summary>
/// Hybrid search — vector similarity + full-text search birleştirme (RRF)
/// </summary>
public class HybridSearchService
{
    private readonly IVectorDatabaseService _vectorService;
    private readonly FullTextSearchService _ftsService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HybridSearchService> _logger;

    public HybridSearchService(
        IVectorDatabaseService vectorService,
        FullTextSearchService ftsService,
        IServiceProvider serviceProvider,
        ILogger<HybridSearchService> logger)
    {
        _vectorService = vectorService;
        _ftsService = ftsService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Hem vektör hem metin araması yapıp RRF ile birleştirir
    /// </summary>
    public async Task<SearchResultDto<object>> SearchAsync(
        string query, string collection = "default",
        int topK = 10, CancellationToken ct = default)
    {
        var ftsTask = _ftsService.SearchAsync(new SearchRequest
        {
            Query = query,
            Types = ["workflow"],
            PageSize = topK
        }, ct);

        // Vector search (embedding gerektirir)
        List<SearchHitDto<object>> vectorHits = [];
        try
        {
            var embeddingService = _serviceProvider.GetService<ITextEmbeddingService>();
            if (embeddingService != null)
            {
                var embedding = await embeddingService.GenerateEmbeddingAsync(query, ct);
                var vectorResults = await _vectorService.SearchAsync(
                    collection, embedding.Vector, topK, 0.0,
                    SimilarityMetric.Cosine, cancellationToken: ct);

                vectorHits = vectorResults.Select(r => new SearchHitDto<object>
                {
                    Item = new { r.Id, r.Content, r.Score },
                    Score = r.Score,
                    Highlight = r.Content?.Length > 100 ? r.Content[..100] + "..." : r.Content ?? "",
                    ResultType = "vector"
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vector search başarısız, sadece FTS kullanılıyor");
        }

        var ftsResult = await ftsTask;

        // RRF (Reciprocal Rank Fusion) ile birleştir
        var merged = ReciprocalRankFusion(ftsResult.Hits, vectorHits, k: 60);

        return new SearchResultDto<object>
        {
            Hits = merged.Take(topK).ToList(),
            TotalCount = merged.Count,
            Page = 1,
            PageSize = topK
        };
    }

    /// <summary>
    /// Reciprocal Rank Fusion — farklı kaynaklardan gelen sıralamaları birleştirir
    /// </summary>
    private static List<SearchHitDto<object>> ReciprocalRankFusion(
        List<SearchHitDto<object>> listA,
        List<SearchHitDto<object>> listB,
        int k = 60)
    {
        var scores = new Dictionary<int, double>();
        var itemMap = new Dictionary<int, SearchHitDto<object>>();

        for (int i = 0; i < listA.Count; i++)
        {
            var hash = listA[i].GetHashCode();
            scores[hash] = 1.0 / (k + i + 1);
            itemMap[hash] = listA[i];
        }

        for (int i = 0; i < listB.Count; i++)
        {
            var hash = listB[i].GetHashCode();
            if (scores.ContainsKey(hash))
                scores[hash] += 1.0 / (k + i + 1);
            else
            {
                scores[hash] = 1.0 / (k + i + 1);
                itemMap[hash] = listB[i];
            }
        }

        return scores
            .OrderByDescending(kv => kv.Value)
            .Select(kv =>
            {
                var hit = itemMap[kv.Key];
                hit.Score = kv.Value;
                return hit;
            })
            .ToList();
    }
}
