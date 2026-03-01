using SonOfAnton.Shared.Models;

namespace SonOfAnton.Backend.Services.DataStore;

public interface IDataTableService
{
    // ─── Tables ───────────────────────────────────────────────────────
    Task<List<DataTableDto>> GetTablesAsync(string tenantId, Guid? workspaceId);
    Task<DataTableDto?> GetTableAsync(Guid id, string tenantId);
    Task<DataTableDto> CreateTableAsync(DataTableCreateDto dto, string tenantId, Guid? workspaceId, Guid? userId);
    Task<DataTableDto> UpdateTableAsync(Guid id, DataTableUpdateDto dto, string tenantId);
    Task<bool> DeleteTableAsync(Guid id, string tenantId);

    // ─── Rows ─────────────────────────────────────────────────────────
    Task<List<DataRowDto>> GetRowsAsync(Guid tableId, string tenantId);
    Task<DataRowDto> CreateRowAsync(Guid tableId, DataRowCreateDto dto, string tenantId);
    Task<DataRowDto> UpdateRowAsync(Guid rowId, DataRowUpdateDto dto, string tenantId);
    Task<bool> DeleteRowAsync(Guid rowId, string tenantId);

    // ─── CSV Import/Export ────────────────────────────────────────────
    Task<Guid> ImportCsvAsync(Stream csvStream, string fileName, string tenantId, Guid? workspaceId, Guid? userId, Guid? existingTableId = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates a CSV stream for a given table ID containing all its rows.
    /// </summary>
    Task<Stream> ExportCsvAsync(Guid tableId, string tenantId, CancellationToken cancellationToken = default);
}
