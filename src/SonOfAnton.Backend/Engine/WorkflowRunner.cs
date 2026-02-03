using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;
using SonOfAnton.Shared.Enums;
using LogLevel = SonOfAnton.Shared.Enums.LogLevel;

namespace SonOfAnton.Backend.Engine;

/// <summary>
/// Workflow �al��t�rma motoru
/// JSON'dan ak��� okur, node'lar� s�rayla �al��t�r�r
/// </summary>
public class WorkflowRunner
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkflowRunner> _logger;
    private readonly LogStreamService _logStream;
    private readonly Dictionary<string, INodeHandler> _handlers;
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    public WorkflowRunner(
        IServiceProvider serviceProvider,
        ILogger<WorkflowRunner> logger,
        LogStreamService logStream,
        IEnumerable<INodeHandler> handlers)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _logStream = logStream;
        _handlers = handlers.ToDictionary(h => h.NodeType, h => h);
    }

    /// <summary>
    /// Execution'� �al��t�r - Hangfire taraf�ndan �a�r�l�r
    /// </summary>
    public async Task RunAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

        var execution = await dbContext.Executions
            .Include(e => e.Workflow)
            .FirstOrDefaultAsync(e => e.Id == executionId, cancellationToken);

        if (execution == null)
        {
            _logger.LogError("Execution bulunamad�: {ExecutionId}", executionId);
            return;
        }

        var workflow = execution.Workflow;
        if (workflow.GraphData == null)
        {
            await FailExecution(dbContext, execution, "Workflow graph verisi bulunamad�");
            return;
        }


        try
        {
            // Status'u Running yap
            await _dbLock.WaitAsync(cancellationToken);
            try
            {
                execution.Status = ExecutionStatus.Running;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                _dbLock.Release();
            }

            await WriteLog(dbContext, execution, null, LogLevel.Info, $"Workflow ba�lat�l�yor: {workflow.Name}");

            // Graph'� parse et
            var graph = JsonSerializer.Deserialize<WorkflowGraphDto>(workflow.GraphData.RootElement.GetRawText());
            if (graph == null || graph.Nodes.Count == 0)
            {
                await FailExecution(dbContext, execution, "Workflow bo� veya ge�ersiz");
                return;
            }

            // Memory'i haz�rla
            var memory = new Dictionary<string, object?>();
            if (execution.Memory != null)
            {
                memory = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                    execution.Memory.RootElement.GetRawText()) ?? [];
            }

            // Trigger node'lar�n� bul (giri�i olmayan node'lar)
            var triggerNodes = graph.Nodes
                .Where(n => !graph.Connections.Any(c => c.TargetNodeId == n.Id))
                .ToList();

            if (triggerNodes.Count == 0)
            {
                await FailExecution(dbContext, execution, "Ba�lang�� node'u bulunamad�");
                return;
            }

            // Her trigger'dan ba�layarak �al��t�r
            foreach (var triggerNode in triggerNodes)
            {
                await ExecuteNodeChainAsync(
                    dbContext, execution, graph, triggerNode,
                    null, memory, cancellationToken);
            }

            // Ba�ar�l� tamamla
            await _dbLock.WaitAsync(cancellationToken);
            try
            {
                execution.Status = ExecutionStatus.Completed;
                execution.CompletedAt = DateTime.UtcNow;
                execution.Memory = JsonDocument.Parse(JsonSerializer.Serialize(memory));
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                _dbLock.Release();
            }

            await WriteLog(dbContext, execution, null, LogLevel.Info, "Workflow ba�ar�yla tamamland�");
            
            // SSE stream'i tamamla
            _logStream.Complete(executionId);
        }
        catch (OperationCanceledException)
        {
            await FailExecution(dbContext, execution, "�ptal edildi");
            _logStream.Complete(executionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow execution hatas�: {ExecutionId}", executionId);
            await FailExecution(dbContext, execution, ex.Message);
            _logStream.Complete(executionId);
        }
    }

    private async Task ExecuteNodeChainAsync(
        SonOfAntonDbContext dbContext,
        ExecutionEntity execution,
        WorkflowGraphDto graph,
        WorkflowNodeDto node,
        object? inputData,
        Dictionary<string, object?> memory,
        CancellationToken cancellationToken)
    {
        // Current node'u g�ncelle
        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            execution.CurrentNodeId = node.Id;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _dbLock.Release();
        }

        await WriteLog(dbContext, execution, node.Id, LogLevel.Info, 
            $"Node �al��t�r�l�yor: {node.Name} ({node.Type})");

        // Handler'� bul
        if (!_handlers.TryGetValue(node.Type, out var handler))
        {
            _logger.LogWarning("Handler bulunamad�: {NodeType}, veri ge�iriliyor", node.Type);
            // Handler yoksa veriyi oldu�u gibi ge�ir
            await ExecuteNextNodes(dbContext, execution, graph, node, inputData, "output", memory, cancellationToken);
            return;
        }

        // Node log'lar�n� topla
        var pendingLogs = new List<(string msg, LogLevel level, object? data)>();

        // Context olu�tur
        var context = new NodeExecutionContext
        {
            ExecutionId = execution.Id,
            Node = node,
            InputData = inputData,
            Memory = memory,
            CancellationToken = cancellationToken,
            Log = (msg, level, data) =>
            {
                // Log'lar� kuyru�a ekle, sonra toplu yaz�lacak
                pendingLogs.Add((msg, level, data));
            }
        };

        // Node'u �al��t�r
        var result = await handler.ExecuteAsync(context);

        // Biriken log'lar� yaz
        foreach (var (msg, level, data) in pendingLogs)
        {
            await WriteLog(dbContext, execution, node.Id, level, msg, data);
        }

        if (!result.Success)
        {
            throw new InvalidOperationException($"Node hatas� ({node.Name}): {result.Error}");
        }

        // Memory'e yaz
        memory[$"node_{node.Id}"] = result.OutputData;

        // Sonraki node'lar� �al��t�r
        await ExecuteNextNodes(dbContext, execution, graph, node, result.OutputData, 
            result.OutputPort, memory, cancellationToken);
    }

    private async Task ExecuteNextNodes(
        SonOfAntonDbContext dbContext,
        ExecutionEntity execution,
        WorkflowGraphDto graph,
        WorkflowNodeDto currentNode,
        object? outputData,
        string outputPort,
        Dictionary<string, object?> memory,
        CancellationToken cancellationToken)
    {
        // Belirtilen port'tan ��kan ba�lant�lar� bul
        var connections = graph.Connections
            .Where(c => c.SourceNodeId == currentNode.Id && c.SourcePort == outputPort)
            .ToList();

        foreach (var connection in connections)
        {
            var nextNode = graph.Nodes.FirstOrDefault(n => n.Id == connection.TargetNodeId);
            if (nextNode != null)
            {
                await ExecuteNodeChainAsync(
                    dbContext, execution, graph, nextNode,
                    outputData, memory, cancellationToken);
            }
        }
    }


    private async Task WriteLog(
        SonOfAntonDbContext dbContext,
        ExecutionEntity execution,
        Guid? nodeId,
        LogLevel level,
        string message,
        object? data = null)
    {
        await _dbLock.WaitAsync();
        try
        {
            var log = new ExecutionLogEntity
            {
                ExecutionId = execution.Id,
                NodeId = nodeId,
                Level = level,
                Message = message,
                Data = data != null ? JsonDocument.Parse(JsonSerializer.Serialize(data)) : null
            };

            dbContext.ExecutionLogs.Add(log);
            await dbContext.SaveChangesAsync();

            // SSE stream'e de yaz
            var logEvent = new LogEventDto
            {
                ExecutionId = execution.Id,
                NodeId = nodeId,
                Level = level,
                Message = message,
                Data = data,
                Timestamp = log.Timestamp
            };
            
            await _logStream.WriteAsync(execution.Id, logEvent);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private async Task FailExecution(SonOfAntonDbContext dbContext, ExecutionEntity execution, string error)
    {
        await _dbLock.WaitAsync();
        try
        {
            execution.Status = ExecutionStatus.Failed;
            execution.ErrorMessage = error;
            execution.CompletedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }
        finally
        {
            _dbLock.Release();
        }

        await WriteLog(dbContext, execution, null, LogLevel.Error, $"Workflow ba�ar�s�z: {error}");
    }
}

