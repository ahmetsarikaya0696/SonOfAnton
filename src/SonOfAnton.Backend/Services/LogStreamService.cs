using System.Collections.Concurrent;
using System.Threading.Channels;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Services;

// Logs are durable in PostgreSQL. Memory only fans out to currently connected clients.
public sealed class LogStreamService
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<LogEventDto>>> _subscribers = new();
    public int ActiveChannelCount => _subscribers.Count;
    public Task WriteAsync(Guid executionId, LogEventDto log)
    {
        if (_subscribers.TryGetValue(executionId, out var listeners))
            foreach (var (id, channel) in listeners)
                if (!channel.Writer.TryWrite(log) && listeners.TryRemove(id, out _)) channel.Writer.TryComplete();
        return Task.CompletedTask;
    }
    public async IAsyncEnumerable<LogEventDto> ReadAsync(Guid executionId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<LogEventDto>(256);
        var listeners = _subscribers.GetOrAdd(executionId, _ => new());
        listeners[id] = channel;
        try { await foreach (var log in channel.Reader.ReadAllAsync(cancellationToken)) yield return log; }
        finally
        {
            listeners.TryRemove(id, out _);
            channel.Writer.TryComplete();
            if (listeners.IsEmpty) _subscribers.TryRemove(new KeyValuePair<Guid, ConcurrentDictionary<Guid, Channel<LogEventDto>>>(executionId, listeners));
        }
    }
    public void Complete(Guid executionId)
    {
        if (_subscribers.TryRemove(executionId, out var listeners))
            foreach (var channel in listeners.Values) channel.Writer.TryComplete();
    }
    public void Remove(Guid executionId) => Complete(executionId);
    public int CleanupStaleChannels(TimeSpan maxAge) => 0;
}
