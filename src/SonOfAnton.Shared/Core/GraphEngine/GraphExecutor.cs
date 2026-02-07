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
public record NodeExecutionRecord
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
    public int RetryCount { get; init; }
}

/// <summary>
/// Retry policy konfigürasyonu
/// </summary>
public class RetryPolicy
{
    /// <summary>Maksimum deneme sayısı (ilk deneme dahil)</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>İlk bekleme süresi</summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Exponential backoff çarpanı</summary>
    public double BackoffMultiplier { get; init; } = 2.0;

    /// <summary>Maksimum bekleme süresi</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Retry yapılmayacak exception tipleri</summary>
    public HashSet<Type> NonRetryableExceptions { get; init; } = [typeof(ArgumentException), typeof(InvalidOperationException)];

    public static RetryPolicy Default => new();
    public static RetryPolicy None => new() { MaxRetries = 0 };
}

/// <summary>
/// Circuit breaker durumu
/// </summary>
public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}

/// <summary>
/// Circuit breaker pattern implementasyonu
/// </summary>
public class CircuitBreaker
{
    private CircuitState _state = CircuitState.Closed;
    private int _failureCount;
    private DateTime _lastFailureTime = DateTime.MinValue;
    private readonly object _lock = new();

    /// <summary>Circuit açılmadan önceki maksimum hata sayısı</summary>
    public int FailureThreshold { get; init; } = 5;

    /// <summary>Circuit açık kaldıktan sonra half-open'a geçiş süresi</summary>
    public TimeSpan RecoveryTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Mevcut circuit durumu</summary>
    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                if (_state == CircuitState.Open &&
                    DateTime.UtcNow - _lastFailureTime >= RecoveryTimeout)
                {
                    _state = CircuitState.HalfOpen;
                }
                return _state;
            }
        }
    }

    /// <summary>İşlem yapılabilir mi kontrol eder</summary>
    public bool AllowRequest()
    {
        var currentState = State;
        return currentState != CircuitState.Open;
    }

    /// <summary>Başarılı bir işlem sonrası circuit'ı günceller</summary>
    public void RecordSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = CircuitState.Closed;
        }
    }

    /// <summary>Başarısız bir işlem sonrası circuit'ı günceller</summary>
    public void RecordFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;
            if (_failureCount >= FailureThreshold)
            {
                _state = CircuitState.Open;
            }
        }
    }

    /// <summary>Circuit'ı sıfırlar</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = CircuitState.Closed;
        }
    }
}

/// <summary>
/// Execution checkpoint (hata sonrası kaldığı yerden devam için)
/// </summary>
public class ExecutionCheckpoint
{
    /// <summary>Checkpoint ID</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Oluşturulma zamanı</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Tamamlanmış node'ların ID'leri</summary>
    public HashSet<Guid> CompletedNodeIds { get; init; } = [];

    /// <summary>Tamamlanmış node'ların çıktıları</summary>
    public Dictionary<Guid, object?> NodeOutputs { get; init; } = new();

    /// <summary>Execution kayıtları</summary>
    public List<NodeExecutionRecord> Records { get; init; } = [];
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

    /// <summary>
    /// Retry policy
    /// </summary>
    public RetryPolicy RetryPolicy { get; init; } = RetryPolicy.Default;

    /// <summary>
    /// Parallel execution etkin mi
    /// </summary>
    public bool EnableParallelExecution { get; init; }

    /// <summary>
    /// Son checkpoint (resume desteği için)
    /// </summary>
    public ExecutionCheckpoint? Checkpoint { get; set; }
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
    private readonly CircuitBreaker _circuitBreaker = new();
    
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
        var completedNodeIds = new HashSet<Guid>();

        // Checkpoint'ten devam et
        if (context.Checkpoint is { } checkpoint)
        {
            foreach (var kvp in checkpoint.NodeOutputs)
                nodeOutputs[kvp.Key] = kvp.Value;
            foreach (var id in checkpoint.CompletedNodeIds)
                completedNodeIds.Add(id);
            records.AddRange(checkpoint.Records);
        }
        
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
            
            // Node'ları sıfırla (checkpoint'ten devam edenler hariç)
            context.Graph.ResetAllNodes();

