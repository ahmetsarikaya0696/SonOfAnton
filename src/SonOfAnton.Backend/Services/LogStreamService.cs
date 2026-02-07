using System.Collections.Concurrent;
using System.Threading.Channels;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Services;

/// <summary>
/// In-memory log stream servisi - SSE için
/// ConcurrentDictionary ile thread-safe, stale channel temizleme
/// </summary>
public class LogStreamService
{
    private readonly ConcurrentDictionary<Guid, ChannelEntry> _channels = new();

    /// <summary>
    /// Aktif channel sayısı
    /// </summary>
    public int ActiveChannelCount => _channels.Count;

    /// <summary>
    /// Execution için channel oluştur veya var olanı getir
    /// </summary>
    public Channel<LogEventDto> GetOrCreateChannel(Guid executionId)
    {
        var entry = _channels.GetOrAdd(executionId, _ => new ChannelEntry
        {
            Channel = Channel.CreateUnbounded<LogEventDto>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            }),
            CreatedAt = DateTime.UtcNow
        });
        
        entry.LastAccessedAt = DateTime.UtcNow;
        return entry.Channel;
    }

    /// <summary>
    /// Log yaz (Writer tarafı - Hangfire job'ları)
    /// </summary>
    public async Task WriteAsync(Guid executionId, LogEventDto logEvent)
    {
        var channel = GetOrCreateChannel(executionId);
        await channel.Writer.WriteAsync(logEvent);
    }

    /// <summary>
    /// Log oku (Reader tarafı - SSE endpoint)
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
    /// Channel'ı tamamla (execution bittiğinde)
    /// </summary>
    public void Complete(Guid executionId)
    {
        if (_channels.TryGetValue(executionId, out var entry))
        {
            entry.Channel.Writer.TryComplete();
            entry.CompletedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Channel'ı temizle (bellek yönetimi için)
    /// </summary>
    public void Remove(Guid executionId)
    {
        if (_channels.TryRemove(executionId, out var entry))
        {
            entry.Channel.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Eski tamamlanmış channel'ları temizle
    /// </summary>
    public int CleanupStaleChannels(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var removed = 0;

        foreach (var kvp in _channels)
        {
            if (kvp.Value.CompletedAt.HasValue && kvp.Value.CompletedAt.Value < cutoff)
            {
                if (_channels.TryRemove(kvp.Key, out _))
                {
                    removed++;
                }
            }
        }

        return removed;
    }

    private class ChannelEntry
    {
        public required Channel<LogEventDto> Channel { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime LastAccessedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}

