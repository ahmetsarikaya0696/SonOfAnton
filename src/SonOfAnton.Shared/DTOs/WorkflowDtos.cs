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

    // Statistics
    public int RunCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double AvgDurationMs { get; set; }
    public DateTime? LastRunAt { get; set; }
    public double SuccessRate => RunCount > 0 ? (double)SuccessCount / RunCount * 100 : 0;

    // Folder
    public Guid? FolderId { get; set; }
    public string? FolderName { get; set; }
}

/// <summary>
/// Workflow graph verisi (nodes + connections)
/// </summary>
public class WorkflowGraphDto
{
    public List<WorkflowNodeDto> Nodes { get; set; } = [];
    public List<NodeConnectionDto> Connections { get; set; } = [];
    public List<WorkflowVariableDto> Variables { get; set; } = [];
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

    /// <summary>Nested subflow graph (loop/parallel)</summary>
    public WorkflowGraphDto? ChildGraph { get; set; }
    public SubflowType? SubflowType { get; set; }
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
    public Guid? FolderId { get; set; }
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

// ─── Workflow Variables ────────────────────────────────────────

public class WorkflowVariableDto
{
    public string Name { get; set; } = string.Empty;
    public VariableType Type { get; set; } = VariableType.String;
    public string? DefaultValue { get; set; }
    public VariableScope Scope { get; set; } = VariableScope.Execution;
    public string? Description { get; set; }
}

// ─── Folder DTOs ───────────────────────────────────────────────

public class FolderDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public int SortOrder { get; set; }
    public Guid? ParentFolderId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int WorkflowCount { get; set; }
}

public class FolderTreeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public int SortOrder { get; set; }
    public List<FolderTreeDto> Children { get; set; } = [];
    public List<WorkflowDto> Workflows { get; set; } = [];
}

public class CreateFolderRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public Guid? ParentFolderId { get; set; }
}

public class MoveWorkflowRequest
{
    public Guid? FolderId { get; set; }
}

// ─── Snapshot DTOs ─────────────────────────────────────────────

public class SnapshotDto
{
    public Guid Id { get; set; }
    public Guid ExecutionId { get; set; }
    public string? Label { get; set; }
    public Guid? AtNodeId { get; set; }
    public bool IsAutomatic { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateSnapshotRequest
{
    public string? Label { get; set; }
}

// ─── Stats DTOs ────────────────────────────────────────────────

public class WorkflowStatsDto
{
    public Guid WorkflowId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public int TotalRuns { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double SuccessRate { get; set; }
    public double AvgDurationMs { get; set; }
    public DateTime? LastRunAt { get; set; }
    public List<DailyStatsDto> DailyStats { get; set; } = [];
}

public class DailyStatsDto
{
    public DateOnly Date { get; set; }
    public int RunCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
}

public class DashboardStatsDto
{
    public int TotalWorkflows { get; set; }
    public int ActiveWorkflows { get; set; }
    public int TotalExecutions { get; set; }
    public int TodayExecutions { get; set; }
    public double OverallSuccessRate { get; set; }
    public List<WorkflowStatsDto> TopWorkflows { get; set; } = [];
}

// ─── Chat DTOs ─────────────────────────────────────────────────

public class ChatMessageDto
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public WorkflowGraphDto? Artifact { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class WorkflowChatRequest
{
    public string Message { get; set; } = string.Empty;
    public Guid? WorkflowId { get; set; }
    public WorkflowGraphDto? CurrentGraph { get; set; }
    public List<ChatMessageDto> History { get; set; } = [];
}

public class WorkflowChatResponse
{
    public string Message { get; set; } = string.Empty;
    public WorkflowGraphDto? SuggestedGraph { get; set; }
    public string? YamlPreview { get; set; }
    public List<WorkflowPlanStepDto> PlanSteps { get; set; } = [];
}

public class WorkflowPlanStepDto
{
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

// ─── Search DTOs ───────────────────────────────────────────────

public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public List<string> Types { get; set; } = [];
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class SearchResultDto<T>
{
    public List<SearchHitDto<T>> Hits { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class SearchHitDto<T>
{
    public T Item { get; set; } = default!;
    public double Score { get; set; }
    public string? Highlight { get; set; }
    public string ResultType { get; set; } = string.Empty;
}

// ─── A2A DTOs ──────────────────────────────────────────────────

public class AgentCardDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public List<string> Capabilities { get; set; } = [];
    public string? Endpoint { get; set; }
    public AgentSchemaDto? InputSchema { get; set; }
    public AgentSchemaDto? OutputSchema { get; set; }
}

public class AgentSchemaDto
{
    public string Type { get; set; } = "object";
    public Dictionary<string, object?> Properties { get; set; } = [];
}

public class A2ATaskDto
{
    public Guid Id { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public A2ATaskState State { get; set; }
    public object? Input { get; set; }
    public object? Output { get; set; }
    public List<A2AArtifactDto> Artifacts { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class A2AArtifactDto
{
    public string Name { get; set; } = string.Empty;
    public ArtifactType Type { get; set; }
    public string Content { get; set; } = string.Empty;
}

public class A2ATaskRequest
{
    public string AgentId { get; set; } = string.Empty;
    public object? Input { get; set; }
}

// ─── YAML / Artifact DTOs ──────────────────────────────────────

public class PlanArtifactDto
{
    public ArtifactType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

public class YamlImportRequest
{
    public string YamlContent { get; set; } = string.Empty;
}

public class YamlValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = [];
    public WorkflowGraphDto? ParsedGraph { get; set; }
}

// ─── Revision DTOs ─────────────────────────────────────────────

public class WorkflowRevisionDto
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public int Version { get; set; }
    public string? CommitMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public WorkflowGraphDto? Graph { get; set; }
}

