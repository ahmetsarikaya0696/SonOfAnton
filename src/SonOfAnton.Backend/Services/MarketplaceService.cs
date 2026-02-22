using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;

namespace SonOfAnton.Backend.Services;

/// <summary>
/// Marketplace servisi — tenant-scoped.
/// Node tanımlarını ve workflow template'lerini paylaşma, kurma ve değerlendirme.
/// </summary>
public class MarketplaceService
{
    private readonly SonOfAntonDbContext _db;
    private readonly IUserContext _userContext;
    private readonly CustomNodeService _customNodeService;
    private readonly ILogger<MarketplaceService> _logger;

    public MarketplaceService(
        SonOfAntonDbContext db,
        IUserContext userContext,
        CustomNodeService customNodeService,
        ILogger<MarketplaceService> logger)
    {
        _db = db;
        _userContext = userContext;
        _customNodeService = customNodeService;
        _logger = logger;
    }

    /// <summary>
    /// Marketplace'e öğe yayınla.
    /// </summary>
    public async Task<MarketplaceItemDto> PublishAsync(PublishMarketplaceItemDto dto)
    {
        var tenantId = _userContext.TenantId ?? "default";
        var userId = _userContext.CurrentUserId;

        JsonDocument? contentJson = null;

        // İçerik türüne göre içerik oluştur
        if (dto.ItemType == "CustomNode" && dto.SourceId.HasValue)
        {
            var customNode = await _db.CustomNodeDefinitions
                .FirstOrDefaultAsync(n => n.Id == dto.SourceId.Value && n.TenantId == tenantId)
                ?? throw new KeyNotFoundException("Kaynak custom node bulunamadı.");

            // Node tanımını JSON olarak sakla
            var nodeData = new
            {
                customNode.Type,
                customNode.Name,
                customNode.Icon,
                customNode.Category,
                customNode.Description,
                customNode.Color,
                Inputs = customNode.InputsJson,
                Outputs = customNode.OutputsJson,
                Parameters = customNode.ParametersJson,
                customNode.ScriptType,
                customNode.ScriptCode
            };
            contentJson = JsonDocument.Parse(JsonSerializer.Serialize(nodeData));

            // Custom node'u published olarak işaretle
            customNode.IsPublished = true;
            customNode.UpdatedAt = DateTime.UtcNow;
        }
        else if (dto.ItemType == "WorkflowTemplate" && dto.SourceId.HasValue)
        {
            var workflow = await _db.Workflows
                .FirstOrDefaultAsync(w => w.Id == dto.SourceId.Value && w.TenantId == tenantId)
                ?? throw new KeyNotFoundException("Kaynak workflow bulunamadı.");

            contentJson = workflow.GraphData;
        }

        var entity = new MarketplaceItemEntity
        {
            TenantId = tenantId,
            PublishedByUserId = userId,
            ItemType = Enum.Parse<MarketplaceItemType>(dto.ItemType, true),
            Name = dto.Name,
            Description = dto.Description,
            Icon = dto.Icon,
            Category = dto.Category,
            Tags = dto.Tags.Count > 0
                ? JsonDocument.Parse(JsonSerializer.Serialize(dto.Tags))
                : null,
            ContentJson = contentJson,
            Version = dto.Version
        };

        _db.MarketplaceItems.Add(entity);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Marketplace öğesi yayınlandı: {Name} (Tenant: {TenantId})", dto.Name, tenantId);
        return await MapToDto(entity, userId);
    }

    /// <summary>
    /// Marketplace öğesini kaldır.
    /// </summary>
    public async Task UnpublishAsync(Guid id)
    {
        var tenantId = _userContext.TenantId ?? "default";
        var entity = await _db.MarketplaceItems
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Marketplace öğesi bulunamadı.");

        _db.MarketplaceItems.Remove(entity);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Marketplace'de arama yap (tenant-scoped).
    /// </summary>
    public async Task<MarketplaceSearchResultDto> SearchAsync(MarketplaceSearchDto search)
    {
        var tenantId = _userContext.TenantId ?? "default";
        var userId = _userContext.CurrentUserId;

        var query = _db.MarketplaceItems
            .Where(i => i.TenantId == tenantId && i.IsPublished);

        // Filtreler
        if (!string.IsNullOrEmpty(search.Query))
        {
            var q = search.Query.ToLower();
            query = query.Where(i =>
                i.Name.ToLower().Contains(q) ||
                (i.Description != null && i.Description.ToLower().Contains(q)));
        }

        if (!string.IsNullOrEmpty(search.Category))
            query = query.Where(i => i.Category == search.Category);

        if (!string.IsNullOrEmpty(search.ItemType))
        {
            var itemType = Enum.Parse<MarketplaceItemType>(search.ItemType, true);
            query = query.Where(i => i.ItemType == itemType);
        }

        // Sıralama
        query = search.SortBy switch
        {
            "popular" => query.OrderByDescending(i => i.DownloadCount),
            "rating" => query.OrderByDescending(i => i.AverageRating),
            _ => query.OrderByDescending(i => i.CreatedAt)
        };

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .Include(i => i.PublishedByUser)
            .Include(i => i.Reviews)
            .ToListAsync();

        var dtos = new List<MarketplaceItemDto>();
        foreach (var item in items)
        {
            dtos.Add(await MapToDto(item, userId));
        }

        return new MarketplaceSearchResultDto
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = search.Page,
            PageSize = search.PageSize
        };
    }

