using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Orchestrator;

/// <summary>
/// WorkflowPlan adımlarını WorkflowGraphDto üzerinde uygular
/// </summary>
public class WorkflowPlanExecutor
{
    /// <summary>
    /// Plan adımlarını graph üzerine uygula
    /// </summary>
    public static WorkflowGraphDto Execute(WorkflowGraphDto graph, WorkflowPlan plan)
    {
        var result = new WorkflowGraphDto
        {
            Nodes = [.. graph.Nodes],
            Connections = [.. graph.Connections],
            Variables = [.. graph.Variables]
        };

        foreach (var step in plan.Steps)
        {
            switch (step.Action)
            {
                case WorkflowEditAction.AddNode when step.Node != null:
                    result.Nodes.Add(step.Node);
                    break;

                case WorkflowEditAction.RemoveNode when step.NodeId.HasValue:
                    result.Nodes.RemoveAll(n => n.Id == step.NodeId.Value);
                    result.Connections.RemoveAll(c =>
                        c.SourceNodeId == step.NodeId.Value || c.TargetNodeId == step.NodeId.Value);
                    break;

                case WorkflowEditAction.AddConnection when step.Connection != null:
                    result.Connections.Add(step.Connection);
                    break;

                case WorkflowEditAction.RemoveConnection when step.Connection != null:
                    result.Connections.RemoveAll(c =>
                        c.SourceNodeId == step.Connection.SourceNodeId &&
                        c.TargetNodeId == step.Connection.TargetNodeId);
                    break;

                case WorkflowEditAction.UpdateParameter when step.NodeId.HasValue && step.ParameterName != null:
                    var node = result.Nodes.FirstOrDefault(n => n.Id == step.NodeId.Value);
                    if (node != null)
                    {
                        node.Parameters[step.ParameterName] = step.ParameterValue;
                    }
                    break;
            }
        }

        return result;
    }
}
