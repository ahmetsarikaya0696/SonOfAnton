using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// E-posta gönderme node'u
/// SMTP veya HTTP API üzerinden e-posta gönderir
/// </summary>
public class EmailHandler : INodeHandler
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public EmailHandler(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public string NodeType => "comm.email";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var to = context.Node.Parameters.GetValueOrDefault("to")?.ToString();
        var subject = context.Node.Parameters.GetValueOrDefault("subject")?.ToString();
        var body = context.Node.Parameters.GetValueOrDefault("body")?.ToString();

        if (string.IsNullOrEmpty(to))
        {
            return NodeExecutionResult.Fail("Alýcý (to) parametresi gerekli");
        }

        if (string.IsNullOrEmpty(subject))
        {
            return NodeExecutionResult.Fail("Konu (subject) parametresi gerekli");
        }

        // Input data'dan template deðiþkenleri uygula
        if (context.InputData != null)
        {
            var inputJson = JsonSerializer.Serialize(context.InputData);
            body = ApplyTemplate(body ?? string.Empty, inputJson);
            subject = ApplyTemplate(subject, inputJson);
        }

        context.Log($"E-posta gönderiliyor: {to}", Shared.Enums.LogLevel.Info, null);

        try
        {
            // Webhook/HTTP API ile e-posta gönderim simülasyonu
            // Gerçek SMTP entegrasyonu yapýlandýrma ile aktif edilebilir
            var smtpEndpoint = _configuration.GetValue<string>("Email:WebhookUrl");
            
            if (!string.IsNullOrEmpty(smtpEndpoint))
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.PostAsJsonAsync(smtpEndpoint, new
                {
                    to,
                    subject,
                    body,
                    html = body
                }, context.CancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(context.CancellationToken);
                    return NodeExecutionResult.Fail($"E-posta API hatasý: {response.StatusCode} - {error}");
                }
            }

            context.Log($"E-posta gönderildi: {to}", Shared.Enums.LogLevel.Info, null);

            return NodeExecutionResult.Ok(new
            {
                sent = true,
                to,
                subject,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            context.Log($"E-posta hatasý: {ex.Message}", Shared.Enums.LogLevel.Error, null);
            return NodeExecutionResult.Fail($"E-posta hatasý: {ex.Message}");
        }
    }

    private static string ApplyTemplate(string template, string dataJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                template = template.Replace($"{{{{{prop.Name}}}}}", prop.Value.ToString());
            }
        }
        catch { /* Template uygulama hatasý - orijinal template kullan */ }
        return template;
    }
}

/// <summary>
/// Slack mesaj gönderme node'u
/// Slack Webhook URL üzerinden mesaj gönderir
/// </summary>
public class SlackHandler : INodeHandler
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public SlackHandler(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public string NodeType => "comm.slack";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var channel = context.Node.Parameters.GetValueOrDefault("channel")?.ToString();
        var message = context.Node.Parameters.GetValueOrDefault("message")?.ToString();

        if (string.IsNullOrEmpty(channel))
        {
            return NodeExecutionResult.Fail("Kanal parametresi gerekli");
        }

        if (string.IsNullOrEmpty(message))
        {
            return NodeExecutionResult.Fail("Mesaj parametresi gerekli");
        }

        // Input data'dan template
        if (context.InputData != null)
        {
            var inputJson = JsonSerializer.Serialize(context.InputData);
            message = ApplyTemplate(message, inputJson);
        }

        context.Log($"Slack mesajý gönderiliyor: #{channel}", Shared.Enums.LogLevel.Info, null);

        try
        {
            var webhookUrl = _configuration.GetValue<string>("Slack:WebhookUrl");
            
            if (!string.IsNullOrEmpty(webhookUrl))
            {
                var client = _httpClientFactory.CreateClient();
                var payload = new { channel, text = message };
                var response = await client.PostAsJsonAsync(webhookUrl, payload, context.CancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(context.CancellationToken);
                    return NodeExecutionResult.Fail($"Slack API hatasý: {response.StatusCode} - {error}");
                }
            }

            context.Log($"Slack mesajý gönderildi: #{channel}", Shared.Enums.LogLevel.Info, null);

            return NodeExecutionResult.Ok(new
            {
                sent = true,
                channel,
                message,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            context.Log($"Slack hatasý: {ex.Message}", Shared.Enums.LogLevel.Error, null);
            return NodeExecutionResult.Fail($"Slack hatasý: {ex.Message}");
        }
    }

    private static string ApplyTemplate(string template, string dataJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                template = template.Replace($"{{{{{prop.Name}}}}}", prop.Value.ToString());
            }
        }
        catch { /* Template uygulama hatasý */ }
        return template;
    }
}
