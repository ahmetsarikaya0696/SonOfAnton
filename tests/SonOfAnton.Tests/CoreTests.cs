using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.Core.GraphEngine;
using Xunit;

namespace SonOfAnton.Tests;

public class CoreTests
{
    private static EncryptionService Encryption(byte[]? key = null) => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string,string?> { ["Encryption:Key"] = Convert.ToBase64String(key ?? new byte[32]) }).Build());

    [Fact]
    public void Ciphertext_roundtrips_but_tampering_and_wrong_key_fail()
    {
        var service = Encryption();
        var cipher = service.Encrypt("gizli bilgi Türkçe");
        Assert.Equal("gizli bilgi Türkçe", service.Decrypt(cipher));
        Assert.NotEqual(cipher, service.Encrypt("gizli bilgi Türkçe"));
        var bytes = Convert.FromBase64String(cipher[3..]); bytes[^1] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt("v1:" + Convert.ToBase64String(bytes)));
        Assert.ThrowsAny<CryptographicException>(() => Encryption(RandomNumberGenerator.GetBytes(32)).Decrypt(cipher));
        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt("unversioned-ciphertext"));
    }

    [Fact]
    public void Missing_encryption_key_has_no_fallback() => Assert.Throws<InvalidOperationException>(() => new EncryptionService(new ConfigurationBuilder().Build()));

    [Theory]
    [InlineData("0 9 * * 1-5", true)]
    [InlineData("*/15 * * * *", true)]
    [InlineData("0 0 0 * * *", false)]
    [InlineData("60 * * * *", false)]
    public void Cron_matches_five_field_scheduler(string value, bool valid) => Assert.Equal(valid, WorkflowJobService.ValidateCronExpression(value).IsValid);

    [Fact]
    public async Task Sse_broadcasts_to_each_subscriber_without_replacing_channels()
    {
        var service = new SseEventService(NullLogger<SseEventService>.Instance);
        var id = Guid.NewGuid();
        Assert.Same(service.CreateChannel(id), service.CreateChannel(id));
        var a = service.RegisterSession(id); var b = service.RegisterSession(id);
        var ca = service.GetSessionChannel(a.SessionId); var cb = service.GetSessionChannel(b.SessionId);
        await service.PublishLogAsync(id, null, "info", "one");
        await service.PublishLogAsync(id, null, "info", "two");
        var a1 = await ca.Reader.ReadAsync(); var b1 = await cb.Reader.ReadAsync();
        Assert.Equal(a1.SequenceNumber, b1.SequenceNumber);
        Assert.Equal((await ca.Reader.ReadAsync()).SequenceNumber, (await cb.Reader.ReadAsync()).SequenceNumber);
        service.CompleteChannel(id);
        Assert.False(await ca.Reader.WaitToReadAsync());
        Assert.Equal(0, service.GetStatistics().ActiveSessions);
    }

    [Fact]
    public async Task Slow_sse_subscriber_is_disconnected_instead_of_growing_memory()
    {
        var service = new SseEventService(NullLogger<SseEventService>.Instance);
        var id = Guid.NewGuid(); service.RegisterSession(id);
        for (var i = 0; i < 300; i++) await service.PublishLogAsync(id, null, "info", "data");
        Assert.Equal(0, service.GetStatistics().ActiveSessions);
    }
}
