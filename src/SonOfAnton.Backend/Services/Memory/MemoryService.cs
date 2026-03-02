using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Backend.Services.Memory;

/// <summary>
/// Ana bellek yönetim servisi arayüzü — CRUD + Search + Chunking pipeline
/// </summary>
public interface IMemoryService
{
    // ─── Store (Chunking + Embedding + Kaydetme) ────────────────────
    /// <summary>
    /// Metni chunk'lara ayırıp vektörleştirip belleğe kaydeder
    /// </summary>
    Task<IReadOnlyList<MemoryEntryDto>> StoreAsync(
        MemoryStoreRequestDto request,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    // ─── Search ─────────────────────────────────────────────────────
    /// <summary>
    /// Vektörel benzerlik araması yapar
    /// </summary>
    Task<MemorySearchResultDto> SearchAsync(
        MemorySearchRequestDto request,
        CancellationToken cancellationToken = default);

    // ─── CRUD ─────────────────────────────────────────────────────────
    /// <summary>
    /// Id ile bellek kaydı getirir
    /// </summary>
    Task<MemoryEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bellek kaydını siler
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kaynak döküman Id'sine göre tüm chunk'ları siler
    /// </summary>
    Task<int> DeleteBySourceDocumentAsync(string sourceDocumentId, CancellationToken cancellationToken = default);

    // ─── Collections ─────────────────────────────────────────────────
    /// <summary>
    /// Tüm koleksiyonları getirir
    /// </summary>
    Task<IReadOnlyList<MemoryCollectionDto>> GetCollectionsAsync(
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Yeni koleksiyon oluşturur
    /// </summary>
    Task<MemoryCollectionDto> CreateCollectionAsync(
        CreateMemoryCollectionRequest request,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Koleksiyonu siler (tüm kayıtlar dahil)
    /// </summary>
    Task<bool> DeleteCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestrator: Chunking → Embedding → Store → Search pipeline yönetimi.
/// </summary>
public class MemoryService : IMemoryService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMemoryEmbeddingService _embeddingService;
    private readonly ITextChunkingService _chunkingService;
    private readonly IMemorySearchService _searchService;
    private readonly ILogger<MemoryService> _logger;
    private readonly IConfiguration _configuration;

    public MemoryService(
        IServiceProvider serviceProvider,
        IMemoryEmbeddingService embeddingService,
        ITextChunkingService chunkingService,
        IMemorySearchService searchService,
        ILogger<MemoryService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _embeddingService = embeddingService;
        _chunkingService = chunkingService;
        _searchService = searchService;
        _logger = logger;
        _configuration = configuration;
    }

    // ─── Store Pipeline ──────────────────────────────────────────────

    public async Task<IReadOnlyList<MemoryEntryDto>> StoreAsync(
        MemoryStoreRequestDto request,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

        // 1. Koleksiyonun var olduğunu kontrol et
        var collection = await db.MemoryCollections
            .FirstOrDefaultAsync(c => c.Id == request.CollectionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Koleksiyon bulunamadı: {request.CollectionId}");

        // 2. Metin parçalama (Chunking)
        var chunkingOptions = new ChunkingOptions
        {
            Strategy = request.ChunkingStrategy
                ?? Enum.Parse<ChunkingStrategy>(
                    _configuration["Memory:DefaultChunkingStrategy"] ?? "Paragraph"),
            MaxChunkSize = _configuration.GetValue("Memory:MaxChunkSize", 512),
            OverlapSize = _configuration.GetValue("Memory:ChunkOverlap", 50)
        };

        var chunks = _chunkingService.ChunkText(request.Content, chunkingOptions);

        _logger.LogInformation(
            "Metin {TextLength} karakter → {ChunkCount} chunk olarak parçalandı (Koleksiyon: {Collection})",
            request.Content.Length, chunks.Count, collection.Name);

        // 3. Batch embedding
        var chunkTexts = chunks.Select(c => c.Content).ToList();
        var embeddings = await _embeddingService.GenerateMemoryEmbeddingsAsync(chunkTexts, cancellationToken);

        // 4. Entity oluştur ve kaydet
        var sourceDocId = request.SourceDocumentId ?? Guid.NewGuid().ToString();
        var metadataJson = request.Metadata != null
            ? JsonDocument.Parse(JsonSerializer.Serialize(request.Metadata))
            : null;

        var entries = new List<MemoryEntryEntity>();

        for (int i = 0; i < chunks.Count; i++)
        {
            var entry = new MemoryEntryEntity
            {
                CollectionId = request.CollectionId,
                Content = chunks[i].Content,
                Embedding = new Vector(embeddings[i]),
                Metadata = metadataJson,
                SectorType = request.SectorType,
                Importance = request.Importance,
                ChunkIndex = chunks[i].Index,
                SourceDocumentId = sourceDocId
            };

            entries.Add(entry);
        }

        db.MemoryEntries.AddRange(entries);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "{EntryCount} bellek kaydı oluşturuldu (Kaynak: {SourceDoc})",
            entries.Count, sourceDocId);

        return entries.Select(MapToDto).ToList();
    }

    // ─── Search ──────────────────────────────────────────────────────

    public async Task<MemorySearchResultDto> SearchAsync(
        MemorySearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var searchOptions = new MemorySearchOptions
        {
            TopK = request.TopK,
            MinSimilarity = request.MinSimilarity,
            CollectionId = request.CollectionId,
            SectorTypes = request.SectorTypes.Count > 0 ? request.SectorTypes : null
        };

        var results = await _searchService.SearchAsync(request.Query, searchOptions, cancellationToken);

        sw.Stop();

        return new MemorySearchResultDto
        {
            Hits = results.Select(r => new MemorySearchHitDto
            {
                Entry = MapToDto(r.Entry),
                Score = r.Score,
                Distance = r.Distance
            }).ToList(),
            TotalCount = results.Count,
            QueryDuration = sw.Elapsed
        };
    }

    // ─── CRUD ────────────────────────────────────────────────────────

    public async Task<MemoryEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

        var entry = await db.MemoryEntries
            .Include(e => e.Collection)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entry is null) return null;

        // Erişim güncelle
        entry.AccessCount++;
        entry.LastAccessedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return MapToDto(entry);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

        var entry = await db.MemoryEntries.FindAsync([id], cancellationToken);
        if (entry is null) return false;

        db.MemoryEntries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> DeleteBySourceDocumentAsync(
        string sourceDocumentId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

        var entries = await db.MemoryEntries
            .Where(e => e.SourceDocumentId == sourceDocumentId)
            .ToListAsync(cancellationToken);

        if (entries.Count == 0) return 0;

        db.MemoryEntries.RemoveRange(entries);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("{Count} bellek kaydı silindi (SourceDoc: {SourceDoc})",
            entries.Count, sourceDocumentId);

        return entries.Count;
    }

    // ─── Collections ─────────────────────────────────────────────────

    public async Task<IReadOnlyList<MemoryCollectionDto>> GetCollectionsAsync(
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

        var query = db.MemoryCollections.AsQueryable();

        if (!string.IsNullOrEmpty(tenantId))
            query = query.Where(c => c.TenantId == tenantId);

        var collections = await query
            .Select(c => new MemoryCollectionDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                DefaultSectorType = c.DefaultSectorType,
                EmbeddingDimension = c.EmbeddingDimension,
                EntryCount = c.Entries.Count,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return collections;
    }

    public async Task<MemoryCollectionDto> CreateCollectionAsync(
        CreateMemoryCollectionRequest request,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

        var entity = new MemoryCollectionEntity
        {
            Name = request.Name,
            Description = request.Description,
            DefaultSectorType = request.DefaultSectorType,
            EmbeddingDimension = request.EmbeddingDimension,
            TenantId = tenantId,
            WorkspaceId = request.WorkspaceId
        };

        db.MemoryCollections.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bellek koleksiyonu oluşturuldu: {Name} (Id: {Id})", entity.Name, entity.Id);

        return new MemoryCollectionDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            DefaultSectorType = entity.DefaultSectorType,
            EmbeddingDimension = entity.EmbeddingDimension,
            EntryCount = 0,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public async Task<bool> DeleteCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

        var collection = await db.MemoryCollections.FindAsync([collectionId], cancellationToken);
        if (collection is null) return false;

        db.MemoryCollections.Remove(collection); // Cascade ile entry'ler de silinir
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bellek koleksiyonu silindi: {Name}", collection.Name);
        return true;
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static MemoryEntryDto MapToDto(MemoryEntryEntity entity)
    {
        Dictionary<string, object>? metadata = null;
        if (entity.Metadata != null)
        {
            try
            {
                metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    entity.Metadata.RootElement.GetRawText());
            }
            catch
            {
                // Metadata parse edilemezse null olarak bırak
            }
        }

        return new MemoryEntryDto
        {
            Id = entity.Id,
            CollectionId = entity.CollectionId,
            Content = entity.Content,
            Summary = entity.Summary,
            SectorType = entity.SectorType,
            Importance = entity.Importance,
            AccessCount = entity.AccessCount,
            LastAccessedAt = entity.LastAccessedAt,
            ChunkIndex = entity.ChunkIndex,
            SourceDocumentId = entity.SourceDocumentId,
            Metadata = metadata,
            CreatedAt = entity.CreatedAt
        };
    }
}
