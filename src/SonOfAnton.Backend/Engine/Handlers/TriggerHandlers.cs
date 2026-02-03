namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// Manuel tetikleyici - workflow'u ba�lat�r
/// </summary>
public class ManualTriggerHandler : INodeHandler
{
    public string NodeType => "trigger.manual";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        context.Log("Manuel tetikleyici ba�lat�ld�", Shared.Enums.LogLevel.Info, null);
        
        return Task.FromResult(NodeExecutionResult.Ok(new
        {
            triggered = true,
            timestamp = DateTime.UtcNow,
            source = "manual"
        }));
    }
}

/// <summary>
/// Webhook tetikleyici - HTTP iste�i ile tetiklenir
/// </summary>
public class WebhookTriggerHandler : INodeHandler
{
    public string NodeType => "trigger.webhook";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        context.Log("Webhook tetikleyici �al��t�", Shared.Enums.LogLevel.Info, null);
        
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
/// Zamanl� tetikleyici - Hangfire cron job ile tetiklenir
/// </summary>
public class ScheduleTriggerHandler : INodeHandler
{
    public string NodeType => "trigger.schedule";

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var cron = context.Node.Parameters.GetValueOrDefault("cron")?.ToString();
        
        context.Log($"Zamanl� tetikleyici �al��t� (cron: {cron})", Shared.Enums.LogLevel.Info, null);
        
        return Task.FromResult(NodeExecutionResult.Ok(new
        {
            triggered = true,
            timestamp = DateTime.UtcNow,
            source = "schedule",
            cron
        }));
    }
}

