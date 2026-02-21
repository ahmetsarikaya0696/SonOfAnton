using SonOfAnton.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SonOfAnton.Shared.Core;

public class WorkflowDiffResult
{
    public List<NodeDiff> Nodes { get; set; } = [];
    public List<ConnectionDiff> Connections { get; set; } = [];
    public bool HasChanges => Nodes.Any() || Connections.Any();
}

public enum DiffType { Added, Removed, Modified }

public class NodeDiff
{
    public Guid NodeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DiffType Type { get; set; }
    public List<string> ChangedProperties { get; set; } = [];
}

public class ConnectionDiff
{
    public Guid ConnectionId { get; set; }
    public DiffType Type { get; set; }
    public string Description { get; set; } = string.Empty;
}

public static class WorkflowDiffService
{
    /// <summary>
    /// İki workflow JSON/Graph durumu arasındaki farkları çıkarır
    /// </summary>
    public static WorkflowDiffResult Compare(WorkflowGraphDto oldGraph, WorkflowGraphDto newGraph)
    {
        var result = new WorkflowDiffResult();

        if (oldGraph == null || newGraph == null)
            return result;

        // Nodes
        var oldNodes = oldGraph.Nodes?.ToDictionary(n => n.Id) ?? new Dictionary<Guid, WorkflowNodeDto>();
        var newNodes = newGraph.Nodes?.ToDictionary(n => n.Id) ?? new Dictionary<Guid, WorkflowNodeDto>();

        foreach (var newNode in newNodes.Values)
        {
            if (!oldNodes.TryGetValue(newNode.Id, out var oldNode))
            {
                result.Nodes.Add(new NodeDiff { NodeId = newNode.Id, Name = newNode.Name, Type = DiffType.Added });
            }
            else
            {
                var changes = new List<string>();
                if (newNode.Name != oldNode.Name) changes.Add("Name");
                if (newNode.Type != oldNode.Type) changes.Add("Type");

                // Note: X, Y coords intentionally ignored for diff simplicity unless needed
                
                // Compare Parameters safely
                if (newNode.Parameters != null && oldNode.Parameters != null)
                {
                    if (newNode.Parameters.Count != oldNode.Parameters.Count ||
                        newNode.Parameters.Any(p => !oldNode.Parameters.ContainsKey(p.Key) || 
                                                    p.Value?.ToString() != oldNode.Parameters[p.Key]?.ToString()))
                    {
                        changes.Add("Parameters");
                    }
                }

                if (changes.Any())
                {
                    result.Nodes.Add(new NodeDiff { NodeId = newNode.Id, Name = newNode.Name, Type = DiffType.Modified, ChangedProperties = changes });
                }
            }
        }

        foreach (var oldNode in oldNodes.Values)
        {
            if (!newNodes.ContainsKey(oldNode.Id))
            {
                result.Nodes.Add(new NodeDiff { NodeId = oldNode.Id, Name = oldNode.Name, Type = DiffType.Removed });
            }
        }

        // Connections
        var oldConns = oldGraph.Connections?.ToDictionary(c => c.Id) ?? new Dictionary<Guid, NodeConnectionDto>();
        var newConns = newGraph.Connections?.ToDictionary(c => c.Id) ?? new Dictionary<Guid, NodeConnectionDto>();

        foreach (var newConn in newConns.Values)
        {
            if (!oldConns.ContainsKey(newConn.Id))
            {
                var sourceNode = newNodes.TryGetValue(newConn.SourceNodeId, out var s) ? s.Name : "Unknown";
                var targetNode = newNodes.TryGetValue(newConn.TargetNodeId, out var t) ? t.Name : "Unknown";
                result.Connections.Add(new ConnectionDiff 
                { 
                    ConnectionId = newConn.Id, 
                    Type = DiffType.Added, 
                    Description = $"{sourceNode} -> {targetNode}" 
                });
            }
        }

        foreach (var oldConn in oldConns.Values)
        {
            if (!newConns.ContainsKey(oldConn.Id))
            {
                var sourceNode = oldNodes.TryGetValue(oldConn.SourceNodeId, out var s) ? s.Name : "Unknown";
                var targetNode = oldNodes.TryGetValue(oldConn.TargetNodeId, out var t) ? t.Name : "Unknown";
                result.Connections.Add(new ConnectionDiff 
                { 
                    ConnectionId = oldConn.Id, 
                    Type = DiffType.Removed, 
                    Description = $"{sourceNode} -> {targetNode}" 
                });
            }
        }

        return result;
    }
}
