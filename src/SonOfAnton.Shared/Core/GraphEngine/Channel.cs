using System.Threading.Channels;

namespace SonOfAnton.Shared.Core.GraphEngine;

/// <summary>
/// Type-safe veri kanalı - Node'lar arası asenkron veri akışı
/// Producer-consumer pattern ile thread-safe iletişim sağlar
/// </summary>
/// <typeparam name="T">Kanal üzerinden iletilecek veri tipi</typeparam>
public class DataChannel<T> : IDisposable
{
    private readonly Channel<T> _channel;
    private readonly CancellationTokenSource _cts;
    private bool _disposed;
    
    /// <summary>
    /// Kanal kapasitesi
    /// </summary>
    public int Capacity { get; }
    
    /// <summary>
    /// Kanal aktif mi (yazma/okuma yapılabilir)
    /// </summary>
    public bool IsActive => !_disposed && !_cts.IsCancellationRequested;
    
    /// <summary>
    /// Okunmayı bekleyen item sayısı
    /// </summary>
    public int PendingCount => _channel.Reader.CanCount ? _channel.Reader.Count : -1;
    
    /// <summary>
    /// Yeni bir veri kanalı oluşturur
    /// </summary>
    /// <param name="capacity">Kanal kapasitesi (0 = sınırsız)</param>
    /// <param name="singleWriter">Tek yazıcı optimizasyonu</param>
    /// <param name="singleReader">Tek okuyucu optimizasyonu</param>
    public DataChannel(int capacity = 100, bool singleWriter = true, bool singleReader = true)
    {
        Capacity = capacity;
        _cts = new CancellationTokenSource();
        
        var options = capacity > 0
            ? new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = singleWriter,
                SingleReader = singleReader
            }
            : null;
        
        _channel = capacity > 0
            ? Channel.CreateBounded<T>((BoundedChannelOptions)options!)
            : Channel.CreateUnbounded<T>(new UnboundedChannelOptions
            {
                SingleWriter = singleWriter,
                SingleReader = singleReader
            });
    }
    
    /// <summary>
    /// Kanala veri yazar
    /// </summary>
    public async ValueTask WriteAsync(T item, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        await _channel.Writer.WriteAsync(item, linkedCts.Token);
    }
    
    /// <summary>
    /// Kanala senkron veri yazar (eğer yer varsa)
    /// </summary>
    public bool TryWrite(T item)
    {
        if (_disposed || _cts.IsCancellationRequested)
            return false;
        return _channel.Writer.TryWrite(item);
    }
    
    /// <summary>
    /// Kanaldan veri okur
    /// </summary>
    public async ValueTask<T> ReadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        return await _channel.Reader.ReadAsync(linkedCts.Token);
    }
    
    /// <summary>
    /// Kanaldan senkron veri okur (eğer varsa)
    /// </summary>
    public bool TryRead(out T? item)
    {
        item = default;
        if (_disposed || _cts.IsCancellationRequested)
            return false;
        return _channel.Reader.TryRead(out item);
    }
    
    /// <summary>
    /// Tüm verileri async olarak okur (IAsyncEnumerable)
    /// </summary>
    public async IAsyncEnumerable<T> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        
        await foreach (var item in _channel.Reader.ReadAllAsync(linkedCts.Token))
        {
            yield return item;
        }
    }
    
    /// <summary>
    /// Veri bekliyor mu kontrolü
    /// </summary>
    public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return ValueTask.FromResult(false);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        return _channel.Reader.WaitToReadAsync(linkedCts.Token);
    }
    
    /// <summary>
    /// Kanalı kapatır (yazma tamamlandı)
    /// </summary>
    public void Complete(Exception? error = null)
    {
        _channel.Writer.Complete(error);
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _channel.Writer.TryComplete();
        _cts.Dispose();
    }
}

/// <summary>
/// Workflow event'leri için özel kanal
/// </summary>
public class WorkflowEventChannel : DataChannel<WorkflowEvent>
{
    public WorkflowEventChannel() : base(capacity: 1000) { }
}

/// <summary>
/// Workflow event base class
/// </summary>
public abstract record WorkflowEvent
{
    public Guid ExecutionId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Node execution başladı event'i
/// </summary>
public record NodeExecutionStartedEvent : WorkflowEvent
{
    public Guid NodeId { get; init; }
    public string NodeName { get; init; } = string.Empty;
    public string NodeType { get; init; } = string.Empty;
}

/// <summary>
/// Node execution tamamlandı event'i
/// </summary>
public record NodeExecutionCompletedEvent : WorkflowEvent
{
    public Guid NodeId { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
    public TimeSpan Duration { get; init; }
    public object? OutputPreview { get; init; }
}

/// <summary>
/// İlerleme güncellemesi event'i
/// </summary>
public record ProgressUpdatedEvent : WorkflowEvent
{
    public Guid NodeId { get; init; }
    public int Current { get; init; }
    public int Total { get; init; }
    public string? Message { get; init; }
    
    public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;
}

/// <summary>
/// Log event'i
/// </summary>
public record LogEmittedEvent : WorkflowEvent
{
    public Guid? NodeId { get; init; }
    public string Level { get; init; } = "info";
    public string Message { get; init; } = string.Empty;
    public object? Data { get; init; }
}

/// <summary>
/// Workflow tamamlandı event'i
/// </summary>
public record WorkflowCompletedEvent : WorkflowEvent
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public int NodesExecuted { get; init; }
}
