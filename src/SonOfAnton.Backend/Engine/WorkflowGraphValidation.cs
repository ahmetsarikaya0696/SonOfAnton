using SonOfAnton.Shared.DTOs;
namespace SonOfAnton.Backend.Engine;
public static class WorkflowGraphValidation
{
    public static string? Validate(WorkflowGraphDto? graph, IEnumerable<INodeHandler> handlers)
    {
        if (graph is null) return null;
        if (graph.Nodes.Count > 200 || graph.Connections.Count > 400) return "Graph limit is 200 nodes and 400 edges.";
        if (graph.Nodes.Any(n => n.ChildGraph is not null)) return "Subflows are unavailable in the core release.";
        var ids = graph.Nodes.Select(n => n.Id).ToHashSet();
        if (ids.Contains(Guid.Empty) || ids.Count != graph.Nodes.Count) return "Node IDs must be unique and non-empty.";
        var supported = handlers.Select(h => h.NodeType).ToHashSet();
        if (graph.Nodes.Any(n => !supported.Contains(n.Type))) return "Unsupported node type.";
        if (graph.Connections.Any(c => !ids.Contains(c.SourceNodeId) || !ids.Contains(c.TargetNodeId))) return "Unknown node in connection.";
        var remaining = ids.ToHashSet();
        while (remaining.Count > 0)
        {
            var roots = remaining.Where(id => !graph.Connections.Any(c => c.TargetNodeId == id && remaining.Contains(c.SourceNodeId))).ToList();
            if (roots.Count == 0) return "Graph connections must be acyclic.";
            remaining.ExceptWith(roots);
        }
        return null;
    }
}
