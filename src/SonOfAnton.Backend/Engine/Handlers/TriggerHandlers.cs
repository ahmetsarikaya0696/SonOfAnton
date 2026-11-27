namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// Manuel tetikleyici - workflow'u başlatır
/// </summary>
public class ManualTriggerHandler : INodeHandler
{
    public string NodeType => "trigger.manual";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        context.Log("Manuel tetikleyici başlatıldı", Shared.Enums.LogLevel.Info, null);
        
        return Task.FromResult(NodeExecutionResult.Ok(context.InputData ?? new
        {
            triggered = true,
            timestamp = DateTime.UtcNow,
            source = "manual"
        }));
    }
}

/// <summary>
/// Webhook tetikleyici - HTTP isteği ile tetiklenir
/// </summary>
public class WebhookTriggerHandler : INodeHandler
{
    public string NodeType => "trigger.webhook";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        context.Log("Webhook tetikleyici çalıştı", Shared.Enums.LogLevel.Info, null);
        
        // Webhook verisi InputData'dan gelir
        return Task.FromResult(NodeExecutionResult.Ok(context.InputData ?? new
        {
            triggered = true,
            timestamp = DateTime.UtcNow,
            source = "webhook"
        }));
    }
}

/// <summary>
/// Zamanlı tetikleyici - Hangfire cron job ile tetiklenir
/// </summary>
public class ScheduleTriggerHandler : INodeHandler
{
    public string NodeType => "trigger.schedule";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var cron = context.Node.Parameters.GetValueOrDefault("cron")?.ToString();
        
        context.Log($"Zamanlı tetikleyici çalıştı (cron: {cron})", Shared.Enums.LogLevel.Info, null);
        
        return Task.FromResult(NodeExecutionResult.Ok(context.InputData ?? new
        {
            triggered = true,
            timestamp = DateTime.UtcNow,
            source = "schedule",
            cron
        }));
    }
}

