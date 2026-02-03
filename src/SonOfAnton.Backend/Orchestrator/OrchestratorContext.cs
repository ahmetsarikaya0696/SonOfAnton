using SonOfAnton.Shared.Events;

namespace SonOfAnton.Backend.Orchestrator;

/// <summary>
/// Session bazl� orchestrator context
/// Her kullan�c� iste�i i�in bir context olu�turulur
/// </summary>
public class OrchestratorContext
{
    public string SessionId { get; } = Guid.NewGuid().ToString("N");
    
    public OrchestratorState CurrentState { get; set; } = OrchestratorState.Idle;
    
    public string? UserPrompt { get; set; }
    
    public Shared.Models.ProjectPlan? Plan { get; set; }
    
    public int CurrentTaskIndex { get; set; }
    
    public int RetryCount { get; set; }
    
    public const int MaxRetries = 3;
    
    /// <summary>
    /// Event'leri stream etmek i�in kullan�lacak channel
    /// </summary>
    public required Func<AgentEvent, CancellationToken, Task> EventEmitter { get; init; }
    
    public CancellationToken CancellationToken { get; set; }
    
    /// <summary>
    /// Log event'i g�nderir
    /// </summary>
    public Task LogAsync(string message, CancellationToken ct = default)
    {
        return EventEmitter(new AgentEvent
        {
            Type = EventType.Log,
            Payload = message
        }, ct == default ? CancellationToken : ct);
    }
    
    /// <summary>
    /// Error event'i g�nderir
    /// </summary>
    public Task ErrorAsync(string message, CancellationToken ct = default)
    {
        return EventEmitter(new AgentEvent
        {
            Type = EventType.Error,
            Payload = message
        }, ct == default ? CancellationToken : ct);
    }
}

