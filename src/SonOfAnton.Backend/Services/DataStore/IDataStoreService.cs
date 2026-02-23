namespace SonOfAnton.Backend.Services.DataStore;

using SonOfAnton.Shared.Entities;

/// <summary>
/// Data Store servisi — SQL bilgisi gerektirmeden Key-Value/JSON depolama
/// </summary>
public interface IDataStoreService
{
    // ─── Store CRUD ─────────────────────────────────────────────
    Task<DataStoreEntity> CreateStoreAsync(string name, string? description, string? tenantId);
    Task<DataStoreEntity?> GetStoreAsync(Guid storeId);
    Task<DataStoreEntity?> GetStoreByNameAsync(string name, string? tenantId);
    Task<List<DataStoreEntity>> GetAllStoresAsync(string? tenantId);
    Task<bool> DeleteStoreAsync(Guid storeId);

    // ─── Entry CRUD (SQL-free key-value ops) ────────────────────
    Task<DataStoreEntryEntity> SetEntryAsync(Guid storeId, string key, object? value);
    Task<DataStoreEntryEntity?> GetEntryAsync(Guid storeId, string key);
    Task<List<DataStoreEntryEntity>> GetAllEntriesAsync(Guid storeId);
    Task<bool> DeleteEntryAsync(Guid storeId, string key);
}
