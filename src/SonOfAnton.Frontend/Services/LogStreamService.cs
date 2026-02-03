using System.Text.Json;
using System.Threading.Channels;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Frontend.Services;

/// <summary>
/// SSE log stream servisi - Backend'den gelen loglar� okur
/// </summary>
public class LogStreamService
{
    private readonly HttpClient _httpClient;

    public LogStreamService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Execution loglar�n� stream olarak oku
    /// Ba�lant� kesilirse otomatik yeniden ba�lan (retry policy)
    /// </summary>
    public async IAsyncEnumerable<LogEventDto> StreamLogsAsync(
        Guid executionId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[LogStreamService] StreamLogsAsync started for: {executionId}");
        Console.WriteLine($"[LogStreamService] HttpClient BaseAddress: {_httpClient.BaseAddress}");
        
        var channel = Channel.CreateUnbounded<LogEventDto>();
        
        _ = Task.Run(async () =>
        {
            var retryCount = 0;
            const int maxRetries = 3;

            while (retryCount < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                HttpResponseMessage? response = null;
                
                try
                {
                    var url = $"api/logs/{executionId}";
                    Console.WriteLine($"[LogStreamService] Connecting to: {url}");
                    
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Accept.ParseAdd("text/event-stream");
                    
                    response = await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);

                    Console.WriteLine($"[LogStreamService] Response status: {response.StatusCode}");
                    response.EnsureSuccessStatusCode();

                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var reader = new StreamReader(stream);

                    Console.WriteLine("[LogStreamService] Stream opened, reading lines...");

                    while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync(cancellationToken);
                        
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        Console.WriteLine($"[LogStreamService] Received line: {line[..Math.Min(100, line.Length)]}");


                        if (line.StartsWith("data: "))
                        {
                            var json = line[6..].Trim();
                            if (json == "{}")
                                continue;

                            var logEvent = JsonSerializer.Deserialize<LogEventDto>(json, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (logEvent != null)
                            {
                                Console.WriteLine($"[LogStreamService] Parsed log: {logEvent.Message}");
                                retryCount = 0;
                                await channel.Writer.WriteAsync(logEvent, cancellationToken);
                            }
                        }
                        else if (line.StartsWith("event: complete"))
                        {
                            Console.WriteLine("[LogStreamService] Received complete event");
                            break;
                        }
                    }
                    
                    Console.WriteLine("[LogStreamService] Stream reading completed normally");
                    break; // Normal completion
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("[LogStreamService] Operation cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LogStreamService] Error: {ex.Message}");
                    retryCount++;
                    if (retryCount < maxRetries)
                    {
                        Console.WriteLine($"[LogStreamService] Retrying ({retryCount}/{maxRetries})...");
                        await Task.Delay(1000 * retryCount, cancellationToken);
                    }
                }
                finally
                {
                    response?.Dispose();
                }
            }
            
            Console.WriteLine("[LogStreamService] Channel writer completing");
            channel.Writer.Complete();
        }, cancellationToken);

        await foreach (var log in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return log;
        }
        
        Console.WriteLine("[LogStreamService] StreamLogsAsync finished");
    }
}

