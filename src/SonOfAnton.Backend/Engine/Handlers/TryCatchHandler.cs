using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// Try/Catch kontrol node'u.
/// Sub-graph içindeki hataları yakalayıp 'error' bacağına yönlendirir.
/// </summary>
public class TryCatchHandler : INodeHandler
{
    public string NodeType => "flow.trycatch";
    
    private readonly IServiceProvider _serviceProvider;

    public TryCatchHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        context.Log("Try/Catch bloğu değerlendiriliyor...", Shared.Enums.LogLevel.Info, null);
        
        // Bu node'un ChildGraph özelliği WorkflowRunner seviyesinde desteklendiğinde
        // alt akış çalıştırılıp sonucu değerlendirilebilir. Şimdilik akış kontrolü ve 
        // node-level error mekanizmaları ile birlikte logic olarak kullanılır.
        
        return Task.FromResult(NodeExecutionResult.Ok(context.InputData, "output"));
    }
}
