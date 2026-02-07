using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using SonOfAnton.Shared.Core.GraphEngine;

namespace SonOfAnton.Backend.Services;

/// <summary>
/// SSE client session bilgisi
/// </summary>
public class SseClientSession
{
    public string SessionId { get; init; } = Guid.NewGuid().ToString();
    public Guid ExecutionId { get; init; }
    public DateTime ConnectedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastHeartbeatAt { get; set; } = DateTime.UtcNow;
    public HashSet<string> SubscribedTopics { get; init; } = ["*"];
    public bool IsActive { get; set; } = true;
    public long EventsReceived { get; set; }
}

/// <summary>
/// SSE event priority seviyeleri
/// </summary>
public enum SseEventPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Prioritized wrapper for workflow events
/// </summary>
public class PrioritizedEvent
{
    public required WorkflowEvent Event { get; init; }
    public SseEventPriority Priority { get; init; } = SseEventPriority.Normal;
    public long SequenceNumber { get; init; }
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");
    public string Topic { get; init; } = "execution";
}

/// <summary>
/// SSE broadcast istatistikleri
/// </summary>
public class SseBroadcastStatistics
{
    public int ActiveChannels { get; init; }
    public int ActiveSessions { get; init; }
    public long TotalEventsPublished { get; init; }
    public long TotalBytesTransferred { get; init; }
}

/// <summary>
/// SSE (Server-Sent Events) Broadcast Service
/// Workflow execution event'lerini client'lara yayınlar
/// Multi-client session tracking, topic-based routing, event prioritization
/// </summary>
public class SseEventService
{
    private readonly ConcurrentDictionary<Guid, Channel<PrioritizedEvent>> _executionChannels = new();
    private readonly ConcurrentDictionary<string, SseClientSession> _sessions = new();
    private readonly ILogger<SseEventService> _logger;
    
    private long _sequenceCounter;
    private long _totalEventsPublished;
    private long _totalBytesTransferred;
    
    public SseEventService(ILogger<SseEventService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Execution için yeni bir event channel oluşturur
    /// </summary>
    public Channel<PrioritizedEvent> CreateChannel(Guid executionId)
    {
        var channel = Channel.CreateUnbounded<PrioritizedEvent>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false
        });
        
        _executionChannels[executionId] = channel;
        
