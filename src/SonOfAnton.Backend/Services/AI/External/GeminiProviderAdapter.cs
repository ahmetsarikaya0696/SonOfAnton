using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SonOfAnton.Shared.Core;

namespace SonOfAnton.Backend.Services.AI.External;

/// <summary>
/// Google Gemini (Generative AI) API adaptörü.
/// generateContent ve streamGenerateContent endpoint'leri üzerinden erişim.
/// Vision, Streaming destekli.
/// </summary>
public class GeminiProviderAdapter : IProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiProviderAdapter> _logger;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public AIProviderType ProviderType => AIProviderType.Gemini;
    public bool SupportsVision => true;
    public bool SupportsJsonMode => true;
    public bool SupportsStreaming => true;
    public string DefaultModel => "gemini-2.0-flash";
    public IReadOnlyList<string> AvailableModels => [
        "gemini-2.0-flash", "gemini-2.0-flash-lite", "gemini-1.5-pro", "gemini-1.5-flash"
    ];

    public GeminiProviderAdapter(IHttpClientFactory httpClientFactory, ILogger<GeminiProviderAdapter> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Gemini");
        _logger = logger;
    }

    public async Task<string> CompleteAsync(
        ExternalAIRequest request,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var model = request.ModelId ?? DefaultModel;
        var payload = BuildRequestPayload(request);
        var url = $"{BaseUrl}/{model}:generateContent?key={apiKey}";

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await JsonSerializer.DeserializeAsync<JsonElement>(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        var content = responseJson.GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        _logger.LogDebug("Gemini tamamlama başarılı. Model: {Model}, Uzunluk: {Length}",
            model, content.Length);

        return content;
    }

    public async IAsyncEnumerable<string> StreamCompleteAsync(
        ExternalAIRequest request,
        string apiKey,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var model = request.ModelId ?? DefaultModel;
        var payload = BuildRequestPayload(request);
        var url = $"{BaseUrl}/{model}:streamGenerateContent?alt=sse&key={apiKey}";

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(
            httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrEmpty(line)) continue;

            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];

            var chunks = new List<string>();
            try
            {
                var responseJson = JsonDocument.Parse(data);

                if (responseJson.RootElement.TryGetProperty("candidates", out var candidates))
                {
                    foreach (var candidate in candidates.EnumerateArray())
                    {
                        if (candidate.TryGetProperty("content", out var content) &&
                            content.TryGetProperty("parts", out var parts))
                        {
                            foreach (var part in parts.EnumerateArray())
                            {
                                if (part.TryGetProperty("text", out var textProp))
                                {
                                    var text = textProp.GetString();
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        chunks.Add(text);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Gemini SSE chunk parse hatası: {Data}", data);
            }

            foreach (var chunk in chunks)
            {
                yield return chunk;
            }
        }
    }

    // ─── Private Helpers ──────────────────────────────────────────

    private object BuildRequestPayload(ExternalAIRequest request)
    {
        var contents = new List<object>();
        string? systemInstruction = null;

        foreach (var msg in request.Messages)
        {
            if (msg.Role == ChatMessageRole.System)
            {
                systemInstruction = msg.Content;
                continue;
            }

            var parts = new List<object>();

            // Vision: görüntü verisi
            if (msg.Role == ChatMessageRole.User && request.ImageData != null)
            {
                parts.Add(new
                {
                    inlineData = new
                    {
                        mimeType = request.ImageMimeType ?? "image/png",
                        data = Convert.ToBase64String(request.ImageData)
                    }
                });
            }

            parts.Add(new { text = msg.Content });

            contents.Add(new
            {
                role = MapRole(msg.Role),
                parts
            });
        }

        var payload = new Dictionary<string, object>
        {
            ["contents"] = contents,
            ["generationConfig"] = new
            {
                temperature = request.Temperature,
                maxOutputTokens = request.MaxTokens ?? 4096,
                responseMimeType = request.ResponseFormat == AIResponseFormat.JsonObject
                    ? "application/json"
                    : "text/plain"
            }
        };

        if (systemInstruction != null)
        {
            payload["systemInstruction"] = new
            {
                parts = new[] { new { text = systemInstruction } }
            };
        }

        return payload;
    }

    private static string MapRole(ChatMessageRole role) => role switch
    {
        ChatMessageRole.User => "user",
        ChatMessageRole.Assistant => "model",
        _ => "user"
    };
}
