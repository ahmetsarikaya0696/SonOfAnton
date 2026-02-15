using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Agents;

/// <summary>
/// Chat tabanlı workflow oluşturma / düzenleme asistanı
/// </summary>
public class WorkflowAssistantService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<WorkflowAssistantService> _logger;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public WorkflowAssistantService(Kernel kernel, ILogger<WorkflowAssistantService> logger)
    {
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
        _logger = logger;
    }

    /// <summary>
    /// Doğal dilden workflow graph üretir veya mevcut olanı düzenler
    /// </summary>
    public async Task<WorkflowChatResponse> ProcessChatAsync(
        WorkflowChatRequest request, CancellationToken ct = default)
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(WorkflowAssistantPrompts.SystemPrompt);

        // Geçmiş mesajları ekle
        foreach (var msg in request.History)
        {
            if (msg.Role == "user")
                chatHistory.AddUserMessage(msg.Content);
            else
                chatHistory.AddAssistantMessage(msg.Content);
        }

        // Mevcut graph context'i varsa ekle
        if (request.CurrentGraph != null)
        {
            chatHistory.AddUserMessage(
                $"[MEVCUT WORKFLOW]\n```json\n{JsonSerializer.Serialize(request.CurrentGraph, _jsonOptions)}\n```");
        }

        chatHistory.AddUserMessage(request.Message);

        var response = await _chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: ct);
        var content = response.Content ?? string.Empty;

        _logger.LogDebug("Workflow assistant raw: {Response}", content[..Math.Min(200, content.Length)]);

        return ParseResponse(content);
    }

    private WorkflowChatResponse ParseResponse(string content)
    {
        var response = new WorkflowChatResponse();

        // JSON bloğu varsa graph olarak parse et
        var jsonStart = content.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        var jsonEnd = jsonStart >= 0 ? content.IndexOf("```", jsonStart + 7, StringComparison.OrdinalIgnoreCase) : -1;

        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            var jsonBlock = content[(jsonStart + 7)..jsonEnd].Trim();
            try
            {
                response.SuggestedGraph = JsonSerializer.Deserialize<WorkflowGraphDto>(jsonBlock, _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Workflow graph JSON parse başarısız");
            }

            // JSON bloğu dışındaki metni mesaj olarak kullan
            response.Message = content[..jsonStart].Trim();
            if (jsonEnd + 3 < content.Length)
                response.Message += "\n" + content[(jsonEnd + 3)..].Trim();
        }
        else
        {
            response.Message = content;
        }

        // YAML bloğu varsa preview olarak ekle
        var yamlStart = content.IndexOf("```yaml", StringComparison.OrdinalIgnoreCase);
        var yamlEnd = yamlStart >= 0 ? content.IndexOf("```", yamlStart + 7, StringComparison.OrdinalIgnoreCase) : -1;

        if (yamlStart >= 0 && yamlEnd > yamlStart)
        {
            response.YamlPreview = content[(yamlStart + 7)..yamlEnd].Trim();
        }

        return response;
    }
}

/// <summary>
/// Workflow asistan prompt'ları
/// </summary>
public static class WorkflowAssistantPrompts
{
    public const string SystemPrompt = """
        You are a Workflow Design Assistant for SonOfAnton, a visual workflow automation platform.
        You help users create and modify workflows through natural language conversation.

        AVAILABLE NODE TYPES:
        - trigger.manual: Manuel tetikleyici (başlangıç noktası)
        - trigger.webhook: Webhook tetikleyici
        - trigger.schedule: Zamanlayıcı tetikleyici
        - http.request: HTTP isteği (GET/POST/PUT/DELETE)
        - transform.set: Değer atama / veri dönüşümü
        - transform.filter: Veri filtreleme
        - transform.code: Kod çalıştırma
        - transform.template: Şablon uygulama
        - transform.aggregate: Veri toplama
        - logic.if: Koşullu dallanma
        - logic.switch: Çoklu dallanma
        - logic.merge: Dalları birleştirme
        - ai.llm: LLM çağrısı (Ollama)
        - ai.embed: Embedding oluşturma
        - vector.store: Vektör kaydetme
        - vector.search: Vektör arama
        - db.query: Veritabanı sorgusu
        - comm.email: E-posta gönderme
        - comm.slack: Slack mesajı
        - util.delay: Bekleme
        - util.debug: Debug log
        - util.end: Bitiş node'u
        - subflow.foreach: ForEach döngüsü
        - subflow.while: While döngüsü
        - subflow.parallel: Paralel çalıştırma

        RESPONSE FORMAT:
        1. Açıklama metni yazın
        2. Workflow graph'ı JSON olarak verin (```json ... ``` bloğunda)
        3. Graph format: {"nodes": [...], "connections": [...], "variables": [...]}
        4. Her node: {"id": "guid", "type": "...", "name": "...", "icon": "...", "color": "#hex", "x": 0, "y": 0, "parameters": {}}
        5. Her connection: {"sourceNodeId": "guid", "targetNodeId": "guid"}

        RULES:
        - Always include at least one trigger node as the starting point
        - Place nodes in a logical left-to-right flow (x increases by ~250)
        - Use Turkish for node names
        - Include appropriate parameters for each node type
        - If modifying an existing workflow, keep unchanged nodes intact
        """;
}
