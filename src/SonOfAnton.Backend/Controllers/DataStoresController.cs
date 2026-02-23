using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.Services.DataStore;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Core.Identity;

namespace SonOfAnton.Backend.Controllers;

/// <summary>
/// Data Store CRUD API — SQL bilgisi gerektirmeden Key-Value/JSON depolama
/// </summary>
[ApiController]
[Route("api/datastores")]
public class DataStoresController : ControllerBase
{
    private readonly IDataStoreService _dataStoreService;
    private readonly IUserContext _userContext;

    public DataStoresController(IDataStoreService dataStoreService, IUserContext userContext)
    {
        _dataStoreService = dataStoreService;
        _userContext = userContext;
    }

    // ─── Store Endpoints ────────────────────────────────────────

    /// <summary>Tüm Data Store'ları listele</summary>
    [HttpGet]
    public async Task<ActionResult<List<DataStoreDto>>> GetAllStores()
    {
        var tenantId = _userContext.TenantId;
        var stores = await _dataStoreService.GetAllStoresAsync(tenantId);

        var dtos = stores.Select(s => new DataStoreDto
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            TenantId = s.TenantId,
            EntryCount = s.Entries?.Count ?? 0,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>Tek Data Store detayı</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DataStoreDto>> GetStore(Guid id)
    {
        var store = await _dataStoreService.GetStoreAsync(id);
        if (store == null) return NotFound();

        return Ok(new DataStoreDto
        {
            Id = store.Id,
            Name = store.Name,
            Description = store.Description,
            TenantId = store.TenantId,
            EntryCount = store.Entries?.Count ?? 0,
            CreatedAt = store.CreatedAt,
            UpdatedAt = store.UpdatedAt
        });
    }

    /// <summary>Yeni Data Store oluştur</summary>
    [HttpPost]
    public async Task<ActionResult<DataStoreDto>> CreateStore([FromBody] CreateDataStoreRequest request)
    {
        var tenantId = _userContext.TenantId;
        var store = await _dataStoreService.CreateStoreAsync(request.Name, request.Description, tenantId);

        return CreatedAtAction(nameof(GetStore), new { id = store.Id }, new DataStoreDto
        {
            Id = store.Id,
            Name = store.Name,
            Description = store.Description,
            TenantId = store.TenantId,
            EntryCount = 0,
            CreatedAt = store.CreatedAt,
            UpdatedAt = store.UpdatedAt
        });
    }

    /// <summary>Data Store sil</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteStore(Guid id)
    {
        var deleted = await _dataStoreService.DeleteStoreAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    // ─── Entry Endpoints ────────────────────────────────────────

    /// <summary>Store'daki tüm entry'leri listele</summary>
    [HttpGet("{id:guid}/entries")]
    public async Task<ActionResult<List<DataStoreEntryDto>>> GetAllEntries(Guid id)
    {
        var entries = await _dataStoreService.GetAllEntriesAsync(id);

        var dtos = entries.Select(e => new DataStoreEntryDto
        {
            Id = e.Id,
            Key = e.Key,
            Value = e.Value?.RootElement.Clone(),
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>Key'e göre tek entry al</summary>
    [HttpGet("{id:guid}/entries/{key}")]
    public async Task<ActionResult<DataStoreEntryDto>> GetEntry(Guid id, string key)
    {
        var entry = await _dataStoreService.GetEntryAsync(id, key);
        if (entry == null) return NotFound();

        return Ok(new DataStoreEntryDto
        {
            Id = entry.Id,
            Key = entry.Key,
            Value = entry.Value?.RootElement.Clone(),
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt
        });
    }

    /// <summary>Entry ekle/güncelle (upsert)</summary>
    [HttpPut("{id:guid}/entries")]
    public async Task<ActionResult<DataStoreEntryDto>> SetEntry(Guid id, [FromBody] SetDataStoreEntryRequest request)
    {
        var entry = await _dataStoreService.SetEntryAsync(id, request.Key, request.Value);

        return Ok(new DataStoreEntryDto
        {
            Id = entry.Id,
            Key = entry.Key,
            Value = entry.Value?.RootElement.Clone(),
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt
        });
    }

    /// <summary>Entry sil</summary>
    [HttpDelete("{id:guid}/entries/{key}")]
    public async Task<IActionResult> DeleteEntry(Guid id, string key)
    {
        var deleted = await _dataStoreService.DeleteEntryAsync(id, key);
        return deleted ? NoContent() : NotFound();
    }
}
