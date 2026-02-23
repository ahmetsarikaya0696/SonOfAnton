using SonOfAnton.Backend.Services.DataStore;

namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// State Set node — Data Store'a Key-Value çifti kaydet
/// </summary>
public class StateSetHandler : INodeHandler
{
    private readonly IDataStoreService _dataStoreService;

    public StateSetHandler(IDataStoreService dataStoreService)
    {
        _dataStoreService = dataStoreService;
    }

    public string NodeType => "state.set";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        try
        {
            var storeName = context.Node.Parameters.GetValueOrDefault("storeName")?.ToString();
            var key = context.Node.Parameters.GetValueOrDefault("key")?.ToString();
            var value = context.Node.Parameters.GetValueOrDefault("value");

            if (string.IsNullOrWhiteSpace(storeName))
                return NodeExecutionResult.Fail("storeName parametresi gerekli");
            if (string.IsNullOrWhiteSpace(key))
                return NodeExecutionResult.Fail("key parametresi gerekli");

            // Store'u ismiyle bul veya oluştur
            var store = await _dataStoreService.GetStoreByNameAsync(storeName, null);
            if (store == null)
            {
                store = await _dataStoreService.CreateStoreAsync(storeName, null, null);
                context.Log($"Yeni Data Store oluşturuldu: {storeName}", Shared.Enums.LogLevel.Info, null);
            }

            // Eğer value boşsa, inputData'yı kullan
            var actualValue = value ?? context.InputData;

            var entry = await _dataStoreService.SetEntryAsync(store.Id, key, actualValue);

            context.Log($"State kayıt edildi: {storeName}/{key}", Shared.Enums.LogLevel.Info, new { key, storeName });

            return NodeExecutionResult.Ok(new
            {
                storeName,
                key,
                value = entry.Value?.RootElement.Clone(),
                updatedAt = entry.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Fail($"State Set hatası: {ex.Message}");
        }
    }
}

/// <summary>
/// State Get node — Data Store'dan Key'e göre değer oku
/// </summary>
public class StateGetHandler : INodeHandler
{
    private readonly IDataStoreService _dataStoreService;

    public StateGetHandler(IDataStoreService dataStoreService)
    {
        _dataStoreService = dataStoreService;
    }

    public string NodeType => "state.get";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        try
        {
            var storeName = context.Node.Parameters.GetValueOrDefault("storeName")?.ToString();
            var key = context.Node.Parameters.GetValueOrDefault("key")?.ToString();

            if (string.IsNullOrWhiteSpace(storeName))
                return NodeExecutionResult.Fail("storeName parametresi gerekli");
            if (string.IsNullOrWhiteSpace(key))
                return NodeExecutionResult.Fail("key parametresi gerekli");

            var store = await _dataStoreService.GetStoreByNameAsync(storeName, null);
            if (store == null)
                return NodeExecutionResult.Fail($"Data Store bulunamadı: {storeName}");

            var entry = await _dataStoreService.GetEntryAsync(store.Id, key);
            if (entry == null)
            {
                context.Log($"Key bulunamadı: {storeName}/{key}", Shared.Enums.LogLevel.Warning, null);
                return NodeExecutionResult.Ok(null);
            }

            context.Log($"State okundu: {storeName}/{key}", Shared.Enums.LogLevel.Info, new { key, storeName });

            return NodeExecutionResult.Ok(entry.Value?.RootElement.Clone());
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Fail($"State Get hatası: {ex.Message}");
        }
    }
}

/// <summary>
/// State Delete node — Data Store'dan Key'e göre değer sil
/// </summary>
public class StateDeleteHandler : INodeHandler
{
    private readonly IDataStoreService _dataStoreService;

    public StateDeleteHandler(IDataStoreService dataStoreService)
    {
        _dataStoreService = dataStoreService;
    }

    public string NodeType => "state.delete";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        try
        {
            var storeName = context.Node.Parameters.GetValueOrDefault("storeName")?.ToString();
            var key = context.Node.Parameters.GetValueOrDefault("key")?.ToString();

            if (string.IsNullOrWhiteSpace(storeName))
                return NodeExecutionResult.Fail("storeName parametresi gerekli");
            if (string.IsNullOrWhiteSpace(key))
                return NodeExecutionResult.Fail("key parametresi gerekli");

            var store = await _dataStoreService.GetStoreByNameAsync(storeName, null);
            if (store == null)
                return NodeExecutionResult.Fail($"Data Store bulunamadı: {storeName}");

            var deleted = await _dataStoreService.DeleteEntryAsync(store.Id, key);

            context.Log($"State silindi: {storeName}/{key} (sonuç: {deleted})", Shared.Enums.LogLevel.Info, new { key, storeName, deleted });

            return NodeExecutionResult.Ok(new { key, storeName, deleted });
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Fail($"State Delete hatası: {ex.Message}");
        }
    }
}

/// <summary>
/// State GetAll node — Data Store'daki tüm Key-Value çiftlerini getir
/// </summary>
public class StateGetAllHandler : INodeHandler
{
    private readonly IDataStoreService _dataStoreService;

    public StateGetAllHandler(IDataStoreService dataStoreService)
    {
        _dataStoreService = dataStoreService;
    }

    public string NodeType => "state.getall";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        try
        {
            var storeName = context.Node.Parameters.GetValueOrDefault("storeName")?.ToString();

            if (string.IsNullOrWhiteSpace(storeName))
                return NodeExecutionResult.Fail("storeName parametresi gerekli");

            var store = await _dataStoreService.GetStoreByNameAsync(storeName, null);
            if (store == null)
                return NodeExecutionResult.Fail($"Data Store bulunamadı: {storeName}");

            var entries = await _dataStoreService.GetAllEntriesAsync(store.Id);

            var result = entries.ToDictionary(
                e => e.Key,
                e => (object?)e.Value?.RootElement.Clone()
            );

            context.Log($"Store okundu: {storeName} ({entries.Count} kayıt)", Shared.Enums.LogLevel.Info, new { storeName, count = entries.Count });

            return NodeExecutionResult.Ok(result);
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Fail($"State GetAll hatası: {ex.Message}");
        }
    }
}
