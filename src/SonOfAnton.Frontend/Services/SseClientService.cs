using System.Text.Json;
using Microsoft.JSInterop;

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
/// SSE (Server-Sent Events) Client Servisi
/// Browser native EventSource API'sini kullanır
/// </summary>
public class SseClientService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private DotNetObjectReference<SseClientService>? _dotNetRef;
    private readonly Dictionary<string, string> _activeConnections = new();
    
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
    
    public SseClientService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
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
    /// <param name="url">SSE endpoint URL</param>
    /// <param name="connectionId">Opsiyonel connection ID</param>
    public async Task<bool> ConnectAsync(string url, string? connectionId = null)
    {
        await EnsureModuleLoadedAsync();
        
        connectionId ??= Guid.NewGuid().ToString();
        
        var result = await _module!.InvokeAsync<bool>("connectToStream", connectionId, url, _dotNetRef);
        
        if (result)
        {
            _activeConnections[connectionId] = url;
            ConnectionState = SseConnectionState.Connecting;
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
        
        OnConnectionStateChanged?.Invoke(ConnectionState);
    }
    
    [JSInvokable]
    public void OnMessage_Internal(string data)
    {
        OnMessage?.Invoke(data);
    }
    
    [JSInvokable]
    public void OnNodeStarted_Internal(string data)
    {
        try
        {
            var evt = JsonSerializer.Deserialize<NodeStartedEvent>(data, JsonOptions);
            if (evt != null)
            {
                OnNodeStarted?.Invoke(evt);
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
            var evt = JsonSerializer.Deserialize<NodeCompletedEvent>(data, JsonOptions);
            if (evt != null)
            {
                OnNodeCompleted?.Invoke(evt);
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
            var evt = JsonSerializer.Deserialize<ProgressEvent>(data, JsonOptions);
            if (evt != null)
            {
                OnProgress?.Invoke(evt);
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
            var evt = JsonSerializer.Deserialize<LogEvent>(data, JsonOptions);
            if (evt != null)
            {
                OnLog?.Invoke(evt);
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
            var evt = JsonSerializer.Deserialize<WorkflowCompletedEventDto>(data, JsonOptions);
            if (evt != null)
            {
                OnWorkflowCompleted?.Invoke(evt);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"WorkflowCompleted parse hatası: {ex.Message}");
        }
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
