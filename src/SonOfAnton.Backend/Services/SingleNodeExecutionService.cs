using System.Text.Json;
using SonOfAnton.Backend.Engine;
using SonOfAnton.Backend.Engine.Expressions;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Services;

public interface ISingleNodeExecutionService
{
    Task<ExecuteSingleNodeResponse> ExecuteNodeAsync(ExecuteSingleNodeRequest request, CancellationToken cancellationToken = default);
}

public class SingleNodeExecutionService : ISingleNodeExecutionService
{
    private readonly Dictionary<string, INodeHandler> _handlers;
    private readonly ILogger<SingleNodeExecutionService> _logger;

    public SingleNodeExecutionService(IEnumerable<INodeHandler> handlers, ILogger<SingleNodeExecutionService> logger)
    {
        _handlers = handlers.ToDictionary(h => h.NodeType, h => h);
        _logger = logger;
    }

    public async Task<ExecuteSingleNodeResponse> ExecuteNodeAsync(ExecuteSingleNodeRequest request, CancellationToken cancellationToken = default)
    {
        var node = request.Node;
        if (!_handlers.TryGetValue(node.Type, out var handler))
        {
            return new ExecuteSingleNodeResponse
            {
                Success = false,
                Error = $"Handler not found for type {node.Type}"
            };
        }

        var executionId = Guid.NewGuid();
        var logs = new List<string>();
        var memory = request.Memory ?? new Dictionary<string, object?>();

        var variableStore = new VariableStore();
        foreach (var (k, v) in memory)
            variableStore.Set(k, v);

        var resolvedParams = ExpressionEvaluator.ResolveParameters(node.Parameters, variableStore);
        var resolvedNode = new WorkflowNodeDto
        {
            Id = node.Id,
            Type = node.Type,
            Name = node.Name,
            Icon = node.Icon,
            Color = node.Color,
            X = node.X,
            Y = node.Y,
            Parameters = resolvedParams,
            ChildGraph = node.ChildGraph,
            SubflowType = node.SubflowType
        };

        var context = new NodeExecutionContext
        {
            ExecutionId = executionId,
            Node = resolvedNode,
            InputData = request.InputData,
            Memory = memory,
            CancellationToken = cancellationToken,
            Log = (msg, level, data) =>
            {
                logs.Add($"[{level}] {msg} {(data != null ? JsonSerializer.Serialize(data) : string.Empty)}");
            }
        };

        try
        {
            var result = await handler.ExecuteAsync(context);
            
            if (result.Success)
            {
                memory[$"node_{node.Id}"] = result.OutputData;
            }
            
            return new ExecuteSingleNodeResponse
            {
                Success = result.Success,
                OutputData = result.OutputData,
                Error = result.Error,
                OutputPort = result.OutputPort,
                Memory = memory,
                Logs = logs
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing single node {NodeId}", node.Id);
            return new ExecuteSingleNodeResponse
            {
                Success = false,
                Error = ex.Message,
                Memory = memory,
                Logs = logs
            };
        }
    }
}
