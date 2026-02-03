namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// Debug node'u - veriyi loglar
/// </summary>
public class DebugHandler : INodeHandler
{
    public string NodeType => "debug";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var message = context.Node.Parameters.GetValueOrDefault("message")?.ToString() ?? "Debug";
        
        context.Log($"[DEBUG] {message}", Shared.Enums.LogLevel.Debug, context.InputData);
        
        return Task.FromResult(NodeExecutionResult.Ok(context.InputData));
    }
}

/// <summary>
/// Sonland�rma node'u - workflow'u bitirir
/// </summary>
public class EndHandler : INodeHandler
{
    public string NodeType => "end";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        context.Log("Workflow tamamland�", Shared.Enums.LogLevel.Info, context.InputData);
        
        return Task.FromResult(NodeExecutionResult.Ok(context.InputData));
    }
}

