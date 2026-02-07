using System.Text.Json;
using System.Threading.Channels;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Frontend.Services;

/// <summary>
/// SSE log stream servisi - Backend'den gelen logları okur
/// Exponential backoff ile otomatik yeniden bağlanma
/// </summary>
public class LogStreamService
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public LogStreamService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Bağlantı durumu
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// Aktif bağlantı sayısı
    /// </summary>
    public int RetryAttempt { get; private set; }

    /// <summary>
    /// Execution loglarını stream olarak oku
    /// Bağlantı kesilirse exponential backoff ile yeniden bağlan
    /// </summary>
    public async IAsyncEnumerable<LogEventDto> StreamLogsAsync(
        Guid executionId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<LogEventDto>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
        
        _ = Task.Run(async () =>
        {
            RetryAttempt = 0;
            const int maxRetries = 5;
            const int baseDelayMs = 500;
            const int maxDelayMs = 30_000;

            while (RetryAttempt < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                HttpResponseMessage? response = null;
                
                try
                {
                    var url = $"api/logs/{executionId}";
                    
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Accept.ParseAdd("text/event-stream");
                    
                    response = await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);

                    response.EnsureSuccessStatusCode();
                    IsConnected = true;
                    RetryAttempt = 0; // Başarılı bağlantı, retry'ı sıfırla

                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var reader = new StreamReader(stream);

                    while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync(cancellationToken);
                        
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        if (line.StartsWith("data: "))
                        {
                            var json = line[6..].Trim();
                            if (json is "{}" or "")
                                continue;

                            try
                            {
                                var logEvent = JsonSerializer.Deserialize<LogEventDto>(json, JsonOptions);
                                if (logEvent != null)
                                {
                                    await channel.Writer.WriteAsync(logEvent, cancellationToken);
                                }
                            }
                            catch (JsonException)
                            {
                                // Hatalı JSON atla
                            }
                        }
                        else if (line.StartsWith("event: complete"))
                        {
                            break;
                        }
                        // Heartbeat yorumlarını sessizce atla
                    }
                    
                    break; // Normal tamamlanma
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    IsConnected = false;
                    RetryAttempt++;
                    
                    if (RetryAttempt < maxRetries)
                    {
                        // Exponential backoff with jitter
                        var delay = Math.Min(
                            baseDelayMs * (int)Math.Pow(2, RetryAttempt - 1) + Random.Shared.Next(0, 500),
                            maxDelayMs);
                        
                        await Task.Delay(delay, cancellationToken);
                    }
                }
                finally
                {
                    response?.Dispose();
                }
            }
            
            IsConnected = false;
            channel.Writer.Complete();
        }, cancellationToken);

        await foreach (var log in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return log;
        }
    }
}

