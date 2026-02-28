using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Entities;
using SonOfAnton.Shared.Models;

namespace SonOfAnton.Backend.Services.DataStore;

public class DataTableService : IDataTableService
{
    private readonly SonOfAntonDbContext _dbContext;

    public DataTableService(SonOfAntonDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<DataTableDto>> GetTablesAsync(string tenantId, Guid? workspaceId)
    {
        var query = _dbContext.DataTables.Where(t => t.TenantId == tenantId);

        if (workspaceId.HasValue)
        {
            query = query.Where(t => t.WorkspaceId == workspaceId);
        }

        var tables = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

        var tableIds = tables.Select(t => t.Id).ToList();
        var rowCounts = await _dbContext.DataRows
            .Where(r => tableIds.Contains(r.DataTableId))
            .GroupBy(r => r.DataTableId)
            .Select(g => new { TableId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TableId, x => x.Count);

        return tables.Select(t =>
        {
            var dto = MapToDto(t);
            dto.RowCount = rowCounts.GetValueOrDefault(t.Id, 0);
            return dto;
        }).ToList();
    }

    public async Task<DataTableDto?> GetTableAsync(Guid id, string tenantId)
    {
        var entity = await _dbContext.DataTables
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);

        if (entity == null) return null;

        var dto = MapToDto(entity);
        dto.RowCount = await _dbContext.DataRows.CountAsync(r => r.DataTableId == id);
        return dto;
    }

    public async Task<DataTableDto> CreateTableAsync(DataTableCreateDto dto, string tenantId, Guid? workspaceId, Guid? userId)
    {
        var entity = new DataTableEntity
        {
            Name = dto.Name,
            Description = dto.Description,
            ColumnsJson = dto.ColumnsJson,
            TenantId = tenantId,
            WorkspaceId = workspaceId,
            CreatedByUserId = userId
        };

        _dbContext.DataTables.Add(entity);
        await _dbContext.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<DataTableDto> UpdateTableAsync(Guid id, DataTableUpdateDto dto, string tenantId)
    {
        var entity = await _dbContext.DataTables
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);

        if (entity == null) throw new InvalidOperationException("Table not found");

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.ColumnsJson = dto.ColumnsJson;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<bool> DeleteTableAsync(Guid id, string tenantId)
    {
        var entity = await _dbContext.DataTables
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);

        if (entity == null) return false;

        _dbContext.DataTables.Remove(entity);
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<List<DataRowDto>> GetRowsAsync(Guid tableId, string tenantId)
    {
        // Yetki kontrolü (table bu tenanta ait mi)
        var hasAccess = await _dbContext.DataTables.AnyAsync(t => t.Id == tableId && t.TenantId == tenantId);
        if (!hasAccess) return [];

        var rows = await _dbContext.DataRows
            .AsNoTracking()
            .Where(r => r.DataTableId == tableId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return rows.Select(MapToDto).ToList();
    }

    public async Task<DataRowDto> CreateRowAsync(Guid tableId, DataRowCreateDto dto, string tenantId)
    {
        var hasAccess = await _dbContext.DataTables.AnyAsync(t => t.Id == tableId && t.TenantId == tenantId);
        if (!hasAccess) throw new UnauthorizedAccessException();

        var row = new DataRowEntity
        {
            DataTableId = tableId,
            ValuesJson = dto.ValuesJson
        };

        _dbContext.DataRows.Add(row);
        await _dbContext.SaveChangesAsync();

        return MapToDto(row);
    }

    public async Task<DataRowDto> UpdateRowAsync(Guid rowId, DataRowUpdateDto dto, string tenantId)
    {
        var row = await _dbContext.DataRows
            .Include(r => r.DataTable)
            .FirstOrDefaultAsync(r => r.Id == rowId);

        if (row == null || row.DataTable.TenantId != tenantId) throw new UnauthorizedAccessException();

        row.ValuesJson = dto.ValuesJson;
        row.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return MapToDto(row);
    }

    public async Task<bool> DeleteRowAsync(Guid rowId, string tenantId)
    {
        var row = await _dbContext.DataRows
            .Include(r => r.DataTable)
            .FirstOrDefaultAsync(r => r.Id == rowId);

        if (row == null || row.DataTable.TenantId != tenantId) return false;

        _dbContext.DataRows.Remove(row);
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<Guid> ImportCsvAsync(Stream csvStream, string fileName, string tenantId, Guid? workspaceId, Guid? userId, Guid? existingTableId = null)
    {
        using var reader = new StreamReader(csvStream);
        
        var headerLine = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(headerLine)) throw new InvalidOperationException("CSV dosyası boş veya başlık satırı yok.");

        var headers = headerLine.Split(',').Select(h => h.Trim('"').Trim()).ToList();
        
        DataTableEntity table;
        List<DataTableColumnDefinition> columns;

        if (existingTableId.HasValue)
        {
            table = await _dbContext.DataTables.FirstOrDefaultAsync(t => t.Id == existingTableId.Value && t.TenantId == tenantId) 
                    ?? throw new InvalidOperationException("Tablo bulunamadı");
                    
            if (table.ColumnsJson != null)
            {
                columns = JsonSerializer.Deserialize<List<DataTableColumnDefinition>>(table.ColumnsJson.RootElement.GetRawText()) ?? [];
            }
            else
            {
                columns = [];
            }
        }
        else
        {
            columns = headers.Select(h => new DataTableColumnDefinition
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Name = h,
                Type = "text" // Default text
            }).ToList();

            var columnsJsonText = JsonSerializer.Serialize(columns);

            table = new DataTableEntity
            {
                Name = Path.GetFileNameWithoutExtension(fileName),
                TenantId = tenantId,
                WorkspaceId = workspaceId,
                CreatedByUserId = userId,
                ColumnsJson = JsonDocument.Parse(columnsJsonText)
            };
            
            _dbContext.DataTables.Add(table);
            await _dbContext.SaveChangesAsync();
        }

        // Satırları okuyup map edelim
        var rowsToAdd = new List<DataRowEntity>();
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Basit split, tırnak içindeki virgülleri tam desteklemez, ama ek framework kullanmamak için kısıtlı.
            var values = line.Split(',').Select(v => v.Trim('"').Trim()).ToList();
            
            var rowDict = new Dictionary<string, object?>();
            
            for (int i = 0; i < headers.Count; i++)
            {
                if (i >= values.Count) break;
                
                var headerName = headers[i];
                var matchedCol = columns.FirstOrDefault(c => c.Name.Equals(headerName, StringComparison.OrdinalIgnoreCase));
                
                if (matchedCol != null)
                {
                    rowDict[matchedCol.Id] = values[i];
                }
            }

            if (rowDict.Any())
            {
                var dictJson = JsonSerializer.Serialize(rowDict);
                rowsToAdd.Add(new DataRowEntity
                {
                    DataTableId = table.Id,
                    ValuesJson = JsonDocument.Parse(dictJson)
                });
            }
        }

        if (rowsToAdd.Any())
        {
            _dbContext.DataRows.AddRange(rowsToAdd);
            await _dbContext.SaveChangesAsync();
        }

        return table.Id;
    }

    public async Task<Stream> ExportCsvAsync(Guid tableId, string tenantId)
    {
        var table = await _dbContext.DataTables.FirstOrDefaultAsync(t => t.Id == tableId && t.TenantId == tenantId);
        if (table == null) throw new InvalidOperationException("Tablo bulunamadı.");

        var rows = await _dbContext.DataRows.AsNoTracking().Where(r => r.DataTableId == tableId).OrderBy(r => r.CreatedAt).ToListAsync();

        var columns = new List<DataTableColumnDefinition>();
        if (table.ColumnsJson != null)
        {
             columns = JsonSerializer.Deserialize<List<DataTableColumnDefinition>>(table.ColumnsJson.RootElement.GetRawText()) ?? [];
        }

        var memoryStream = new MemoryStream();
        var writer = new StreamWriter(memoryStream, Encoding.UTF8);

        // Headers
        var headerLine = string.Join(",", columns.Select(c => $"\"{c.Name}\""));
        await writer.WriteLineAsync(headerLine);

        // Lines
        foreach (var row in rows)
        {
            var rowDict = new Dictionary<string, JsonElement>();
            if (row.ValuesJson != null)
            {
                rowDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.ValuesJson.RootElement.GetRawText()) ?? [];
            }

            var lineValues = new List<string>();
            foreach (var col in columns)
            {
                if (rowDict.TryGetValue(col.Id, out var jsonVal))
                {
                    var valStr = jsonVal.ValueKind == JsonValueKind.String ? jsonVal.GetString() : jsonVal.GetRawText();
                    valStr ??= "";
                    lineValues.Add($"\"{valStr.Replace("\"", "\"\"")}\""); // Escape quotes
                }
                else
                {
                    lineValues.Add("\"\"");
                }
            }

            await writer.WriteLineAsync(string.Join(",", lineValues));
        }

        await writer.FlushAsync();
        memoryStream.Position = 0;

        return memoryStream;
    }

    private static DataTableDto MapToDto(DataTableEntity t)
    {
        return new DataTableDto
        {
            Id = t.Id,
            Name = t.Name,
            Description = t.Description,
            ColumnsJson = t.ColumnsJson,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        };
    }

    private static DataRowDto MapToDto(DataRowEntity r)
    {
        return new DataRowDto
        {
            Id = r.Id,
            DataTableId = r.DataTableId,
            ValuesJson = r.ValuesJson,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        };
    }
}
