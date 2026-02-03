using System.Text.Json;
using System.Threading.Channels;
using SonOfAnton.Shared.Core.GraphEngine;

namespace SonOfAnton.Backend.Services;

/// <summary>
/// SSE (Server-Sent Events) Broadcast Service
/// Workflow execution event'lerini client'lara yayınlar
/// </summary>
public class SseEventService
{
    private readonly Dictionary<Guid, Channel<WorkflowEvent>> _executionChannels = new();
    private readonly object _lock = new();
    private readonly ILogger<SseEventService> _logger;
    
    public SseEventService(ILogger<SseEventService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Execution için yeni bir event channel oluşturur
    /// </summary>
    public Channel<WorkflowEvent> CreateChannel(Guid executionId)
    {
        var channel = Channel.CreateUnbounded<WorkflowEvent>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false
        });
        
        lock (_lock)
        {
            _executionChannels[executionId] = channel;
        }
        
        _logger.LogDebug("SSE channel oluşturuldu: {ExecutionId}", executionId);
        return channel;
    }
    
    /// <summary>
    /// Execution channel'ı alır
    /// </summary>
    public Channel<WorkflowEvent>? GetChannel(Guid executionId)
    {
        lock (_lock)
        {
            return _executionChannels.TryGetValue(executionId, out var channel) ? channel : null;
        }
    }
    
    /// <summary>
    /// Event yayınlar
    /// </summary>
    public async ValueTask PublishAsync(Guid executionId, WorkflowEvent evt)
    {
        Channel<WorkflowEvent>? channel;
        lock (_lock)
        {
            _executionChannels.TryGetValue(executionId, out channel);
        }
        
        if (channel != null)
        {
            await channel.Writer.WriteAsync(evt);
        }
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
        });
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
        });
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
        });
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
        await PublishAsync(executionId, new LogEmittedEvent
        {
            ExecutionId = executionId,
            NodeId = nodeId,
            Level = level,
            Message = message,
            Data = data
        });
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
        });
    }
    
    /// <summary>
    /// Channel'ı tamamlar ve kaldırır
    /// </summary>
    public void CompleteChannel(Guid executionId)
    {
        Channel<WorkflowEvent>? channel;
        lock (_lock)
        {
            if (_executionChannels.TryGetValue(executionId, out channel))
            {
                _executionChannels.Remove(executionId);
            }
        }
        
        channel?.Writer.TryComplete();
        _logger.LogDebug("SSE channel tamamlandı: {ExecutionId}", executionId);
    }
}

/// <summary>
/// SSE Event formatter - WorkflowEvent'leri SSE formatına dönüştürür
/// </summary>
public static class SseFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
    
    /// <summary>
    /// Event'i SSE formatına dönüştürür
    /// </summary>
    public static string FormatEvent(WorkflowEvent evt)
    {
        var eventType = evt switch
        {
            NodeExecutionStartedEvent => "node-started",
            NodeExecutionCompletedEvent => "node-completed",
            ProgressUpdatedEvent => "progress",
            LogEmittedEvent => "log",
            WorkflowCompletedEvent => "workflow-completed",
            _ => "message"
        };
        
        var data = JsonSerializer.Serialize(evt, evt.GetType(), JsonOptions);
        
        return $"event: {eventType}\ndata: {data}\n\n";
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
}