        _logger.LogDebug("SSE channel oluşturuldu: {ExecutionId}", executionId);
        return channel;
    }
    
    /// <summary>
    /// Execution channel'ı alır
    /// </summary>
    public Channel<PrioritizedEvent>? GetChannel(Guid executionId)
    {
        return _executionChannels.TryGetValue(executionId, out var channel) ? channel : null;
    }
    
    /// <summary>
    /// Client session kaydeder
    /// </summary>
    public SseClientSession RegisterSession(Guid executionId, IEnumerable<string>? topics = null)
    {
        var session = new SseClientSession
        {
            ExecutionId = executionId,
            SubscribedTopics = topics != null ? new HashSet<string>(topics) : ["*"]
        };
        
        _sessions[session.SessionId] = session;
        
        _logger.LogDebug("SSE session kaydedildi: {SessionId} -> Execution: {ExecutionId}", 
            session.SessionId, executionId);
        
        return session;
    }
    
    /// <summary>
    /// Client session kaldırır
    /// </summary>
    public void UnregisterSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            session.IsActive = false;
            _logger.LogDebug("SSE session kaldırıldı: {SessionId}", sessionId);
        }
    }
    
    /// <summary>
    /// Session heartbeat günceller
    /// </summary>
    public void UpdateSessionHeartbeat(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.LastHeartbeatAt = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// Aktif session'ları getirir
    /// </summary>
    public IReadOnlyList<SseClientSession> GetActiveSessions(Guid executionId)
    {
        return _sessions.Values
            .Where(s => s.ExecutionId == executionId && s.IsActive)
            .ToList();
    }
    
    /// <summary>
    /// Stale (ölü) session'ları temizler
    /// </summary>
    public int CleanupStaleSessions(TimeSpan timeout)
    {
        var cutoff = DateTime.UtcNow - timeout;
        var staleSessionIds = _sessions.Values
            .Where(s => s.LastHeartbeatAt < cutoff)
            .Select(s => s.SessionId)
            .ToList();
        
        foreach (var sessionId in staleSessionIds)
        {
            UnregisterSession(sessionId);
        }
        
        if (staleSessionIds.Count > 0)
        {
            _logger.LogInformation("{Count} stale SSE session temizlendi", staleSessionIds.Count);
        }
        
        return staleSessionIds.Count;
    }
    
    /// <summary>
    /// Event yayınlar (priority ve topic ile)
    /// </summary>
    public async ValueTask PublishAsync(
        Guid executionId, 
        WorkflowEvent evt,
        SseEventPriority priority = SseEventPriority.Normal,
        string topic = "execution")
    {
        if (!_executionChannels.TryGetValue(executionId, out var channel))
            return;
        
        var sequenceNumber = Interlocked.Increment(ref _sequenceCounter);
        Interlocked.Increment(ref _totalEventsPublished);
        
        var prioritizedEvent = new PrioritizedEvent
        {
            Event = evt,
            Priority = priority,
            SequenceNumber = sequenceNumber,
            Topic = topic
        };
        
        await channel.Writer.WriteAsync(prioritizedEvent);
    }
    
    /// <summary>
    /// Node execution başladı event'i
    /// </summary>
    public async ValueTask PublishNodeStartedAsync(Guid executionId, Guid nodeId, string nodeName, string nodeType)
    {
        await PublishAsync(executionId, new NodeExecutionStartedEvent
        {
            ExecutionId = executionId,
            NodeId = nodeId,
            NodeName = nodeName,
            NodeType = nodeType
        }, SseEventPriority.High, "node-execution");
    }
    
    /// <summary>
    /// Node execution tamamlandı event'i
    /// </summary>
    public async ValueTask PublishNodeCompletedAsync(
        Guid executionId,
        Guid nodeId,
        bool success,
        string? error,
        TimeSpan duration,
        object? outputPreview = null)
    {
        await PublishAsync(executionId, new NodeExecutionCompletedEvent
        {
            ExecutionId = executionId,
            NodeId = nodeId,
            Success = success,
            Error = error,
            Duration = duration,
            OutputPreview = outputPreview
        }, success ? SseEventPriority.Normal : SseEventPriority.High, "node-execution");
    }
    
    /// <summary>
    /// İlerleme event'i
    /// </summary>
    public async ValueTask PublishProgressAsync(
        Guid executionId,
        Guid nodeId,
        int current,
        int total,
        string? message = null)
    {
        await PublishAsync(executionId, new ProgressUpdatedEvent
        {
            ExecutionId = executionId,
            NodeId = nodeId,
            Current = current,
            Total = total,
            Message = message
        }, SseEventPriority.Low, "progress");
    }
    
    /// <summary>
    /// Log event'i
    /// </summary>
    public async ValueTask PublishLogAsync(
        Guid executionId,
        Guid? nodeId,
        string level,
        string message,
        object? data = null)
    {
        var priority = level.ToLowerInvariant() switch
        {
            "error" or "critical" => SseEventPriority.High,
            "warning" => SseEventPriority.Normal,
            _ => SseEventPriority.Low
        };
        
        await PublishAsync(executionId, new LogEmittedEvent
        {
            ExecutionId = executionId,
            NodeId = nodeId,
            Level = level,
            Message = message,
            Data = data
        }, priority, "log");
    }
    
    /// <summary>
    /// Workflow tamamlandı event'i
    /// </summary>
    public async ValueTask PublishWorkflowCompletedAsync(
        Guid executionId,
        bool success,
        string? error,
        TimeSpan totalDuration,
        int nodesExecuted)
    {
        await PublishAsync(executionId, new WorkflowCompletedEvent
        {
            ExecutionId = executionId,
            Success = success,
            Error = error,
            TotalDuration = totalDuration,
            NodesExecuted = nodesExecuted
        }, SseEventPriority.Critical, "workflow");
    }
    
    /// <summary>
    /// Channel'ı tamamlar ve kaldırır
    /// </summary>
    public void CompleteChannel(Guid executionId)
    {
        if (_executionChannels.TryRemove(executionId, out var channel))
        {
            channel.Writer.TryComplete();
        }
        
        // İlgili session'ları temizle
        var sessionIds = _sessions.Values
            .Where(s => s.ExecutionId == executionId)
            .Select(s => s.SessionId)
            .ToList();
        
        foreach (var sessionId in sessionIds)
        {
            UnregisterSession(sessionId);
        }
        
        _logger.LogDebug("SSE channel tamamlandı: {ExecutionId}", executionId);
    }
    
    /// <summary>
    /// Broadcast istatistikleri
    /// </summary>
    public SseBroadcastStatistics GetStatistics()
    {
        return new SseBroadcastStatistics
        {
            ActiveChannels = _executionChannels.Count,
            ActiveSessions = _sessions.Values.Count(s => s.IsActive),
            TotalEventsPublished = Interlocked.Read(ref _totalEventsPublished),
            TotalBytesTransferred = Interlocked.Read(ref _totalBytesTransferred)
        };
    }
    
    /// <summary>
    /// Bytes transferred günceller (SSE endpoint tarafından çağrılır)
    /// </summary>
    public void TrackBytesTransferred(long bytes)
    {
        Interlocked.Add(ref _totalBytesTransferred, bytes);
    }
}

