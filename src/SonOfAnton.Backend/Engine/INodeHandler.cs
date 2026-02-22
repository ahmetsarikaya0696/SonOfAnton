using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Engine;

/// <summary>
/// Node execution context - node handler'lara ge�irilen ba�lam
/// </summary>
public class NodeExecutionContext
{
    /// <summary>
    /// Execution ID
    /// </summary>
    public Guid ExecutionId { get; init; }
    
    /// <summary>
    /// Node bilgileri
    /// </summary>
    public required WorkflowNodeDto Node { get; init; }
    
    /// <summary>
    /// �nceki node'dan gelen veri
    /// </summary>
    public object? InputData { get; init; }
    
    /// <summary>
    /// Ad�mlar aras� payla��lan bellek
    /// </summary>
    public Dictionary<string, object?> Memory { get; init; } = [];
    
    /// <summary>
    /// İptal token'ı
    /// </summary>
    public CancellationToken CancellationToken { get; set; }
    
    /// <summary>
    /// Log yazmak iin callback
    /// </summary>
    public required Action<string, Shared.Enums.LogLevel, object?> Log { get; init; }
    
    /// <summary>
    /// Saga / Compensating action kaydetmek için delegate
    /// </summary>
    public Action<CompensatingAction>? RegisterCompensation { get; init; }
}

/// <summary>
/// Node handler sonucu
/// </summary>
public class NodeExecutionResult
{
    public bool Success { get; init; }
    public object? OutputData { get; init; }
    public string? Error { get; init; }
    
    /// <summary>
    /// Hangi port'tan k yaplacak (if/switch gibi node'lar iin)
    /// </summary>
    public string OutputPort { get; init; } = "output";
    
    public static NodeExecutionResult Ok(object? data = null, string port = "output") 
        => new() { Success = true, OutputData = data, OutputPort = port };
    
    public static NodeExecutionResult Fail(string error) 
        => new() { Success = false, Error = error };
}

/// <summary>
/// Saga pattern için telafi işlemi (Compensating action) tanımı
/// </summary>
public class CompensatingAction
{
    public Guid OriginalNodeId { get; set; }
    public string TargetNodeType { get; set; } = string.Empty;
    public Dictionary<string, object?> Parameters { get; set; } = [];
}

/// <summary>
/// Node handler arayz - Strategy Pattern
/// Her node tipi iin bir implementasyon
/// </summary>
public interface INodeHandler
{
    /// <summary>
    /// Bu handler'n ileyecei node tipi
    /// </summary>
    string NodeType { get; }
    
    /// <summary>
    /// Node'u �al��t�r
    /// </summary>
    Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context);
}

