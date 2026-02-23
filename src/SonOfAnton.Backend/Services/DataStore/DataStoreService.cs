using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Entities;

namespace SonOfAnton.Backend.Services.DataStore;

/// <summary>
/// Data Store servisi implementasyonu — EF Core ile Key-Value/JSON CRUD
/// </summary>
public class DataStoreService : IDataStoreService
{
    private readonly SonOfAntonDbContext _db;
    private readonly ILogger<DataStoreService> _logger;

    public DataStoreService(SonOfAntonDbContext db, ILogger<DataStoreService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ─── Store CRUD ─────────────────────────────────────────────

    public async Task<DataStoreEntity> CreateStoreAsync(string name, string? description, string? tenantId)
    {
        var store = new DataStoreEntity
        {
            Name = name,
            Description = description,
            TenantId = tenantId
        };

        _db.DataStores.Add(store);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Data Store oluşturuldu: {Name} (ID={Id})", name, store.Id);
        return store;
    }

    public async Task<DataStoreEntity?> GetStoreAsync(Guid storeId)
    {
        return await _db.DataStores
            .Include(s => s.Entries)
            .FirstOrDefaultAsync(s => s.Id == storeId);
    }

    public async Task<DataStoreEntity?> GetStoreByNameAsync(string name, string? tenantId)
    {
        return await _db.DataStores
            .FirstOrDefaultAsync(s => s.Name == name && s.TenantId == tenantId);
    }

    public async Task<List<DataStoreEntity>> GetAllStoresAsync(string? tenantId)
    {
        var query = _db.DataStores.AsQueryable();
        
        if (tenantId != null)
            query = query.Where(s => s.TenantId == tenantId);

        return await query
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();
    }

    public async Task<bool> DeleteStoreAsync(Guid storeId)
    {
        var store = await _db.DataStores.FindAsync(storeId);
        if (store == null) return false;

        _db.DataStores.Remove(store);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Data Store silindi: {Name} (ID={Id})", store.Name, store.Id);
        return true;
    }

    // ─── Entry CRUD ─────────────────────────────────────────────

    public async Task<DataStoreEntryEntity> SetEntryAsync(Guid storeId, string key, object? value)
    {
        var jsonValue = value != null
            ? JsonDocument.Parse(JsonSerializer.Serialize(value))
            : null;

        var existing = await _db.DataStoreEntries
            .FirstOrDefaultAsync(e => e.DataStoreId == storeId && e.Key == key);

        if (existing != null)
        {
            // Upsert: güncelle
            existing.Value = jsonValue;
            existing.UpdatedAt = DateTime.UtcNow;
            _db.DataStoreEntries.Update(existing);
        }
        else
        {
            // Yeni kayıt
            existing = new DataStoreEntryEntity
            {
                DataStoreId = storeId,
                Key = key,
                Value = jsonValue
            };
            _db.DataStoreEntries.Add(existing);
        }

        // Store'un UpdatedAt'ini de güncelle
        var store = await _db.DataStores.FindAsync(storeId);
        if (store != null)
        {
            store.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<DataStoreEntryEntity?> GetEntryAsync(Guid storeId, string key)
    {
        return await _db.DataStoreEntries
            .FirstOrDefaultAsync(e => e.DataStoreId == storeId && e.Key == key);
    }

    public async Task<List<DataStoreEntryEntity>> GetAllEntriesAsync(Guid storeId)
    {
        return await _db.DataStoreEntries
            .Where(e => e.DataStoreId == storeId)
            .OrderBy(e => e.Key)
            .ToListAsync();
    }

    public async Task<bool> DeleteEntryAsync(Guid storeId, string key)
    {
        var entry = await _db.DataStoreEntries
            .FirstOrDefaultAsync(e => e.DataStoreId == storeId && e.Key == key);

        if (entry == null) return false;

        _db.DataStoreEntries.Remove(entry);
        await _db.SaveChangesAsync();
        return true;
    }
}
