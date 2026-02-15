using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Services.Search;

/// <summary>
/// PostgreSQL full-text search servisi
/// </summary>
public class FullTextSearchService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FullTextSearchService> _logger;

    public FullTextSearchService(IServiceProvider serviceProvider, ILogger<FullTextSearchService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Tüm entity tipleri üzerinde arama
    /// </summary>
    public async Task<SearchResultDto<object>> SearchAsync(SearchRequest request, CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

        var allHits = new List<SearchHitDto<object>>();
        var types = request.Types.Count > 0 ? request.Types : ["workflow", "log"];

        if (types.Contains("workflow"))
        {
            var workflows = await SearchWorkflowsAsync(db, request.Query, ct);
            allHits.AddRange(workflows);
        }

        if (types.Contains("log"))
        {
            var logs = await SearchLogsAsync(db, request.Query, ct);
            allHits.AddRange(logs);
        }

        // Skor ile sırala
        allHits = [.. allHits.OrderByDescending(h => h.Score)];

        var totalCount = allHits.Count;
        var paged = allHits
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new SearchResultDto<object>
        {
            Hits = paged,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    private static async Task<List<SearchHitDto<object>>> SearchWorkflowsAsync(
        SonOfAntonDbContext db, string query, CancellationToken ct)
    {
        var lowerQuery = $"%{query.ToLowerInvariant()}%";

        var workflows = await db.Workflows
            .Where(w => EF.Functions.ILike(w.Name, lowerQuery) ||
                        (w.Description != null && EF.Functions.ILike(w.Description, lowerQuery)))
            .Take(50)
            .Select(w => new
            {
                w.Id,
                w.Name,
                w.Description,
                w.TriggerType,
                w.IsActive,
                w.CreatedAt,
                w.UpdatedAt,
                w.RunCount,
                w.LastRunAt
            })
            .ToListAsync(ct);

        return workflows.Select(w => new SearchHitDto<object>
        {
            Item = new WorkflowDto
            {
                Id = w.Id,
                Name = w.Name,
                Description = w.Description,
                TriggerType = w.TriggerType,
                IsActive = w.IsActive,
                CreatedAt = w.CreatedAt,
                UpdatedAt = w.UpdatedAt,
                RunCount = w.RunCount,
                LastRunAt = w.LastRunAt
            },
            Score = CalculateScore(query, w.Name, w.Description),
            Highlight = HighlightMatch(query, w.Name),
            ResultType = "workflow"
        }).ToList();
    }

    private static async Task<List<SearchHitDto<object>>> SearchLogsAsync(
        SonOfAntonDbContext db, string query, CancellationToken ct)
    {
        var lowerQuery = $"%{query.ToLowerInvariant()}%";

        var logs = await db.ExecutionLogs
            .Where(l => EF.Functions.ILike(l.Message, lowerQuery))
            .OrderByDescending(l => l.Timestamp)
            .Take(50)
            .Select(l => new
            {
                l.Id,
                l.ExecutionId,
                l.NodeId,
                l.Level,
                l.Message,
                l.Timestamp
            })
            .ToListAsync(ct);

        return logs.Select(l => new SearchHitDto<object>
        {
            Item = new LogEventDto
            {
                ExecutionId = l.ExecutionId,
                NodeId = l.NodeId,
                Level = l.Level,
                Message = l.Message,
                Timestamp = l.Timestamp
            },
            Score = CalculateScore(query, l.Message, null),
            Highlight = HighlightMatch(query, l.Message),
            ResultType = "log"
        }).ToList();
    }

    private static double CalculateScore(string query, string title, string? description)
    {
        var score = 0.0;
        var lowerQuery = query.ToLowerInvariant();

        if (title.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase))
            score += 2.0;

        if (description?.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) == true)
            score += 1.0;

        // Exact match bonus
        if (title.Equals(query, StringComparison.OrdinalIgnoreCase))
            score += 3.0;

        return score;
    }

    private static string HighlightMatch(string query, string text)
    {
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(text))
            return text;

        var idx = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return text.Length > 100 ? text[..100] + "..." : text;

        var start = Math.Max(0, idx - 30);
        var end = Math.Min(text.Length, idx + query.Length + 30);
        var snippet = text[start..end];

        return (start > 0 ? "..." : "") + snippet + (end < text.Length ? "..." : "");
    }
}
