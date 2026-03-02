using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SonOfAnton.Shared.Core;

namespace SonOfAnton.Backend.Services.AI.External;

/// <summary>
/// OpenAI Chat Completions API adaptörü.
/// /v1/chat/completions endpoint'i üzerinden GPT modellerine erişim.
/// Vision, Streaming, JSON Mode destekli.
/// </summary>
public class OpenAIProviderAdapter : IProviderAdapter
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAIProviderAdapter> _logger;
    private const string BaseUrl = "https://api.openai.com/v1";

    public AIProviderType ProviderType => AIProviderType.OpenAI;
    public bool SupportsVision => true;
    public bool SupportsJsonMode => true;
    public bool SupportsStreaming => true;
    public string DefaultModel => "gpt-4o";
    public IReadOnlyList<string> AvailableModels => [
        "gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-4", "gpt-3.5-turbo", "o1", "o1-mini"
    ];

    public OpenAIProviderAdapter(IHttpClientFactory httpClientFactory, ILogger<OpenAIProviderAdapter> logger)
    {
        _httpClient = httpClientFactory.CreateClient("OpenAI");
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

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var content = json.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        _logger.LogDebug("OpenAI tamamlama başarılı. Model: {Model}, Uzunluk: {Length}",
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

            // SSE format: "data: {...}"
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];

            if (data == "[DONE]") break;

            string? content = null;
            try
            {
                var json = JsonDocument.Parse(data);
                var delta = json.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("delta");

                if (delta.TryGetProperty("content", out var contentProp))
                {
                    content = contentProp.GetString();
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "OpenAI SSE chunk parse hatası: {Data}", data);
            }

            if (!string.IsNullOrEmpty(content))
            {
                yield return content;
            }
        }
    }

    // ─── Private Helpers ──────────────────────────────────────────

    private object BuildRequestPayload(ExternalAIRequest request, bool stream)
    {
        var messages = new List<object>();

        foreach (var msg in request.Messages)
        {
            if (msg.Role == ChatMessageRole.User && request.ImageData != null)
            {
                // Vision: multimodal content
                var content = new List<object>
                {
                    new { type = "text", text = msg.Content }
                };

                var base64Image = Convert.ToBase64String(request.ImageData);
                var mimeType = request.ImageMimeType ?? "image/png";
                content.Add(new
                {
                    type = "image_url",
                    image_url = new { url = $"data:{mimeType};base64,{base64Image}" }
                });

                messages.Add(new
                {
                    role = MapRole(msg.Role),
                    content
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
            ["temperature"] = request.Temperature,
            ["stream"] = stream
        };

        if (request.MaxTokens.HasValue)
        {
            payload["max_tokens"] = request.MaxTokens.Value;
        }

        if (request.ResponseFormat == AIResponseFormat.JsonObject)
        {
            payload["response_format"] = new { type = "json_object" };
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

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        return httpRequest;
    }

    private static string MapRole(ChatMessageRole role) => role switch
    {
        ChatMessageRole.System => "system",
        ChatMessageRole.User => "user",
        ChatMessageRole.Assistant => "assistant",
        _ => "user"
    };
}
