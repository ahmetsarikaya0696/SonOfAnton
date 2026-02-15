using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Frontend.Services;

/// <summary>
/// Chat'ten gelen workflow önerisini DiagramStateService'e uygulayan köprü servis
/// </summary>
public class ChatDiagramBridge
{
    private readonly DiagramStateService _diagramState;

    public ChatDiagramBridge(DiagramStateService diagramState)
    {
        _diagramState = diagramState;
    }

    /// <summary>
    /// AI'dan gelen graph'ı diagram'a uygula
    /// </summary>
    public void ApplySuggestedGraph(WorkflowGraphDto graph)
    {
        var workflow = new WorkflowDto
        {
            Id = _diagramState.CurrentWorkflow?.Id ?? Guid.Empty,
            Name = _diagramState.CurrentWorkflow?.Name ?? "AI Workflow",
            Graph = graph
        };

        _diagramState.LoadWorkflow(workflow);
    }

    /// <summary>
    /// Mevcut graph ile önerilen graph arasındaki farkları hesapla
    /// </summary>
    public WorkflowDiffResult ComputeDiff(WorkflowGraphDto? before, WorkflowGraphDto? after)
    {
        var result = new WorkflowDiffResult();
        if (after == null) return result;

        var beforeIds = before?.Nodes.Select(n => n.Id).ToHashSet() ?? [];
        var afterIds = after.Nodes.Select(n => n.Id).ToHashSet();

        result.AddedNodes = after.Nodes.Where(n => !beforeIds.Contains(n.Id)).ToList();
        result.RemovedNodeIds = beforeIds.Except(afterIds).ToList();
        result.TotalAfterNodes = after.Nodes.Count;
        result.TotalAfterConnections = after.Connections.Count;

        return result;
    }
}

public class WorkflowDiffResult
{
    public List<WorkflowNodeDto> AddedNodes { get; set; } = [];
    public List<Guid> RemovedNodeIds { get; set; } = [];
    public int TotalAfterNodes { get; set; }
    public int TotalAfterConnections { get; set; }
}