    /// <summary>
    /// Marketplace öğesi detayını getir.
    /// </summary>
    public async Task<MarketplaceItemDto?> GetItemAsync(Guid id)
    {
        var tenantId = _userContext.TenantId ?? "default";
        var userId = _userContext.CurrentUserId;

        var entity = await _db.MarketplaceItems
            .Include(i => i.PublishedByUser)
            .Include(i => i.Reviews)
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId);

        return entity == null ? null : await MapToDto(entity, userId);
    }

    /// <summary>
    /// Marketplace öğesini kur — custom node tanımını klonla veya template'i içe aktar.
    /// </summary>
    public async Task InstallAsync(Guid itemId)
    {
        var tenantId = _userContext.TenantId ?? "default";
        var userId = _userContext.CurrentUserId;

        var item = await _db.MarketplaceItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Marketplace öğesi bulunamadı.");

        // Zaten kurulu mu?
        var alreadyInstalled = await _db.MarketplaceInstalls
            .AnyAsync(i => i.ItemId == itemId && i.UserId == userId);
        if (alreadyInstalled)
            throw new InvalidOperationException("Bu öğe zaten kurulu.");

        // Kurulumu kaydet
        _db.MarketplaceInstalls.Add(new MarketplaceInstallEntity
        {
            ItemId = itemId,
            UserId = userId,
            TenantId = tenantId,
            InstalledVersion = item.Version
        });

        // İndirme sayısını artır
        item.DownloadCount++;
        item.UpdatedAt = DateTime.UtcNow;

        // Custom node ise klonla
        if (item.ItemType == MarketplaceItemType.CustomNode && item.ContentJson != null)
        {
            try
            {
                var content = item.ContentJson.RootElement;
                var nodeType = content.GetProperty("Type").GetString() ?? $"custom.installed.{Guid.NewGuid():N[..8]}";

                // Aynı tipte node var mı kontrol et
                var exists = await _db.CustomNodeDefinitions
                    .AnyAsync(n => n.TenantId == tenantId && n.Type == nodeType);

                if (!exists)
                {
                    var scriptTypeStr = content.TryGetProperty("ScriptType", out var st) ? st.GetString() : "JavaScript";

                    _db.CustomNodeDefinitions.Add(new CustomNodeDefinitionEntity
                    {
                        TenantId = tenantId,
                        CreatedByUserId = userId,
                        Type = nodeType,
                        Name = content.TryGetProperty("Name", out var n) ? n.GetString() ?? item.Name : item.Name,
                        Icon = content.TryGetProperty("Icon", out var ic) ? ic.GetString() ?? "puzzle" : "puzzle",
                        Category = content.TryGetProperty("Category", out var cat) ? cat.GetString() ?? "Özel" : "Özel",
                        Description = content.TryGetProperty("Description", out var desc) ? desc.GetString() : null,
                        Color = content.TryGetProperty("Color", out var col) ? col.GetString() ?? "#89b4fa" : "#89b4fa",
                        InputsJson = content.TryGetProperty("Inputs", out var inp) ? JsonDocument.Parse(inp.GetRawText()) : null,
                        OutputsJson = content.TryGetProperty("Outputs", out var outp) ? JsonDocument.Parse(outp.GetRawText()) : null,
                        ParametersJson = content.TryGetProperty("Parameters", out var par) ? JsonDocument.Parse(par.GetRawText()) : null,
                        ScriptType = Enum.TryParse<CustomNodeScriptType>(scriptTypeStr, true, out var sType) ? sType : CustomNodeScriptType.JavaScript,
                        ScriptCode = content.TryGetProperty("ScriptCode", out var sc) ? sc.GetString() ?? "" : ""
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Custom node klonlama hatası, marketplace install kaydı oluşturuldu ancak node kopyalanamadı");
            }
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Marketplace öğesi kurulumunu kaldır.
    /// </summary>
    public async Task UninstallAsync(Guid itemId)
    {
        var userId = _userContext.CurrentUserId;

        var install = await _db.MarketplaceInstalls
            .FirstOrDefaultAsync(i => i.ItemId == itemId && i.UserId == userId)
            ?? throw new KeyNotFoundException("Kurulum bulunamadı.");

        _db.MarketplaceInstalls.Remove(install);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Değerlendirme ekle.
    /// </summary>
    public async Task<MarketplaceReviewDto> AddReviewAsync(Guid itemId, CreateReviewDto dto)
    {
        var tenantId = _userContext.TenantId ?? "default";
        var userId = _userContext.CurrentUserId;

        var item = await _db.MarketplaceItems
            .Include(i => i.Reviews)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.TenantId == tenantId)
            ?? throw new KeyNotFoundException("Marketplace öğesi bulunamadı.");

        // Mevcut değerlendirme var mı?
        var existing = await _db.MarketplaceReviews
            .FirstOrDefaultAsync(r => r.ItemId == itemId && r.UserId == userId);

        if (existing != null)
        {
            // Güncelle
            existing.Rating = Math.Clamp(dto.Rating, 1, 5);
            existing.Comment = dto.Comment;
            existing.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            // Yeni oluştur
            existing = new MarketplaceReviewEntity
            {
                ItemId = itemId,
                UserId = userId,
                TenantId = tenantId,
                Rating = Math.Clamp(dto.Rating, 1, 5),
                Comment = dto.Comment
            };
            _db.MarketplaceReviews.Add(existing);
        }

        // Ortalama puanı güncelle
        await _db.SaveChangesAsync();
        var avgRating = await _db.MarketplaceReviews
            .Where(r => r.ItemId == itemId)
            .AverageAsync(r => r.Rating);
        item.AverageRating = avgRating;
        await _db.SaveChangesAsync();

        var user = await _db.Users.FindAsync(userId);
        return new MarketplaceReviewDto
        {
            Id = existing.Id,
            UserName = user?.DisplayName ?? "Anonim",
            Rating = existing.Rating,
            Comment = existing.Comment,
            CreatedAt = existing.CreatedAt
        };
    }

    /// <summary>
    /// Bir öğenin değerlendirmelerini getir.
    /// </summary>
    public async Task<List<MarketplaceReviewDto>> GetReviewsAsync(Guid itemId)
    {
        var reviews = await _db.MarketplaceReviews
            .Where(r => r.ItemId == itemId)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return reviews.Select(r => new MarketplaceReviewDto
        {
            Id = r.Id,
            UserName = r.User?.DisplayName ?? "Anonim",
            Rating = r.Rating,
            Comment = r.Comment,
            CreatedAt = r.CreatedAt
        }).ToList();
    }

    /// <summary>
    /// Marketplace kategori listesi.
    /// </summary>
    public async Task<List<string>> GetCategoriesAsync()
    {
        var tenantId = _userContext.TenantId ?? "default";
        return await _db.MarketplaceItems
            .Where(i => i.TenantId == tenantId && i.IsPublished)
            .Select(i => i.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }

    // ─── Private Helpers ─────────────────────────────────────────

    private async Task<MarketplaceItemDto> MapToDto(MarketplaceItemEntity entity, Guid userId)
    {
        var isInstalled = await _db.MarketplaceInstalls
            .AnyAsync(i => i.ItemId == entity.Id && i.UserId == userId);

        var tags = new List<string>();
        if (entity.Tags != null)
        {
            try { tags = JsonSerializer.Deserialize<List<string>>(entity.Tags.RootElement.GetRawText()) ?? []; }
            catch { /* ignore */ }
        }

        return new MarketplaceItemDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            PublishedByUserName = entity.PublishedByUser?.DisplayName ?? "Bilinmiyor",
            ItemType = entity.ItemType.ToString(),
            Name = entity.Name,
            Description = entity.Description,
            Icon = entity.Icon,
            Category = entity.Category,
            Tags = tags,
            Version = entity.Version,
            DownloadCount = entity.DownloadCount,
            AverageRating = entity.AverageRating,
            ReviewCount = entity.Reviews?.Count ?? 0,
            IsInstalled = isInstalled,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
