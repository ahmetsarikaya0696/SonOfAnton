namespace SonOfAnton.Shared.Core.GraphEngine;

/// <summary>
/// Graph execution sonucu
/// </summary>
public class GraphExecutionResult
{
    public bool Success { get; init; }
    public TimeSpan Duration { get; init; }
    public int NodesExecuted { get; init; }
    public string? Error { get; init; }
    public Dictionary<Guid, object?> NodeOutputs { get; init; } = new();
    public List<NodeExecutionRecord> ExecutionRecords { get; init; } = [];
}

/// <summary>
/// Tek bir node'un execution kaydı
/// </summary>
public class NodeExecutionRecord
{
    public Guid NodeId { get; init; }
    public string NodeName { get; init; } = string.Empty;
    public string NodeType { get; init; } = string.Empty;
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public TimeSpan Duration => EndTime - StartTime;
    public bool Success { get; init; }
    public string? Error { get; init; }
    public object? InputData { get; init; }
    public object? OutputData { get; init; }
}

/// <summary>
/// Node execution context
/// </summary>
public class ExecutionContext
{
    /// <summary>
    /// Execution ID
    /// </summary>
    public Guid ExecutionId { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// Graph referansı
    /// </summary>
    public required Graph Graph { get; init; }
    
    /// <summary>
    /// Shared memory (node'lar arası veri paylaşımı)
    /// </summary>
    public Dictionary<string, object?> Memory { get; init; } = new();
    
    /// <summary>
    /// Cancellation token
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
    
    /// <summary>
    /// Event channel (SSE için)
    /// </summary>
    public DataChannel<WorkflowEvent>? EventChannel { get; init; }
    
    /// <summary>
    /// Logging delegate
    /// </summary>
    public Action<string, string, object?>? Log { get; init; }
}

/// <summary>
/// Node executor delegate
/// </summary>
/// <param name="node">Çalıştırılacak node</param>
/// <param name="inputData">Önceki node'dan gelen veri</param>
/// <param name="context">Execution context</param>
/// <returns>Node çıktısı</returns>
public delegate Task<(bool Success, object? Output, string? Error)> NodeExecutorDelegate(
    INode node,
    object? inputData,
    ExecutionContext context);

/// <summary>
/// Graph executor - DAG'ı topological order'da çalıştırır
/// </summary>
public class GraphExecutor
{
    private readonly NodeExecutorDelegate _nodeExecutor;
    
    public GraphExecutor(NodeExecutorDelegate nodeExecutor)
    {
        _nodeExecutor = nodeExecutor;
    }
    
