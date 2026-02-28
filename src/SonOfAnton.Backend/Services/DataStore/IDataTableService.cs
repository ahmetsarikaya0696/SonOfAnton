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
    /// <summary>
    /// Reads a CSV stream, maps headers to column definitions, and creates a table (if tableId is null) or appends rows.
    /// Returns the affected/created DataTable ID.
    /// </summary>
    Task<Guid> ImportCsvAsync(Stream csvStream, string fileName, string tenantId, Guid? workspaceId, Guid? userId, Guid? existingTableId = null);
    
    /// <summary>
    /// Generates a CSV stream for a given table ID containing all its rows.
    /// </summary>
    Task<Stream> ExportCsvAsync(Guid tableId, string tenantId);
}
