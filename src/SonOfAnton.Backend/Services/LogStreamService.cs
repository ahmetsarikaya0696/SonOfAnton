using System.Threading.Channels;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Services;

/// <summary>
/// In-memory log stream servisi - SSE i�in
/// System.Threading.Channels kullanarak thread-safe ileti�im
/// </summary>
public class LogStreamService
{
    private readonly Dictionary<Guid, Channel<LogEventDto>> _channels = [];
    private readonly object _lock = new();

    /// <summary>
    /// Execution i�in channel olu�tur veya var olan� getir
    /// </summary>
    public Channel<LogEventDto> GetOrCreateChannel(Guid executionId)
    {
        lock (_lock)
        {
            if (!_channels.TryGetValue(executionId, out var channel))
            {
                channel = Channel.CreateUnbounded<LogEventDto>(new UnboundedChannelOptions
                {
                    SingleReader = false,
                    SingleWriter = false
                });
                _channels[executionId] = channel;
            }
            return channel;
        }
    }

    /// <summary>
    /// Log yaz (Writer taraf� - Hangfire job'lar�)
    /// </summary>
    public async Task WriteAsync(Guid executionId, LogEventDto logEvent)
    {
        var channel = GetOrCreateChannel(executionId);
        await channel.Writer.WriteAsync(logEvent);
    }

    /// <summary>
    /// Log oku (Reader taraf� - SSE endpoint)
    /// </summary>
    public async IAsyncEnumerable<LogEventDto> ReadAsync(
        Guid executionId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = GetOrCreateChannel(executionId);

        await foreach (var log in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return log;
        }
    }

    /// <summary>
    /// Channel'� tamamla (execution bitti�inde)
    /// </summary>
    public void Complete(Guid executionId)
    {
        lock (_lock)
        {
            if (_channels.TryGetValue(executionId, out var channel))
            {
                channel.Writer.Complete();
            }
        }
    }

    /// <summary>
    /// Channel'� temizle (bellek y�netimi i�in)
    /// </summary>
    public void Remove(Guid executionId)
    {
        lock (_lock)
        {
            if (_channels.TryGetValue(executionId, out var channel))
            {
                channel.Writer.Complete();
                _channels.Remove(executionId);
            }
        }
    }
}

