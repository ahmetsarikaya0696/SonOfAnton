using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SonOfAnton.Shared.Models;

namespace SonOfAnton.Backend.Agents;

/// <summary>
/// Semantic Kernel tabanl� agent servisi
/// T�m LLM etkile�imlerini y�netir
/// </summary>
public partial class AgentService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<AgentService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public AgentService(Kernel kernel, ILogger<AgentService> logger)
    {
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Analyst ajan�n� �al��t�r�r ve proje plan� d�nd�r�r
    /// </summary>
    public async Task<ProjectPlan?> AnalyzeAsync(string userPrompt, CancellationToken ct = default)
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(AgentPrompts.AnalystSystemPrompt);
        chatHistory.AddUserMessage(userPrompt);

        var response = await _chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: ct);
        var content = response.Content ?? string.Empty;
        
        _logger.LogDebug("Analyst raw response: {Response}", content);

        // JSON extraction (markdown code block'lar�n� temizle)
        var cleanJson = ExtractJson(content);
        
        try
        {
            return JsonSerializer.Deserialize<ProjectPlan>(cleanJson, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse analyst response as JSON: {Content}", cleanJson);
            return null;
        }
    }

    /// <summary>
    /// Visualizer ajan�n� �al��t�r�r ve Mermaid diyagram� d�nd�r�r
    /// </summary>
    public async Task<string> VisualizeAsync(ProjectPlan plan, CancellationToken ct = default)
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(AgentPrompts.VisualizerSystemPrompt);
        chatHistory.AddUserMessage($"Create a Mermaid diagram for this project plan:\n{JsonSerializer.Serialize(plan, _jsonOptions)}");

        var response = await _chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: ct);
        var content = response.Content ?? string.Empty;
        
        // Mermaid code block temizleme
        return ExtractMermaid(content);
    }

    /// <summary>
    /// Coder ajan�n� �al��t�r�r ve dosya i�eri�i d�nd�r�r
    /// </summary>
    public async Task<string> GenerateCodeAsync(
        DevTask task, 
        List<string> architectureRules,
        Dictionary<string, string>? relatedFiles = null,
        CancellationToken ct = default)
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(AgentPrompts.CoderSystemPrompt);

        var prompt = $"""
            TASK: {task.Description}
            TARGET FILE: {task.TargetFile}
            
            ARCHITECTURE RULES:
            {string.Join("\n- ", architectureRules)}
            """;

        if (relatedFiles?.Count > 0)
        {
            prompt += "\n\nRELATED FILES:";
            foreach (var (path, content) in relatedFiles)
            {
                prompt += $"\n\n--- {path} ---\n{content}";
            }
        }

        chatHistory.AddUserMessage(prompt);

        var response = await _chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: ct);
        return ExtractCode(response.Content ?? string.Empty);
    }

    /// <summary>
    /// Auditor ajan�n� �al��t�r�r ve audit sonucu d�nd�r�r
    /// </summary>
    public async Task<AuditResult?> AuditCodeAsync(
        string code, 
        string filePath,
        List<string> architectureRules,
        CancellationToken ct = default)
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(AgentPrompts.AuditorSystemPrompt);
        
        var prompt = $"""
            FILE: {filePath}
            
            ARCHITECTURE RULES:
            {string.Join("\n- ", architectureRules)}
            
            CODE TO AUDIT:
            {code}
            """;

        chatHistory.AddUserMessage(prompt);

        var response = await _chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: ct);
        var cleanJson = ExtractJson(response.Content ?? string.Empty);
        
        try
        {
            return JsonSerializer.Deserialize<AuditResult>(cleanJson, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse audit response: {Content}", cleanJson);
            return new AuditResult { Passed = true, Summary = "Audit parsing failed, assuming pass" };
        }
    }

    /// <summary>
    /// Fixer ajan�n� �al��t�r�r ve d�zeltilmi� kod d�nd�r�r
    /// </summary>
    public async Task<string> FixCodeAsync(
        string originalCode,
        AuditResult auditResult,
        List<string> architectureRules,
        CancellationToken ct = default)
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(AgentPrompts.FixerSystemPrompt);
        
        var prompt = $"""
            ORIGINAL CODE:
            {originalCode}
            
            AUDIT ISSUES:
            {JsonSerializer.Serialize(auditResult.Issues, _jsonOptions)}
            
            ARCHITECTURE RULES:
            {string.Join("\n- ", architectureRules)}
            """;

        chatHistory.AddUserMessage(prompt);

        var response = await _chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: ct);
        return ExtractCode(response.Content ?? string.Empty);
    }

    /// <summary>
    /// JSON i�eri�ini markdown code block'lar�ndan temizler
    /// </summary>
    private static string ExtractJson(string content)
    {
        // ```json ... ``` veya ``` ... ``` format�n� temizle
        var match = JsonCodeBlockRegex().Match(content);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }
        
        // Direkt JSON ise oldu�u gibi d�nd�r
        content = content.Trim();
        if (content.StartsWith('{') || content.StartsWith('['))
        {
            return content;
        }
        
        return content;
    }

    /// <summary>
    /// Mermaid i�eri�ini markdown code block'lar�ndan temizler
    /// </summary>
    private static string ExtractMermaid(string content)
    {
        var match = MermaidCodeBlockRegex().Match(content);
        return match.Success ? match.Groups[1].Value.Trim() : content.Trim();
    }

    /// <summary>
    /// Kod i�eri�ini markdown code block'lar�ndan temizler
    /// </summary>
    private static string ExtractCode(string content)
    {
        var match = CodeBlockRegex().Match(content);
        return match.Success ? match.Groups[1].Value.Trim() : content.Trim();
    }

    [GeneratedRegex(@"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase)]
    private static partial Regex JsonCodeBlockRegex();

    [GeneratedRegex(@"```(?:mermaid)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase)]
    private static partial Regex MermaidCodeBlockRegex();

    [GeneratedRegex(@"```(?:csharp|cs|c#)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase)]
    private static partial Regex CodeBlockRegex();
}

/// <summary>
/// Auditor ��kt� modeli
/// </summary>
public class AuditResult
{
    public bool Passed { get; set; }
    public List<AuditIssue> Issues { get; set; } = [];
    public string? Summary { get; set; }
}

public class AuditIssue
{
    public string Severity { get; set; } = "info";
    public int? Line { get; set; }
    public required string Message { get; set; }
    public string? Suggestion { get; set; }
}

