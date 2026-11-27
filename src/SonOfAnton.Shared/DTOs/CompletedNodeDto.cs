namespace SonOfAnton.Shared.DTOs;

/// <summary>Durable node outcome used to reconstruct a late execution-monitor subscription.</summary>
public sealed class CompletedNodeDto
{
    public Guid NodeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public double DurationMs { get; set; }
    public DateTime Timestamp { get; set; }
}