    /// <summary>
    /// Graph'ı çalıştırır
    /// </summary>
    public async Task<GraphExecutionResult> ExecuteAsync(ExecutionContext context)
    {
        var startTime = DateTime.UtcNow;
        var records = new List<NodeExecutionRecord>();
        var nodeOutputs = new Dictionary<Guid, object?>();
        
        try
        {
            // Validation
            var validation = context.Graph.Validate();
            if (!validation.IsValid)
            {
                return new GraphExecutionResult
                {
                    Success = false,
                    Error = string.Join("; ", validation.Errors),
                    Duration = DateTime.UtcNow - startTime
                };
            }
            
            // Emit start event
            await EmitEventAsync(context, new LogEmittedEvent
            {
                ExecutionId = context.ExecutionId,
                Level = "info",
                Message = $"Starting workflow execution: {context.Graph.Name}"
            });
            
            // Node'ları sıfırla
            context.Graph.ResetAllNodes();
            
            // Topological order al
            var executionOrder = context.Graph.GetTopologicalOrder();
            
            foreach (var node in executionOrder)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                
                // Input değerlerini hazırla
                var inputData = PrepareInputData(context.Graph, node, nodeOutputs);
                
                // Node çalıştır
                var record = await ExecuteNodeAsync(node, inputData, context);
                records.Add(record);
                
                if (!record.Success)
                {
                    return new GraphExecutionResult
                    {
                        Success = false,
                        Error = $"Node '{node.Name}' failed: {record.Error}",
                        Duration = DateTime.UtcNow - startTime,
                        NodesExecuted = records.Count,
                        ExecutionRecords = records,
                        NodeOutputs = nodeOutputs
                    };
                }
                
                // Output'u kaydet
                nodeOutputs[node.Id] = record.OutputData;
            }
            
            // Success event
            await EmitEventAsync(context, new WorkflowCompletedEvent
            {
                ExecutionId = context.ExecutionId,
                Success = true,
                TotalDuration = DateTime.UtcNow - startTime,
                NodesExecuted = records.Count
            });
            
            return new GraphExecutionResult
            {
                Success = true,
                Duration = DateTime.UtcNow - startTime,
                NodesExecuted = records.Count,
                ExecutionRecords = records,
                NodeOutputs = nodeOutputs
            };
        }
        catch (OperationCanceledException)
        {
            await EmitEventAsync(context, new WorkflowCompletedEvent
            {
                ExecutionId = context.ExecutionId,
                Success = false,
                Error = "Execution cancelled",
                TotalDuration = DateTime.UtcNow - startTime,
                NodesExecuted = records.Count
            });
            
            return new GraphExecutionResult
            {
                Success = false,
                Error = "Execution cancelled",
                Duration = DateTime.UtcNow - startTime,
                NodesExecuted = records.Count,
                ExecutionRecords = records,
                NodeOutputs = nodeOutputs
            };
        }
        catch (Exception ex)
        {
            await EmitEventAsync(context, new WorkflowCompletedEvent
            {
                ExecutionId = context.ExecutionId,
                Success = false,
                Error = ex.Message,
                TotalDuration = DateTime.UtcNow - startTime,
                NodesExecuted = records.Count
            });
            
            return new GraphExecutionResult
            {
                Success = false,
                Error = ex.Message,
                Duration = DateTime.UtcNow - startTime,
                NodesExecuted = records.Count,
                ExecutionRecords = records,
                NodeOutputs = nodeOutputs
            };
        }
    }
    
    /// <summary>
    /// Sadece belirli bir node'dan başlayarak çalıştırır
    /// </summary>
    public async Task<GraphExecutionResult> ExecuteFromNodeAsync(
        ExecutionContext context,
        Guid startNodeId,
        object? initialInput = null)
    {
        var startTime = DateTime.UtcNow;
        var records = new List<NodeExecutionRecord>();
        var nodeOutputs = new Dictionary<Guid, object?>();
        
        try
        {
            var startNode = context.Graph.GetNode(startNodeId);
            if (startNode == null)
            {
                return new GraphExecutionResult
                {
                    Success = false,
                    Error = $"Start node {startNodeId} not found",
                    Duration = DateTime.UtcNow - startTime
                };
            }
            
            // Start node ve successors'ı al
            var nodesToExecute = new List<INode> { startNode };
            nodesToExecute.AddRange(context.Graph.GetSuccessors(startNodeId));
            
            // İlk node için input set et
            if (initialInput != null)
            {
                nodeOutputs[Guid.Empty] = initialInput; // Dummy parent
            }
            
            foreach (var node in nodesToExecute)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                
                var inputData = node.Id == startNodeId
                    ? initialInput
                    : PrepareInputData(context.Graph, node, nodeOutputs);
                
                var record = await ExecuteNodeAsync(node, inputData, context);
                records.Add(record);
                
                if (!record.Success)
                {
                    return new GraphExecutionResult
                    {
                        Success = false,
                        Error = $"Node '{node.Name}' failed: {record.Error}",
                        Duration = DateTime.UtcNow - startTime,
                        NodesExecuted = records.Count,
                        ExecutionRecords = records,
                        NodeOutputs = nodeOutputs
                    };
                }
                
                nodeOutputs[node.Id] = record.OutputData;
            }
            
