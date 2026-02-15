namespace SonOfAnton.Shared.Models;

/// <summary>
/// Kullanıcının backend'e gönderdiği istek
/// </summary>
public class UserRequest
{
    /// <summary>
    /// Kullanıcının doğal dilde isteği
    /// </summary>
    public required string Prompt { get; set; }

    /// <summary>
    /// Session ID (opsiyonel, devam eden session için)
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Orchestrator çalışma modu
    /// </summary>
    public Enums.OrchestratorMode Mode { get; set; } = Enums.OrchestratorMode.CodeGeneration;

    /// <summary>
    /// Workflow context (WorkflowAssistant modu için mevcut graph)
    /// </summary>
    public DTOs.WorkflowGraphDto? WorkflowContext { get; set; }
}

