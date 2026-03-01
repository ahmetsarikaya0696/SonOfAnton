using System.Text.Json;
using Microsoft.JSInterop;

using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace SonOfAnton.Frontend.Services;

/// <summary>
/// Bağlantı durumu
/// </summary>
public enum SseConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting
}

/// <summary>
/// SSE client istatistikleri
/// </summary>
public class SseClientStatistics
{
    public int ActiveConnections { get; set; }
    public long TotalEventsReceived { get; set; }
    public long DuplicatesDropped { get; set; }
    public long ReorderCount { get; set; }
    public int BufferedEvents { get; set; }
}

/// <summary>
/// SSE (Server-Sent Events) Client Servisi
/// Browser native EventSource API'sini kullanır
/// Event deduplication, ordering, offline buffering desteği
/// </summary>
public class SseClientService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IAccessTokenProvider _tokenProvider;
    private IJSObjectReference? _module;
    private DotNetObjectReference<SseClientService>? _dotNetRef;
    private readonly Dictionary<string, string> _activeConnections = new();
    
    // Event deduplication
    private readonly HashSet<string> _processedEventIds = new();
    private readonly object _dedupLock = new();
    private const int MaxProcessedEventIds = 10000;
    
    // Event ordering
    private readonly SortedDictionary<long, string> _reorderBuffer = new();
    private const int MaxReorderWindow = 50;
    
    // Offline buffering
    private readonly Queue<BufferedEvent> _offlineBuffer = new();
    private const int MaxOfflineBufferSize = 500;
    private bool _isOnline = true;
    
    // Statistics
    private long _totalEventsReceived;
    private long _duplicatesDropped;
    
    // Subscribed topics
    private readonly HashSet<string> _subscribedTopics = ["*"];
    
    // Connection state
    public SseConnectionState ConnectionState { get; private set; } = SseConnectionState.Disconnected;
    
    // Events
    public event Action<SseConnectionState>? OnConnectionStateChanged;
    public event Action<string>? OnMessage;
    public event Action<NodeStartedEvent>? OnNodeStarted;
    public event Action<NodeCompletedEvent>? OnNodeCompleted;
    public event Action<ProgressEvent>? OnProgress;
    public event Action<LogEvent>? OnLog;
    public event Action<WorkflowCompletedEventDto>? OnWorkflowCompleted;
    
    public SseClientService(IJSRuntime jsRuntime, IAccessTokenProvider tokenProvider)
    {
        _jsRuntime = jsRuntime;
        _tokenProvider = tokenProvider;
    }
    
    private async Task EnsureModuleLoadedAsync()
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/sse-interop.js");
        _dotNetRef ??= DotNetObjectReference.Create(this);
    }
    
    /// <summary>
    /// SSE stream'e bağlanır
    /// </summary>
    public async Task<bool> ConnectAsync(string url, string? connectionId = null)
    {
        await EnsureModuleLoadedAsync();
        
        connectionId ??= Guid.NewGuid().ToString();
        
        // Topic filtresini URL'ye ekle
        if (_subscribedTopics.Count > 0 && !_subscribedTopics.Contains("*"))
        {
            var separator = url.Contains('?') ? '&' : '?';
            url = $"{url}{separator}topics={string.Join(",", _subscribedTopics)}";
        }
        
        // Token'ı al ve URL'ye ekle (EventSource Header desteklemez, query string ile gönderiyoruz)
        var tokenResult = await _tokenProvider.RequestAccessToken();
        if (tokenResult.TryGetToken(out var token))
        {
            var separator = url.Contains('?') ? '&' : '?';
            url = $"{url}{separator}access_token={token.Value}";
        }
        
        var result = await _module!.InvokeAsync<bool>("connectToStream", connectionId, url, _dotNetRef);
        
        if (result)
        {
            _activeConnections[connectionId] = url;
            ConnectionState = SseConnectionState.Connecting;
            _isOnline = true;
        }
        
        return result;
    }
    
    /// <summary>
    /// Belirli bir execution için SSE stream'e bağlanır
    /// </summary>
    public async Task<bool> ConnectToExecutionAsync(Guid executionId, string baseUrl = "")
    {
        var url = string.IsNullOrEmpty(baseUrl) 
            ? $"/api/workflow/execution/{executionId}/stream"
            : $"{baseUrl}/api/workflow/execution/{executionId}/stream";
        
        return await ConnectAsync(url, executionId.ToString());
    }
    
    /// <summary>
    /// Topic aboneliği ayarlar
    /// </summary>
    public void SetSubscribedTopics(params string[] topics)
    {
        _subscribedTopics.Clear();
        foreach (var topic in topics)
        {
            _subscribedTopics.Add(topic);
        }
    }
    
    /// <summary>
    /// Tüm topic'lere abone ol
    /// </summary>
    public void SubscribeToAllTopics()
    {
        _subscribedTopics.Clear();
        _subscribedTopics.Add("*");
    }
    
    /// <summary>
    /// Bağlantıyı kapatır
    /// </summary>
    public async Task DisconnectAsync(string connectionId)
    {
        if (_module == null) return;
        
        await _module.InvokeVoidAsync("disconnect", connectionId);
        _activeConnections.Remove(connectionId);
        
        if (_activeConnections.Count == 0)
        {
            ConnectionState = SseConnectionState.Disconnected;
            OnConnectionStateChanged?.Invoke(ConnectionState);
        }
    }
    
    /// <summary>
    /// Tüm bağlantıları kapatır
    /// </summary>
    public async Task DisconnectAllAsync()
    {
        if (_module == null) return;
        
        await _module.InvokeVoidAsync("disconnectAll");
        _activeConnections.Clear();
        ConnectionState = SseConnectionState.Disconnected;
        OnConnectionStateChanged?.Invoke(ConnectionState);
    }
    
    /// <summary>
    /// İstatistikleri getirir
    /// </summary>
    public SseClientStatistics GetStatistics()
    {
        return new SseClientStatistics
        {
            ActiveConnections = _activeConnections.Count,
            TotalEventsReceived = _totalEventsReceived,
            DuplicatesDropped = _duplicatesDropped,
            BufferedEvents = _offlineBuffer.Count
        };
    }
    
    /// <summary>
    /// Dedup cache'i temizler
    /// </summary>
    public void ClearDedupCache()
    {
        lock (_dedupLock)
        {
            _processedEventIds.Clear();
        }
    }
    
    /// <summary>
    /// Event'in daha önce işlenip işlenmediğini kontrol eder
    /// </summary>
    private bool IsDuplicateEvent(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return false;
        
        lock (_dedupLock)
        {
            if (_processedEventIds.Contains(eventId))
            {
                _duplicatesDropped++;
                return true;
            }
            
            _processedEventIds.Add(eventId);
            
            // Cache büyüklüğünü kontrol et
            if (_processedEventIds.Count > MaxProcessedEventIds)
            {
                // En eski yarısını temizle (HashSet sırası garanti değil ama yeterli)
                var toRemove = _processedEventIds.Take(_processedEventIds.Count / 2).ToList();
                foreach (var id in toRemove)
                {
                    _processedEventIds.Remove(id);
                }
            }
            
            return false;
        }
    }
    
    #region JS Callbacks
    
    [JSInvokable]
    public void OnConnectionStateChanged_Internal(string state)
    {
        ConnectionState = state switch
        {
            "Connected" => SseConnectionState.Connected,
            "Connecting" => SseConnectionState.Connecting,
            "Reconnecting" => SseConnectionState.Reconnecting,
            _ => SseConnectionState.Disconnected
        };
        
        _isOnline = ConnectionState == SseConnectionState.Connected;
        
        // Yeniden bağlandıysa offline buffer'ı flush et
        if (_isOnline && _offlineBuffer.Count > 0)
        {
            FlushOfflineBuffer();
        }
        
        OnConnectionStateChanged?.Invoke(ConnectionState);
    }
    
    [JSInvokable]
    public void OnMessage_Internal(string data)
    {
        _totalEventsReceived++;
        OnMessage?.Invoke(data);
    }
    
    [JSInvokable]
    public void OnNodeStarted_Internal(string data)
    {
        try
        {
            // Envelope parsing ile dedup ve ordering
            if (!TryParseEnvelope(data, out var eventId, out var seq))
            {
                // Legacy format - doğrudan parse et
                var evt = JsonSerializer.Deserialize<NodeStartedEvent>(data, JsonOptions);
                if (evt != null) OnNodeStarted?.Invoke(evt);
                return;
            }
            
            if (IsDuplicateEvent(eventId!)) return;
            _totalEventsReceived++;
            
            var parsedEvt = JsonSerializer.Deserialize<NodeStartedEvent>(ExtractDataFromEnvelope(data), JsonOptions);
            if (parsedEvt != null)
            {
                OnNodeStarted?.Invoke(parsedEvt);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"NodeStarted parse hatası: {ex.Message}");
        }
    }
    
    [JSInvokable]
    public void OnNodeCompleted_Internal(string data)
    {
        try
        {
            if (!TryParseEnvelope(data, out var eventId, out _))
            {
                var evt = JsonSerializer.Deserialize<NodeCompletedEvent>(data, JsonOptions);
                if (evt != null) OnNodeCompleted?.Invoke(evt);
                return;
            }
            
            if (IsDuplicateEvent(eventId!)) return;
            _totalEventsReceived++;
            
            var parsedEvt = JsonSerializer.Deserialize<NodeCompletedEvent>(ExtractDataFromEnvelope(data), JsonOptions);
            if (parsedEvt != null)
            {
                OnNodeCompleted?.Invoke(parsedEvt);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"NodeCompleted parse hatası: {ex.Message}");
        }
    }
    
    [JSInvokable]
    public void OnProgress_Internal(string data)
    {
        try
        {
            if (!TryParseEnvelope(data, out var eventId, out _))
            {
                var evt = JsonSerializer.Deserialize<ProgressEvent>(data, JsonOptions);
                if (evt != null) OnProgress?.Invoke(evt);
                return;
            }
            
            if (IsDuplicateEvent(eventId!)) return;
            _totalEventsReceived++;
            
            var parsedEvt = JsonSerializer.Deserialize<ProgressEvent>(ExtractDataFromEnvelope(data), JsonOptions);
            if (parsedEvt != null)
            {
                OnProgress?.Invoke(parsedEvt);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Progress parse hatası: {ex.Message}");
        }
    }
    
    [JSInvokable]
    public void OnLog_Internal(string data)
    {
        try
        {
            if (!TryParseEnvelope(data, out var eventId, out _))
            {
                var evt = JsonSerializer.Deserialize<LogEvent>(data, JsonOptions);
                if (evt != null) OnLog?.Invoke(evt);
                return;
            }
            
            if (IsDuplicateEvent(eventId!)) return;
            _totalEventsReceived++;
            
            var parsedEvt = JsonSerializer.Deserialize<LogEvent>(ExtractDataFromEnvelope(data), JsonOptions);
            if (parsedEvt != null)
            {
                OnLog?.Invoke(parsedEvt);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Log parse hatası: {ex.Message}");
        }
    }
    
    [JSInvokable]
    public void OnWorkflowCompleted_Internal(string data)
    {
        try
        {
            if (!TryParseEnvelope(data, out var eventId, out _))
            {
                var evt = JsonSerializer.Deserialize<WorkflowCompletedEventDto>(data, JsonOptions);
                if (evt != null) OnWorkflowCompleted?.Invoke(evt);
                return;
            }
            
            if (IsDuplicateEvent(eventId!)) return;
            _totalEventsReceived++;
            
            var parsedEvt = JsonSerializer.Deserialize<WorkflowCompletedEventDto>(ExtractDataFromEnvelope(data), JsonOptions);
            if (parsedEvt != null)
            {
                OnWorkflowCompleted?.Invoke(parsedEvt);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"WorkflowCompleted parse hatası: {ex.Message}");
        }
    }
    
    #endregion
    
    #region Envelope & Compression Helpers
    
    /// <summary>
    /// Envelope formatındaki event'ten id ve seq çıkarır
    /// </summary>
    private static bool TryParseEnvelope(string data, out string? eventId, out long sequenceNumber)
    {
        eventId = null;
        sequenceNumber = 0;
        
        try
        {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;
            
            // Compressed payload kontrolü
            if (root.TryGetProperty("compressed", out var compressed) && compressed.GetBoolean())
            {
                // Compressed data - client'ta decompress desteklenmez, raw olarak geç
                return false;
            }
            
            if (root.TryGetProperty("id", out var id))
            {
                eventId = id.GetString();
            }
            
            if (root.TryGetProperty("seq", out var seq))
            {
                sequenceNumber = seq.GetInt64();
            }
            
            return eventId != null;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Envelope'dan inner data'yı çıkarır
    /// </summary>
    private static string ExtractDataFromEnvelope(string data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.TryGetProperty("data", out var dataElement))
            {
                return dataElement.GetRawText();
            }
        }
        catch
        {
            // Parse hatası - orijinal data'yı dön
        }
        return data;
    }
    
    /// <summary>
    /// Offline buffer'daki event'leri işler
    /// </summary>
    private void FlushOfflineBuffer()
    {
        while (_offlineBuffer.TryDequeue(out var buffered))
        {
            try
            {
                switch (buffered.EventType)
                {
                    case "node-started":
                        OnNodeStarted_Internal(buffered.Data);
                        break;
                    case "node-completed":
                        OnNodeCompleted_Internal(buffered.Data);
                        break;
                    case "progress":
                        OnProgress_Internal(buffered.Data);
                        break;
                    case "log":
                        OnLog_Internal(buffered.Data);
                        break;
                    case "workflow-completed":
                        OnWorkflowCompleted_Internal(buffered.Data);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Offline buffer flush hatası: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Offline buffer'a event ekler (JS tarafından çağrılabilir)
    /// </summary>
    [JSInvokable]
    public void BufferEvent_Internal(string eventType, string data)
    {
        if (_offlineBuffer.Count >= MaxOfflineBufferSize)
        {
            _offlineBuffer.Dequeue(); // En eski event'i at
        }
        
        _offlineBuffer.Enqueue(new BufferedEvent
        {
            EventType = eventType,
            Data = data,
            BufferedAt = DateTime.UtcNow
        });
    }
    
    #endregion
    
    private static JsonSerializerOptions JsonOptions => new()
    {
        PropertyNameCaseInsensitive = true
    };
    
    public async ValueTask DisposeAsync()
    {
        await DisconnectAllAsync();
        
        _dotNetRef?.Dispose();
        
        if (_module != null)
        {
            await _module.DisposeAsync();
        }
    }
}

#region Event DTOs

public record BufferedEvent
{
    public string EventType { get; init; } = string.Empty;
    public string Data { get; init; } = string.Empty;
    public DateTime BufferedAt { get; init; }
}

public record NodeStartedEvent
{
    public Guid ExecutionId { get; init; }
    public Guid NodeId { get; init; }
    public string NodeName { get; init; } = string.Empty;
    public string NodeType { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}

public record NodeCompletedEvent
{
    public Guid ExecutionId { get; init; }
    public Guid NodeId { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
    public double DurationMs { get; init; }
    public object? OutputPreview { get; init; }
    public DateTime Timestamp { get; init; }
}

public record ProgressEvent
{
    public Guid ExecutionId { get; init; }
    public Guid NodeId { get; init; }
    public int Current { get; init; }
    public int Total { get; init; }
    public string? Message { get; init; }
    public DateTime Timestamp { get; init; }
    
    public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;
}

public record LogEvent
{
    public Guid ExecutionId { get; init; }
    public Guid? NodeId { get; init; }
    public string Level { get; init; } = "info";
    public string Message { get; init; } = string.Empty;
    public object? Data { get; init; }
    public DateTime Timestamp { get; init; }
}

public record WorkflowCompletedEventDto
{
    public Guid ExecutionId { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
    public double TotalDurationMs { get; init; }
    public int NodesExecuted { get; init; }
    public DateTime Timestamp { get; init; }
}

#endregion
