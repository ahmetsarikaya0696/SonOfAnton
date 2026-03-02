using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SonOfAnton.Shared.Core;

namespace SonOfAnton.Backend.Services.AI.External;

/// <summary>
/// Anthropic Claude Messages API adaptörü.
/// /v1/messages endpoint'i üzerinden Claude modellerine erişim.
/// Vision, Streaming destekli.
/// </summary>
public class ClaudeProviderAdapter : IProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClaudeProviderAdapter> _logger;
    private const string BaseUrl = "https://api.anthropic.com/v1";
    private const string ApiVersion = "2023-06-01";

    public AIProviderType ProviderType => AIProviderType.Claude;
    public bool SupportsVision => true;
    public bool SupportsJsonMode => false;
    public bool SupportsStreaming => true;
    public string DefaultModel => "claude-sonnet-4-20250514";
    public IReadOnlyList<string> AvailableModels => [
        "claude-sonnet-4-20250514", "claude-3-5-sonnet-20241022",
        "claude-3-5-haiku-20241022", "claude-3-opus-20240229"
    ];

    public ClaudeProviderAdapter(IHttpClientFactory httpClientFactory, ILogger<ClaudeProviderAdapter> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Claude");
        _logger = logger;
    }

    public async Task<string> CompleteAsync(
        ExternalAIRequest request,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var payload = BuildRequestPayload(request, stream: false);
        using var httpRequest = CreateHttpRequest(apiKey, payload);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await JsonSerializer.DeserializeAsync<JsonElement>(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        var contentBlocks = json.GetProperty("content");
        var sb = new StringBuilder();
        foreach (var block in contentBlocks.EnumerateArray())
        {
            if (block.GetProperty("type").GetString() == "text")
            {
                sb.Append(block.GetProperty("text").GetString());
            }
        }

        var content = sb.ToString();

        _logger.LogDebug("Claude tamamlama başarılı. Model: {Model}, Uzunluk: {Length}",
            request.ModelId ?? DefaultModel, content.Length);

        return content;
    }

    public async IAsyncEnumerable<string> StreamCompleteAsync(
        ExternalAIRequest request,
        string apiKey,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = BuildRequestPayload(request, stream: true);
        using var httpRequest = CreateHttpRequest(apiKey, payload);

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

            // SSE format: "event: ..." ve "data: {...}"
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];

            string? text = null;
            bool shouldBreak = false;
            try
            {
                var json = JsonDocument.Parse(data);
                var type = json.RootElement.GetProperty("type").GetString();

                if (type == "content_block_delta")
                {
                    var delta = json.RootElement.GetProperty("delta");
                    if (delta.GetProperty("type").GetString() == "text_delta")
                    {
                        text = delta.GetProperty("text").GetString();
                    }
                }
                else if (type == "message_stop")
                {
                    shouldBreak = true;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Claude SSE chunk parse hatası: {Data}", data);
            }

            if (shouldBreak) break;

            if (!string.IsNullOrEmpty(text))
            {
                yield return text;
            }
        }
    }

    // ─── Private Helpers ──────────────────────────────────────────

    private object BuildRequestPayload(ExternalAIRequest request, bool stream)
    {
        // Claude: system mesajı ayrı parametre olarak gönderilir
        string? systemPrompt = null;
        var messages = new List<object>();

        foreach (var msg in request.Messages)
        {
            if (msg.Role == ChatMessageRole.System)
            {
                systemPrompt = msg.Content;
                continue;
            }

            if (msg.Role == ChatMessageRole.User && request.ImageData != null)
            {
                // Vision: multimodal content
                var base64Image = Convert.ToBase64String(request.ImageData);
                var mimeType = request.ImageMimeType ?? "image/png";

                messages.Add(new
                {
                    role = MapRole(msg.Role),
                    content = new object[]
                    {
                        new
                        {
                            type = "image",
                            source = new
                            {
                                type = "base64",
                                media_type = mimeType,
                                data = base64Image
                            }
                        },
                        new { type = "text", text = msg.Content }
                    }
                });
            }
            else
            {
                messages.Add(new
                {
                    role = MapRole(msg.Role),
                    content = msg.Content
                });
            }
        }

        var payload = new Dictionary<string, object>
        {
            ["model"] = request.ModelId ?? DefaultModel,
            ["messages"] = messages,
            ["max_tokens"] = request.MaxTokens ?? 4096,
            ["temperature"] = request.Temperature,
            ["stream"] = stream
        };

        if (systemPrompt != null)
        {
            payload["system"] = systemPrompt;
        }

        return payload;
    }

    private HttpRequestMessage CreateHttpRequest(string apiKey, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/messages")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        httpRequest.Headers.Add("x-api-key", apiKey);
        httpRequest.Headers.Add("anthropic-version", ApiVersion);

        return httpRequest;
    }

    private static string MapRole(ChatMessageRole role) => role switch
    {
        ChatMessageRole.User => "user",
        ChatMessageRole.Assistant => "assistant",
        _ => "user"
    };
}
