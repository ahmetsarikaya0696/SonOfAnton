using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;
using SonOfAnton.Shared.Models;

namespace SonOfAnton.Backend.Services;

/// <summary>
/// Custom node tanımları için CRUD servisi.
/// Tüm işlemler tenant-scoped'dır.
/// </summary>
public class CustomNodeService
{
    private readonly SonOfAntonDbContext _db;
    private readonly IUserContext _userContext;
    private readonly ILogger<CustomNodeService> _logger;

    public CustomNodeService(SonOfAntonDbContext db, IUserContext userContext, ILogger<CustomNodeService> logger)
    {
        _db = db;
        _userContext = userContext;
        _logger = logger;
    }

    /// <summary>
    /// Yeni custom node tanımı oluşturur.
    /// </summary>
    public async Task<CustomNodeDto> CreateAsync(CreateCustomNodeDto dto)
    {
        var tenantId = _userContext.TenantId ?? "default";
        var nodeType = dto.Type.StartsWith("custom.") ? dto.Type : $"custom.{dto.Type}";

        // Tip çakışması kontrolü
        var exists = await _db.CustomNodeDefinitions
            .AnyAsync(n => n.TenantId == tenantId && n.Type == nodeType);
        if (exists)
            throw new InvalidOperationException($"'{nodeType}' tipi bu tenant'ta zaten mevcut.");

        var entity = new CustomNodeDefinitionEntity
        {
            TenantId = tenantId,
            CreatedByUserId = _userContext.CurrentUserId,
            Type = nodeType,
            Name = dto.Name,
            Icon = dto.Icon,
            Category = dto.Category,
            Description = dto.Description,
            Color = dto.Color,
            InputsJson = SerializeToJsonDocument(dto.Inputs),
            OutputsJson = SerializeToJsonDocument(dto.Outputs),
            ParametersJson = SerializeToJsonDocument(dto.Parameters),
            ScriptType = Enum.Parse<CustomNodeScriptType>(dto.ScriptType, true),
            ScriptCode = dto.ScriptCode
        };

        _db.CustomNodeDefinitions.Add(entity);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Custom node oluşturuldu: {Type} (Tenant: {TenantId})", nodeType, tenantId);
        return MapToDto(entity);
    }

    /// <summary>
    /// Custom node tanımını günceller ve versiyonu artırır.
    /// </summary>
    public async Task<CustomNodeDto> UpdateAsync(Guid id, CreateCustomNodeDto dto)
    {
        var tenantId = _userContext.TenantId ?? "default";
        var entity = await _db.CustomNodeDefinitions
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId)
            ?? throw new KeyNotFoundException($"Custom node bulunamadı: {id}");

        entity.Name = dto.Name;
        entity.Icon = dto.Icon;
        entity.Category = dto.Category;
        entity.Description = dto.Description;
        entity.Color = dto.Color;
        entity.InputsJson = SerializeToJsonDocument(dto.Inputs);
        entity.OutputsJson = SerializeToJsonDocument(dto.Outputs);
        entity.ParametersJson = SerializeToJsonDocument(dto.Parameters);
        entity.ScriptType = Enum.Parse<CustomNodeScriptType>(dto.ScriptType, true);
        entity.ScriptCode = dto.ScriptCode;
        entity.Version++;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapToDto(entity);
    }

    /// <summary>
    /// Custom node tanımını siler.
    /// </summary>
    public async Task DeleteAsync(Guid id)
    {
        var tenantId = _userContext.TenantId ?? "default";
        var entity = await _db.CustomNodeDefinitions
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId)
            ?? throw new KeyNotFoundException($"Custom node bulunamadı: {id}");

        _db.CustomNodeDefinitions.Remove(entity);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Tenant'a ait tüm custom node tanımlarını döndürür.
    /// </summary>
    public async Task<List<CustomNodeDto>> GetByTenantAsync()
    {
        var tenantId = _userContext.TenantId ?? "default";
        var entities = await _db.CustomNodeDefinitions
            .Where(n => n.TenantId == tenantId)
            .OrderBy(n => n.Category)
            .ThenBy(n => n.Name)
            .ToListAsync();

        return entities.Select(MapToDto).ToList();
    }

    /// <summary>
    /// ID ile custom node tanımı döndürür.
    /// </summary>
    public async Task<CustomNodeDto?> GetByIdAsync(Guid id)
    {
        var tenantId = _userContext.TenantId ?? "default";
        var entity = await _db.CustomNodeDefinitions
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId);
        return entity == null ? null : MapToDto(entity);
    }

    /// <summary>
    /// Node tipine göre custom node tanımını entity olarak döndürür.
    /// WorkflowRunner tarafından kullanılır.
    /// </summary>
    public async Task<CustomNodeDefinitionEntity?> GetEntityByTypeAsync(string tenantId, string type)
    {
        return await _db.CustomNodeDefinitions
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Type == type);
    }

    /// <summary>
    /// Custom node tanımlarını NodeDefinition formatına çevirir (palette'e eklemek için).
    /// </summary>
    public async Task<List<NodeDefinition>> GetAsNodeDefinitionsAsync(string tenantId)
    {
        var entities = await _db.CustomNodeDefinitions
            .Where(n => n.TenantId == tenantId)
            .ToListAsync();

        return entities.Select(e => new NodeDefinition
        {
            Type = e.Type,
            Name = e.Name,
            Icon = e.Icon,
            Category = e.Category,
            Description = e.Description ?? string.Empty,
            Color = e.Color,
            Inputs = DeserializeList<NodePort>(e.InputsJson),
            Outputs = DeserializeList<NodePort>(e.OutputsJson),
            Parameters = DeserializeList<NodeParameter>(e.ParametersJson)
        }).ToList();
    }

    // ─── Private Helpers ─────────────────────────────────────────

    private static CustomNodeDto MapToDto(CustomNodeDefinitionEntity entity) => new()
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        CreatedByUserId = entity.CreatedByUserId,
        Type = entity.Type,
        Name = entity.Name,
        Icon = entity.Icon,
        Category = entity.Category,
        Description = entity.Description,
        Color = entity.Color,
        Inputs = DeserializeList<NodePortDto>(entity.InputsJson),
        Outputs = DeserializeList<NodePortDto>(entity.OutputsJson),
        Parameters = DeserializeList<NodeParameterDto>(entity.ParametersJson),
        ScriptType = entity.ScriptType.ToString(),
        ScriptCode = entity.ScriptCode,
        Version = entity.Version,
        IsPublished = entity.IsPublished,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static JsonDocument? SerializeToJsonDocument<T>(List<T> list)
    {
        if (list.Count == 0) return null;
        var json = JsonSerializer.Serialize(list);
        return JsonDocument.Parse(json);
    }

    private static List<T> DeserializeList<T>(JsonDocument? doc)
    {
        if (doc == null) return [];
        try
        {
            return JsonSerializer.Deserialize<List<T>>(doc.RootElement.GetRawText()) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