            return new GraphExecutionResult
            {
                Success = true,
                Duration = DateTime.UtcNow - startTime,
                NodesExecuted = records.Count,
                ExecutionRecords = records,
                NodeOutputs = nodeOutputs
            };
        }
        catch (Exception ex)
        {
            return new GraphExecutionResult
            {
                Success = false,
                Error = ex.Message,
                Duration = DateTime.UtcNow - startTime,
                NodesExecuted = records.Count,
                ExecutionRecords = records,
                NodeOutputs = nodeOutputs
            };
        }
    }
    
    private async Task<NodeExecutionRecord> ExecuteNodeAsync(
        INode node,
        object? inputData,
        ExecutionContext context)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            node.State = ExecutionState.Running;
            
            // Emit start event
            await EmitEventAsync(context, new NodeExecutionStartedEvent
            {
                ExecutionId = context.ExecutionId,
                NodeId = node.Id,
                NodeName = node.Name,
                NodeType = node.Type
            });
            
            // Execute
            var (success, output, error) = await _nodeExecutor(node, inputData, context);
            
            var endTime = DateTime.UtcNow;
            node.State = success ? ExecutionState.Completed : ExecutionState.Failed;
            node.LastError = error;
            
            // Emit complete event
            await EmitEventAsync(context, new NodeExecutionCompletedEvent
            {
                ExecutionId = context.ExecutionId,
                NodeId = node.Id,
                Success = success,
                Error = error,
                Duration = endTime - startTime,
                OutputPreview = output?.ToString()?.Length > 200 
                    ? output.ToString()?[..200] + "..." 
                    : output
            });
            
            return new NodeExecutionRecord
            {
                NodeId = node.Id,
                NodeName = node.Name,
                NodeType = node.Type,
                StartTime = startTime,
                EndTime = endTime,
                Success = success,
                Error = error,
                InputData = inputData,
                OutputData = output
            };
        }
        catch (Exception ex)
        {
            node.State = ExecutionState.Failed;
            node.LastError = ex.Message;
            
            return new NodeExecutionRecord
            {
                NodeId = node.Id,
                NodeName = node.Name,
                NodeType = node.Type,
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                Success = false,
                Error = ex.Message,
                InputData = inputData
            };
        }
    }
    
    private static object? PrepareInputData(
        Graph graph,
        INode node,
        Dictionary<Guid, object?> nodeOutputs)
    {
        var parents = graph.GetParents(node.Id).ToList();
        
        if (parents.Count == 0)
            return null;
        
        if (parents.Count == 1)
        {
            var (parentNode, _) = parents[0];
            return nodeOutputs.TryGetValue(parentNode.Id, out var output) ? output : null;
        }
        
        // Multiple parents - dictionary olarak döndür
        var inputs = new Dictionary<string, object?>();
        foreach (var (parentNode, edge) in parents)
        {
            if (nodeOutputs.TryGetValue(parentNode.Id, out var output))
            {
                inputs[edge.SourcePinName] = output;
            }
        }
        
        return inputs;
    }
    
    private static async ValueTask EmitEventAsync(ExecutionContext context, WorkflowEvent evt)
    {
        if (context.EventChannel != null)
        {
            await context.EventChannel.WriteAsync(evt, context.CancellationToken);
        }
        
        context.Log?.Invoke(evt switch
        {
            LogEmittedEvent log => log.Level,
            NodeExecutionStartedEvent => "info",
            NodeExecutionCompletedEvent completed => completed.Success ? "info" : "error",
            WorkflowCompletedEvent completed => completed.Success ? "info" : "error",
            _ => "debug"
        }, evt switch
        {
            LogEmittedEvent log => log.Message,
            NodeExecutionStartedEvent start => $"Starting node: {start.NodeName}",
            NodeExecutionCompletedEvent completed => completed.Success 
                ? $"Completed node: {completed.NodeId}" 
                : $"Failed node: {completed.NodeId} - {completed.Error}",
            WorkflowCompletedEvent completed => completed.Success
                ? $"Workflow completed: {completed.NodesExecuted} nodes in {completed.TotalDuration}"
                : $"Workflow failed: {completed.Error}",
            _ => evt.ToString()
        }, evt);
    }
}