/// <summary>
/// SSE Event formatter - WorkflowEvent'leri SSE formatına dönüştürür
/// Event ID, sequence number, topic ve compression desteği
/// </summary>
public static class SseFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
    
    /// <summary>
    /// Compression eşiği (byte) - bu değerden büyük payload'lar compress edilir
    /// </summary>
    public const int CompressionThreshold = 4096;
    
    /// <summary>
    /// PrioritizedEvent'i SSE formatına dönüştürür (event ID, sequence, topic ile)
    /// </summary>
    public static string FormatPrioritizedEvent(PrioritizedEvent prioritizedEvent, SseClientSession? session = null)
    {
        // Topic filtresi kontrolü
        if (session != null && !session.SubscribedTopics.Contains("*") 
            && !session.SubscribedTopics.Contains(prioritizedEvent.Topic))
        {
            return string.Empty; // Bu event bu session'a gönderilmeyecek
        }
        
        var evt = prioritizedEvent.Event;
        var eventType = GetEventType(evt);
        
        var envelope = new
        {
            id = prioritizedEvent.EventId,
            seq = prioritizedEvent.SequenceNumber,
            topic = prioritizedEvent.Topic,
            priority = (int)prioritizedEvent.Priority,
            timestamp = DateTime.UtcNow,
            data = evt
        };
        
        var data = JsonSerializer.Serialize(envelope, JsonOptions);
        
        var sb = new StringBuilder();
        sb.Append("id: ").Append(prioritizedEvent.EventId).Append('\n');
        sb.Append("event: ").Append(eventType).Append('\n');
        
        // Büyük payload'ları compress et
        if (data.Length > CompressionThreshold)
        {
            var compressed = CompressData(data);
            sb.Append("data: ").Append(compressed).Append('\n');
            sb.Append("retry: 5000\n");
        }
        else
        {
            sb.Append("data: ").Append(data).Append('\n');
        }
        
        sb.Append('\n');
        
        if (session != null)
        {
            session.EventsReceived++;
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Legacy: Event'i SSE formatına dönüştürür (geriye uyumluluk)
    /// </summary>
    public static string FormatEvent(WorkflowEvent evt)
    {
        var eventType = GetEventType(evt);
        var data = JsonSerializer.Serialize(evt, evt.GetType(), JsonOptions);
        return $"event: {eventType}\ndata: {data}\n\n";
    }
    
    /// <summary>
    /// Event tipini string'e dönüştürür
    /// </summary>
    public static string GetEventType(WorkflowEvent evt)
    {
        return evt switch
        {
            NodeExecutionStartedEvent => "node-started",
            NodeExecutionCompletedEvent => "node-completed",
            ProgressUpdatedEvent => "progress",
            LogEmittedEvent => "log",
            WorkflowCompletedEvent => "workflow-completed",
            _ => "message"
        };
    }
    
    /// <summary>
    /// Heartbeat event'i formatlar
    /// </summary>
    public static string FormatHeartbeat()
    {
        return $"event: heartbeat\ndata: {DateTime.UtcNow:O}\n\n";
    }
    
    /// <summary>
    /// Comment formatlar (keep-alive için)
    /// </summary>
    public static string FormatComment(string comment)
    {
        return $": {comment}\n\n";
    }
    
    /// <summary>
    /// Büyük payload'ları base64-encoded gzip ile compress eder
    /// </summary>
    private static string CompressData(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }
        return $"{{\"compressed\":true,\"encoding\":\"gzip+base64\",\"payload\":\"{Convert.ToBase64String(output.ToArray())}\"}}";
    }
}
