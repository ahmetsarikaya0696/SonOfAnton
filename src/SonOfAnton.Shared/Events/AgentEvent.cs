namespace SonOfAnton.Shared.Events;

/// <summary>
/// Backend'den Frontend'e stream edilen event yap�s�
/// </summary>
public record AgentEvent
{
    public required EventType Type { get; init; }
    
    /// <summary>
    /// JSON, Markdown veya Raw Text i�erik
    /// </summary>
    public required string Payload { get; init; }
    
    /// <summary>
    /// Event'in olu�turulma zaman�
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// �lgili task ID'si (varsa)
    /// </summary>
    public int? TaskId { get; init; }
}

