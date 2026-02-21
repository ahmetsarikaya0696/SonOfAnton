using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// Trigger Node for Global Error Workflows
/// Bu node, başka bir workflow hata aldığında ve global error handler olarak bu workflow atandığında tetiklenir.
/// </summary>
public class ErrorTriggerHandler : INodeHandler
{
    public string NodeType => "trigger.error";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        context.Log("Global Hata Tetikleyicisi çalıştırıldı", Shared.Enums.LogLevel.Info, context.InputData);
        
        // context.InputData içinde hatayı fırlatan workflow'un hata detayları yer alır.
        return Task.FromResult(NodeExecutionResult.Ok(context.InputData));
    }
}
