using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Orchestrator;

/// <summary>
/// Workflow düzenleme plan adımları
/// </summary>
public class WorkflowPlan
{
    public List<WorkflowPlanStep> Steps { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
}

public class WorkflowPlanStep
{
    public WorkflowEditAction Action { get; set; }
    public string Description { get; set; } = string.Empty;

    // AddNode
    public WorkflowNodeDto? Node { get; set; }

    // RemoveNode
    public Guid? NodeId { get; set; }

    // AddConnection
    public NodeConnectionDto? Connection { get; set; }

    // UpdateParameter
    public string? ParameterName { get; set; }
    public object? ParameterValue { get; set; }
}

public enum WorkflowEditAction
{
    AddNode,
    RemoveNode,
    AddConnection,
    RemoveConnection,
    UpdateParameter,
    Reorder
}
