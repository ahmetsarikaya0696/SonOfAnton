using System.Diagnostics;
using System.Threading.Channels;

namespace SonOfAnton.Shared.Core.GraphEngine;

/// <summary>
/// Kanal tipi
/// </summary>
public enum ChannelType
{
    /// <summary>Sınırsız kapasiteli kanal</summary>
    Unbounded,
    /// <summary>Sınırlı kapasiteli kanal</summary>
    Bounded,
    /// <summary>Tek öğeli kanal (son değer kazanır)</summary>
    SingleItem
}

/// <summary>
/// Kanal istatistikleri
/// </summary>
public class ChannelStatistics
{
    /// <summary>Toplam yazılan öğe sayısı</summary>
    public long TotalWritten { get; set; }

    /// <summary>Toplam okunan öğe sayısı</summary>
    public long TotalRead { get; set; }

    /// <summary>Buffer kullanım oranı (0-1). Unbounded için -1</summary>
    public double BufferUtilization { get; set; } = -1;

    /// <summary>Saniye başına yazılan öğe</summary>
    public double WriteThroughput { get; set; }

    /// <summary>Saniye başına okunan öğe</summary>
    public double ReadThroughput { get; set; }

    /// <summary>İlk yazma zamanı</summary>
    public DateTime? FirstWriteTime { get; set; }

    /// <summary>Son yazma zamanı</summary>
    public DateTime? LastWriteTime { get; set; }
}

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
    private long _totalWritten;
    private long _totalRead;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    
    /// <summary>
    /// Kanal kapasitesi
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Kanal tipi
    /// </summary>
    public ChannelType ChannelType { get; }
    
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
    /// <param name="channelType">Kanal tipi</param>
    public DataChannel(int capacity = 100, bool singleWriter = true, bool singleReader = true, ChannelType channelType = ChannelType.Bounded)
    {
        Capacity = capacity;
        ChannelType = channelType;
        _cts = new CancellationTokenSource();
        
        _channel = channelType switch
        {
            ChannelType.SingleItem => Channel.CreateBounded<T>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleWriter = singleWriter,
                SingleReader = singleReader
            }),
            ChannelType.Bounded when capacity > 0 => Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = singleWriter,
                SingleReader = singleReader
            }),
            _ => Channel.CreateUnbounded<T>(new UnboundedChannelOptions
            {
                SingleWriter = singleWriter,
                SingleReader = singleReader
            })
        };
    }
    
    /// <summary>
    /// Kanala veri yazar
    /// </summary>
    public async ValueTask WriteAsync(T item, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        await _channel.Writer.WriteAsync(item, linkedCts.Token);
        Interlocked.Increment(ref _totalWritten);
    }
    
    /// <summary>
    /// Kanala senkron veri yazar (eğer yer varsa)
    /// </summary>
    public bool TryWrite(T item)
    {
        if (_disposed || _cts.IsCancellationRequested)
            return false;
        var result = _channel.Writer.TryWrite(item);
        if (result) Interlocked.Increment(ref _totalWritten);
        return result;
    }
    
    /// <summary>
    /// Kanaldan veri okur
    /// </summary>
    public async ValueTask<T> ReadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        var item = await _channel.Reader.ReadAsync(linkedCts.Token);
        Interlocked.Increment(ref _totalRead);
        return item;
    }
    
    /// <summary>
    /// Kanaldan senkron veri okur (eğer varsa)
    /// </summary>
    public bool TryRead(out T? item)
    {
        item = default;
        if (_disposed || _cts.IsCancellationRequested)
            return false;
        var result = _channel.Reader.TryRead(out item);
        if (result) Interlocked.Increment(ref _totalRead);
        return result;
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

    /// <summary>
    /// Kanal istatistiklerini döndürür
    /// </summary>
    public ChannelStatistics GetStatistics()
    {
        var elapsed = _stopwatch.Elapsed;
        var totalSeconds = elapsed.TotalSeconds > 0 ? elapsed.TotalSeconds : 1;
        var written = Interlocked.Read(ref _totalWritten);
        var read = Interlocked.Read(ref _totalRead);
        var pending = PendingCount;

        return new ChannelStatistics
        {
            TotalWritten = written,
            TotalRead = read,
            BufferUtilization = ChannelType == ChannelType.Bounded && Capacity > 0 && pending >= 0
                ? (double)pending / Capacity
                : -1,
            WriteThroughput = written / totalSeconds,
            ReadThroughput = read / totalSeconds,
        };
    }

    /// <summary>
    /// Deadlock riski olup olmadığını kontrol eder
    /// </summary>
    public bool HasDeadlockRisk()
    {
        if (ChannelType != ChannelType.Bounded) return false;
        var pending = PendingCount;
        return pending >= 0 && pending >= Capacity;
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

/// <summary>
/// Farklı kanal tiplerine göre DataChannel oluşturan factory
/// </summary>
public static class ChannelFactory
{
    /// <summary>
    /// Sınırsız kapasiteli kanal oluşturur
    /// </summary>
    public static DataChannel<T> CreateUnbounded<T>(bool singleWriter = true, bool singleReader = true)
    {
        return new DataChannel<T>(capacity: 0, singleWriter, singleReader, ChannelType.Unbounded);
    }

    /// <summary>
    /// Sınırlı kapasiteli kanal oluşturur
    /// </summary>
    public static DataChannel<T> CreateBounded<T>(int capacity, bool singleWriter = true, bool singleReader = true)
    {
        return new DataChannel<T>(capacity, singleWriter, singleReader, ChannelType.Bounded);
    }

    /// <summary>
    /// Tek öğeli kanal oluşturur (son değer kazanır)
    /// </summary>
    public static DataChannel<T> CreateSingleItem<T>(bool singleWriter = true, bool singleReader = true)
    {
        return new DataChannel<T>(capacity: 1, singleWriter, singleReader, ChannelType.SingleItem);
    }

    /// <summary>
    /// Node gereksinimlerine göre uygun kanal oluşturur
    /// </summary>
    public static DataChannel<T> CreateForNode<T>(NodeCategory category)
    {
        return category switch
        {
            NodeCategory.Trigger => CreateUnbounded<T>(),
            NodeCategory.AI => CreateBounded<T>(10),
            NodeCategory.Transform => CreateBounded<T>(100),
            _ => CreateBounded<T>(50)
        };
    }
}
