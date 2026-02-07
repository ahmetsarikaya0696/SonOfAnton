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
    /// Log yazmak i�in callback
    /// </summary>
    public required Action<string, Shared.Enums.LogLevel, object?> Log { get; init; }
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
    /// Hangi port'tan ��k�� yap�lacak (if/switch gibi node'lar i�in)
    /// </summary>
    public string OutputPort { get; init; } = "output";
    
    public static NodeExecutionResult Ok(object? data = null, string port = "output") 
        => new() { Success = true, OutputData = data, OutputPort = port };
    
    public static NodeExecutionResult Fail(string error) 
        => new() { Success = false, Error = error };
}

/// <summary>
/// Node handler aray�z� - Strategy Pattern
/// Her node tipi i�in bir implementasyon
/// </summary>
public interface INodeHandler
{
    /// <summary>
    /// Bu handler'�n i�leyece�i node tipi
    /// </summary>
    string NodeType { get; }
    
    /// <summary>
    /// Node'u �al��t�r
    /// </summary>
    Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context);
}