            if (context.EnableParallelExecution)
            {
                await ExecuteParallelAsync(context, records, nodeOutputs, completedNodeIds);
            }
            else
            {
                await ExecuteSequentialAsync(context, records, nodeOutputs, completedNodeIds);
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
            // Checkpoint oluştur
            context.Checkpoint = CreateCheckpoint(completedNodeIds, nodeOutputs, records);

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
            // Checkpoint oluştur
            context.Checkpoint = CreateCheckpoint(completedNodeIds, nodeOutputs, records);

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

    private async Task ExecuteSequentialAsync(
        ExecutionContext context,
        List<NodeExecutionRecord> records,
        Dictionary<Guid, object?> nodeOutputs,
        HashSet<Guid> completedNodeIds)
    {
        var executionOrder = context.Graph.GetTopologicalOrder();

        foreach (var node in executionOrder)
        {
            if (completedNodeIds.Contains(node.Id)) continue;
            context.CancellationToken.ThrowIfCancellationRequested();

            var inputData = PrepareInputData(context.Graph, node, nodeOutputs);
            var record = await ExecuteNodeWithRetryAsync(node, inputData, context);
            records.Add(record);

            if (!record.Success)
            {
                throw new InvalidOperationException($"Node '{node.Name}' failed: {record.Error}");
            }

            nodeOutputs[node.Id] = record.OutputData;
            completedNodeIds.Add(node.Id);
        }
    }

    private async Task ExecuteParallelAsync(
        ExecutionContext context,
        List<NodeExecutionRecord> records,
        Dictionary<Guid, object?> nodeOutputs,
        HashSet<Guid> completedNodeIds)
    {
        var levels = context.Graph.GetParallelExecutionLevels();

        foreach (var level in levels)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var nodesToRun = level.Where(n => !completedNodeIds.Contains(n.Id)).ToList();
            if (nodesToRun.Count == 0) continue;

            var tasks = nodesToRun.Select(async node =>
            {
                var inputData = PrepareInputData(context.Graph, node, nodeOutputs);
                return (Node: node, Record: await ExecuteNodeWithRetryAsync(node, inputData, context));
            }).ToList();

            var results = await Task.WhenAll(tasks);

            foreach (var (node, record) in results)
            {
                records.Add(record);

                if (!record.Success)
                {
                    throw new InvalidOperationException($"Node '{node.Name}' failed: {record.Error}");
                }

                nodeOutputs[node.Id] = record.OutputData;
                completedNodeIds.Add(node.Id);
            }
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
                
                var record = await ExecuteNodeWithRetryAsync(node, inputData, context);
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

    /// <summary>
    /// Retry policy ile node çalıştırır
    /// </summary>
    private async Task<NodeExecutionRecord> ExecuteNodeWithRetryAsync(
        INode node,
        object? inputData,
        ExecutionContext context)
    {
        var policy = context.RetryPolicy;
        var retryCount = 0;
        NodeExecutionRecord? lastRecord = null;

        for (var attempt = 0; attempt <= policy.MaxRetries; attempt++)
        {
            // Circuit breaker kontrolü
            if (!_circuitBreaker.AllowRequest())
            {
                return new NodeExecutionRecord
                {
                    NodeId = node.Id,
                    NodeName = node.Name,
                    NodeType = node.Type,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow,
                    Success = false,
                    Error = "Circuit breaker is open. Too many failures.",
                    InputData = inputData,
                    RetryCount = retryCount
                };
            }

            lastRecord = await ExecuteNodeAsync(node, inputData, context);

            if (lastRecord.Success)
            {
                _circuitBreaker.RecordSuccess();
                return lastRecord with { RetryCount = retryCount };
            }

            _circuitBreaker.RecordFailure();
            retryCount++;

            // Son deneme ise retry yapma
            if (attempt >= policy.MaxRetries) break;

            // Exponential backoff ile bekle
            var delay = TimeSpan.FromMilliseconds(
                Math.Min(
                    policy.InitialDelay.TotalMilliseconds * Math.Pow(policy.BackoffMultiplier, attempt),
                    policy.MaxDelay.TotalMilliseconds));

            await EmitEventAsync(context, new LogEmittedEvent
            {
                ExecutionId = context.ExecutionId,
                NodeId = node.Id,
                Level = "warning",
                Message = $"Node '{node.Name}' failed (attempt {attempt + 1}/{policy.MaxRetries + 1}). Retrying in {delay.TotalSeconds:F1}s..."
            });

            await Task.Delay(delay, context.CancellationToken);

            // Node durumunu sıfırla
            if (node is NodeBase nodeBase)
                nodeBase.Reset();
        }

        return lastRecord! with { RetryCount = retryCount };
    }
    
    private async Task<NodeExecutionRecord> ExecuteNodeAsync(
        INode node,
        object? inputData,
        ExecutionContext context)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // State transition: Idle -> Pending -> Running
            if (node.State == ExecutionState.Idle)
                node.State = ExecutionState.Pending;
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

    private static ExecutionCheckpoint CreateCheckpoint(
        HashSet<Guid> completedNodeIds,
        Dictionary<Guid, object?> nodeOutputs,
        List<NodeExecutionRecord> records)
    {
        return new ExecutionCheckpoint
        {
            CompletedNodeIds = [.. completedNodeIds],
            NodeOutputs = new Dictionary<Guid, object?>(nodeOutputs),
            Records = [.. records]
        };
    }
}
