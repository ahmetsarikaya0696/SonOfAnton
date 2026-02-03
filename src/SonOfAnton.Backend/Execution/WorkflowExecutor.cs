using SonOfAnton.Shared.Models;

namespace SonOfAnton.Backend.Execution;

/// <summary>
/// Workflow �al��t�rma motoru
/// </summary>
public class WorkflowExecutor
{
    private readonly ILogger<WorkflowExecutor> _logger;
    private readonly IServiceProvider _serviceProvider;

    public WorkflowExecutor(ILogger<WorkflowExecutor> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<WorkflowExecutionResult> ExecuteAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        var result = new WorkflowExecutionResult
        {
            WorkflowId = workflow.Id,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Find trigger nodes (nodes with no inputs)
            var triggerNodes = workflow.Nodes
                .Where(n => !workflow.Connections.Any(c => c.TargetNodeId == n.Id))
                .ToList();

            if (triggerNodes.Count == 0)
            {
                throw new InvalidOperationException("Workflow has no trigger nodes");
            }

            // Execute from trigger nodes
            foreach (var triggerNode in triggerNodes)
            {
                await ExecuteNodeAsync(workflow, triggerNode, null, result, cancellationToken);
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow execution failed: {WorkflowId}", workflow.Id);
            result.Success = false;
            result.Error = ex.Message;
        }
        finally
        {
            result.CompletedAt = DateTime.UtcNow;
        }

        return result;
    }

    private async Task ExecuteNodeAsync(
        Workflow workflow,
        WorkflowNode node,
        object? inputData,
        WorkflowExecutionResult result,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing node: {NodeName} ({NodeType})", node.Name, node.Type);

        var nodeResult = new NodeExecutionResult
        {
            NodeId = node.Id,
            NodeType = node.Type,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            node.Status = NodeStatus.Running;
            node.LastExecutedAt = DateTime.UtcNow;

            // Execute node based on type
            var outputData = await ExecuteNodeTypeAsync(node, inputData, cancellationToken);

            node.Status = NodeStatus.Success;
            node.LastError = null;
            nodeResult.Success = true;
            nodeResult.OutputData = outputData;

            // Find and execute connected nodes
            var connections = workflow.Connections
                .Where(c => c.SourceNodeId == node.Id)
                .ToList();

            foreach (var connection in connections)
            {
                var nextNode = workflow.Nodes.FirstOrDefault(n => n.Id == connection.TargetNodeId);
                if (nextNode != null)
                {
                    await ExecuteNodeAsync(workflow, nextNode, outputData, result, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            node.Status = NodeStatus.Error;
            node.LastError = ex.Message;
            nodeResult.Success = false;
            nodeResult.Error = ex.Message;
            throw;
        }
        finally
        {
            nodeResult.CompletedAt = DateTime.UtcNow;
            result.NodeResults.Add(nodeResult);
        }
    }

    private async Task<object?> ExecuteNodeTypeAsync(WorkflowNode node, object? inputData, CancellationToken cancellationToken)
    {
        return node.Type switch
        {
            "trigger.manual" => await ExecuteManualTrigger(node),
            "trigger.webhook" => await ExecuteWebhookTrigger(node, inputData),
            "http.request" => await ExecuteHttpRequest(node, inputData, cancellationToken),
            "transform.set" => ExecuteSet(node, inputData),
            "transform.code" => await ExecuteCode(node, inputData),
            "transform.filter" => ExecuteFilter(node, inputData),
            "logic.if" => ExecuteIf(node, inputData),
            "ai.openai" => await ExecuteOpenAI(node, inputData, cancellationToken),
            _ => inputData // Pass through for unimplemented nodes
        };
    }

    private Task<object?> ExecuteManualTrigger(WorkflowNode node)
    {
        return Task.FromResult<object?>(new { triggered = true, timestamp = DateTime.UtcNow });
    }

    private Task<object?> ExecuteWebhookTrigger(WorkflowNode node, object? inputData)
    {
        return Task.FromResult(inputData);
    }

    private async Task<object?> ExecuteHttpRequest(WorkflowNode node, object? inputData, CancellationToken cancellationToken)
    {
        var url = node.Parameters.GetValueOrDefault("url")?.ToString();
        var method = node.Parameters.GetValueOrDefault("method")?.ToString() ?? "GET";

        if (string.IsNullOrEmpty(url))
            throw new InvalidOperationException("HTTP Request node requires a URL");

        using var client = new HttpClient();
        var request = new HttpRequestMessage(new HttpMethod(method), url);

        var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        return new
        {
            statusCode = (int)response.StatusCode,
            body = content,
            headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
        };
    }

    private object? ExecuteSet(WorkflowNode node, object? inputData)
    {
        var values = node.Parameters.GetValueOrDefault("values");
        return values ?? inputData;
    }

    private Task<object?> ExecuteCode(WorkflowNode node, object? inputData)
    {
        // In a real implementation, this would execute JavaScript/C# code
        // For now, just pass through
        return Task.FromResult(inputData);
    }

    private object? ExecuteFilter(WorkflowNode node, object? inputData)
    {
        // In a real implementation, this would filter based on condition
        return inputData;
    }

    private object? ExecuteIf(WorkflowNode node, object? inputData)
    {
        // In a real implementation, this would evaluate condition
        return inputData;
    }

    private async Task<object?> ExecuteOpenAI(WorkflowNode node, object? inputData, CancellationToken cancellationToken)
    {
        // In a real implementation, this would call OpenAI API
        await Task.Delay(100, cancellationToken);
        return new { response = "AI response placeholder", inputData };
    }
}

public class WorkflowExecutionResult
{
    public Guid WorkflowId { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<NodeExecutionResult> NodeResults { get; set; } = [];
}

public class NodeExecutionResult
{
    public Guid NodeId { get; set; }
    public string NodeType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public object? OutputData { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

