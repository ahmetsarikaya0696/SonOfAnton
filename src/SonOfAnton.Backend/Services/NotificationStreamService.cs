using System.Collections.Concurrent;
using System.Threading.Channels;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Services;

/// <summary>
/// Server Sent Events (SSE) bildirim yöneticisi.
/// Bağlı olan her kullanıcının bir veya daha fazla aktif kanalı (channel) olacaktır.
/// </summary>
public class NotificationStreamService
{
    // Kullanıcı ID'sini, o kullanıcıya ait aktif kanal (session) listesine eşleyen sözlük
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<ChannelEntry>> _userChannels = new();

    /// <summary>
    /// Belirtilen kullanıcı ve oturum için bir kanal döndürür. Yoksa oluşturur.
    /// Not: Bir kullanıcı birden fazla sekmeden/cihazdan bağlanabilir, bu nedenle her oturum için kendi kanallarını alırlar.
    /// </summary>
    public Channel<NotificationDto> CreateChannelForUser(Guid userId, string connectionId)
    {
        var bag = _userChannels.GetOrAdd(userId, _ => new ConcurrentBag<ChannelEntry>());
        
        var channel = Channel.CreateUnbounded<NotificationDto>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        bag.Add(new ChannelEntry { ConnectionId = connectionId, Channel = channel });

        return channel;
    }

    /// <summary>
    /// Bir kullanıcı bağlantıyı kestiğinde belirli bir kanalı kaldırır.
    /// </summary>
    public void RemoveChannel(Guid userId, string connectionId)
    {
        if (_userChannels.TryGetValue(userId, out var bag))
        {
            // ConcurrentBag doğrudan silme işlemini iyi desteklemez, sadece işaretler veya yoksayarız
            // ancak filtreleyebilir veya daha ağır bir implementasyonda bag'i yeniden oluşturabiliriz.
            // SSE için daha basit bir yaklaşım, yazıcının yazmaya çalışması ve başarısız olursa/okuyucu tamamlarsa atılmasıdır.
            
            // Düzgün bir şekilde kaldırmak için onu değiştireceğiz:
            var currentEntries = bag.ToList();
            var toRemove = currentEntries.FirstOrDefault(e => e.ConnectionId == connectionId);
            if (toRemove != null)
            {
                toRemove.Channel.Writer.TryComplete();
                
                var newBag = new ConcurrentBag<ChannelEntry>(currentEntries.Where(e => e.ConnectionId != connectionId));
                _userChannels.TryUpdate(userId, newBag, bag);
            }
        }
    }

    /// <summary>
    /// Belirtilen kullanıcı için aktif olan tüm kanallara bir bildirim gönderir.
    /// </summary>
    public async Task PublishToUserAsync(Guid userId, NotificationDto payload)
    {
        if (_userChannels.TryGetValue(userId, out var bag))
        {
            foreach (var entry in bag)
            {
                // Kanala yazmayı dene. Kapalı/tamamlanmışsa hata vermeden geçecektir.
                try
                {
                    await entry.Channel.Writer.WriteAsync(payload);
                }
                catch (ChannelClosedException)
                {
                    // Yoksay, okuyucu bağlantıyı kesti
                }
            }
        }
    }

    private class ChannelEntry
    {
        public required string ConnectionId { get; init; }
        public required Channel<NotificationDto> Channel { get; init; }
    }
}
