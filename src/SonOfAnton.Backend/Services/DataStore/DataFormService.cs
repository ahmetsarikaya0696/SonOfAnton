using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Entities;
using SonOfAnton.Shared.Models;

namespace SonOfAnton.Backend.Services.DataStore;

public class DataFormService : IDataFormService
{
    private readonly SonOfAntonDbContext _dbContext;

    public DataFormService(SonOfAntonDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<DataFormDto>> GetFormsForTableAsync(Guid tableId, string tenantId)
    {
        var entities = await _dbContext.DataForms
            .AsNoTracking()
            .Where(f => f.DataTableId == tableId && f.TenantId == tenantId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return entities.Select(MapToDto).ToList();
    }

    public async Task<DataFormDto?> GetFormAsync(Guid id, string tenantId)
    {
        var entity = await _dbContext.DataForms
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId);

        return entity != null ? MapToDto(entity) : null;
    }

    public async Task<DataFormDto> CreateFormAsync(DataFormCreateDto dto, string tenantId, Guid? workspaceId)
    {
        var entity = new DataFormEntity
        {
            DataTableId = dto.DataTableId,
            Name = dto.Name,
            Description = dto.Description,
            Slug = Guid.NewGuid().ToString("N")[..12],
            FieldsJson = dto.FieldsJson,
            ThemeJson = dto.ThemeJson,
            IsActive = dto.IsActive,
            TenantId = tenantId,
            WorkspaceId = workspaceId
        };

        _dbContext.DataForms.Add(entity);
        await _dbContext.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<DataFormDto> UpdateFormAsync(Guid id, DataFormUpdateDto dto, string tenantId)
    {
        var entity = await _dbContext.DataForms
            .FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId);

        if (entity == null) throw new InvalidOperationException("Form not found");

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.FieldsJson = dto.FieldsJson;
        entity.ThemeJson = dto.ThemeJson;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<bool> DeleteFormAsync(Guid id, string tenantId)
    {
        var entity = await _dbContext.DataForms
            .FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId);

        if (entity == null) return false;

        _dbContext.DataForms.Remove(entity);
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<DataFormDto?> GetFormBySlugAsync(string slug)
    {
        var entity = await _dbContext.DataForms
            .AsNoTracking()
            .Include(f => f.DataTable)
            .FirstOrDefaultAsync(f => f.Slug == slug && f.IsActive);

        return entity != null ? MapToDto(entity) : null;
    }

    public async Task<bool> SubmitFormAsync(string slug, DataFormSubmitDto dto)
    {
        var form = await _dbContext.DataForms
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Slug == slug && f.IsActive);

        if (form == null) return false;

        var jsonStr = JsonSerializer.Serialize(dto.Values);
            
        var row = new DataRowEntity
        {
            DataTableId = form.DataTableId,
            ValuesJson = JsonDocument.Parse(jsonStr)
        };

        _dbContext.DataRows.Add(row);
        await _dbContext.SaveChangesAsync();

        return true;
    }

    private static DataFormDto MapToDto(DataFormEntity f)
    {
        return new DataFormDto
        {
            Id = f.Id,
            DataTableId = f.DataTableId,
            Name = f.Name,
            Description = f.Description,
            Slug = f.Slug,
            FieldsJson = f.FieldsJson,
            ThemeJson = f.ThemeJson,
            IsActive = f.IsActive,
            CreatedAt = f.CreatedAt
        };
    }
}
