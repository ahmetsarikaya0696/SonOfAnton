using System.Text.Json;

namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// HTTP istek node'u
/// </summary>
public class HttpRequestHandler : INodeHandler
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpRequestHandler(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string NodeType => "http.request";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var url = context.Node.Parameters.GetValueOrDefault("url")?.ToString();
        var method = context.Node.Parameters.GetValueOrDefault("method")?.ToString() ?? "GET";
        var headersJson = context.Node.Parameters.GetValueOrDefault("headers")?.ToString();
        var bodyJson = context.Node.Parameters.GetValueOrDefault("body")?.ToString();

        if (string.IsNullOrEmpty(url))
        {
            return NodeExecutionResult.Fail("URL parametresi gerekli");
        }

        context.Log($"HTTP {method} iste�i g�nderiliyor: {url}", Shared.Enums.LogLevel.Info, null);

        try
        {
            var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(new HttpMethod(method), url);

            // Headers
            if (!string.IsNullOrEmpty(headersJson))
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
            }

            // Body
            if (!string.IsNullOrEmpty(bodyJson) && method != "GET")
            {
                request.Content = new StringContent(bodyJson, System.Text.Encoding.UTF8, "application/json");
            }

            var response = await client.SendAsync(request, context.CancellationToken);
            var content = await response.Content.ReadAsStringAsync(context.CancellationToken);

            context.Log($"HTTP yan�t� al�nd�: {(int)response.StatusCode}", Shared.Enums.LogLevel.Info, null);

            // Try to parse JSON response
            object? parsedContent = content;
            try
            {
                parsedContent = JsonSerializer.Deserialize<JsonElement>(content);
            }
            catch
            {
                // Not JSON, keep as string
            }

            return NodeExecutionResult.Ok(new
            {
                statusCode = (int)response.StatusCode,
                body = parsedContent,
                headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
            });
        }
        catch (Exception ex)
        {
            context.Log($"HTTP iste�i ba�ar�s�z: {ex.Message}", Shared.Enums.LogLevel.Error, null);
            return NodeExecutionResult.Fail($"HTTP iste�i ba�ar�s�z: {ex.Message}");
        }
    }
}

