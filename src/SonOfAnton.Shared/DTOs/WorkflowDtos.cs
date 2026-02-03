using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Shared.DTOs;

/// <summary>
/// Workflow DTO - API ileti�imi i�in
/// </summary>
public class WorkflowDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TriggerType TriggerType { get; set; }
    public WorkflowGraphDto? Graph { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Workflow graph verisi (nodes + connections)
/// </summary>
public class WorkflowGraphDto
{
    public List<WorkflowNodeDto> Nodes { get; set; } = [];
    public List<NodeConnectionDto> Connections { get; set; } = [];
}

/// <summary>
/// Node DTO
/// </summary>
public class WorkflowNodeDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public Dictionary<string, object?> Parameters { get; set; } = [];
}

/// <summary>
/// Connection DTO
/// </summary>
public class NodeConnectionDto
{
    public Guid Id { get; set; }
    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }
    public string SourcePort { get; set; } = "output";
    public string TargetPort { get; set; } = "input";
}

/// <summary>
/// Workflow olu�turma/g�ncelleme iste�i
/// </summary>
public class SaveWorkflowRequest
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TriggerType TriggerType { get; set; }
    public WorkflowGraphDto? Graph { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Execution ba�latma iste�i
/// </summary>
public class StartExecutionRequest
{
    public Guid WorkflowId { get; set; }
    public Dictionary<string, object?>? InitialData { get; set; }
}

/// <summary>
/// Execution DTO
/// </summary>
public class ExecutionDto
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public ExecutionStatus Status { get; set; }
    public Guid? CurrentNodeId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// SSE Log event'i
/// </summary>
public class LogEventDto
{
    public Guid ExecutionId { get; set; }
    public Guid? NodeId { get; set; }
    public string? NodeName { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Node execution sonucu
/// </summary>
public class NodeExecutionResultDto
{
    public Guid NodeId { get; set; }
    public string NodeType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public object? OutputData { get; set; }
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }
}

