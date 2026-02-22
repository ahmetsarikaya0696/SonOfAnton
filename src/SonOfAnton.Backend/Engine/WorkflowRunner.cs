using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Backend.Engine.Expressions;
using SonOfAnton.Backend.Execution;
using SonOfAnton.Backend.Services;
using SonOfAnton.Backend.Services.Snapshots;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;
using SonOfAnton.Shared.Enums;
using LogLevel = SonOfAnton.Shared.Enums.LogLevel;
using SonOfAnton.Shared.Exceptions;
using SonOfAnton.Backend.Services.State;

namespace SonOfAnton.Backend.Engine;

/// <summary>
/// Workflow çalıştırma motoru
/// JSON'dan akışı okur, node'ları sırayla çalıştırır
/// Retry, timeout ve hata izolasyonu desteği
/// </summary>
public class WorkflowRunner
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkflowRunner> _logger;
    private readonly LogStreamService _logStream;
    private readonly PerformanceMetricsService _metrics;
    private readonly PauseTokenRegistry _pauseTokens;
    private readonly Dictionary<string, INodeHandler> _handlers;
    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private readonly IStateRepository _stateRepository;

    /// <summary>
    /// Node bazında varsayılan timeout (30 saniye)
    /// </summary>
    private static readonly TimeSpan DefaultNodeTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Node başına maksimum retry sayısı
    /// </summary>
    private const int MaxNodeRetries = 3;

    public WorkflowRunner(
        IServiceProvider serviceProvider,
        ILogger<WorkflowRunner> logger,
        LogStreamService logStream,
        PerformanceMetricsService metrics,
        PauseTokenRegistry pauseTokens,
        IEnumerable<INodeHandler> handlers,
        IStateRepository stateRepository)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _logStream = logStream;
        _metrics = metrics;
        _pauseTokens = pauseTokens;
        _handlers = handlers.ToDictionary(h => h.NodeType, h => h);
        _stateRepository = stateRepository;
    }

    /// <summary>
    /// Execution'ı çalıştır - Hangfire tarafından çağrılır
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
            _logger.LogError("Execution bulunamadı: {ExecutionId}", executionId);
            return;
        }

        var workflow = execution.Workflow;
        if (workflow.GraphData == null)
        {
            await FailExecution(dbContext, execution, "Workflow graph verisi bulunamadı");
            return;
        }

        var sw = Stopwatch.StartNew();
        _metrics.RecordWorkflowStarted(execution.WorkflowId);
        var memory = new Dictionary<string, object?>();

        try
        {
            // Status'u Running yap
            await UpdateExecutionStatusAsync(dbContext, execution, ExecutionStatus.Running, cancellationToken);

            await WriteLog(dbContext, execution, null, LogLevel.Info, $"Workflow başlatılıyor: {workflow.Name}");

            // Graph'ı parse et
            var graph = JsonSerializer.Deserialize<WorkflowGraphDto>(workflow.GraphData.RootElement.GetRawText());
            if (graph == null || graph.Nodes.Count == 0)
            {
                await FailExecution(dbContext, execution, "Workflow boş veya geçersiz");
                return;
            }

            // Memory'i hazırla
            if (execution.Memory != null)
            {
                memory = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                    execution.Memory.RootElement.GetRawText()) ?? [];
            }

            // Workflow değişkenlerini memory'ye yükle
            if (graph.Variables.Count > 0)
            {
                foreach (var variable in graph.Variables)
                {
                    if (!memory.ContainsKey(variable.Name) && variable.DefaultValue != null)
                    {
                        memory[variable.Name] = variable.DefaultValue;
                    }
                }
            }

            // Çevrimsel bağımlılık kontrolü
            var visitedNodes = new HashSet<Guid>();

            // Trigger node'larını bul (girişi olmayan node'lar)
            var triggerNodes = graph.Nodes
                .Where(n => !graph.Connections.Any(c => c.TargetNodeId == n.Id))
                .ToList();

            if (triggerNodes.Count == 0)
            {
                await FailExecution(dbContext, execution, "Başlangıç node'u bulunamadı");
                return;
            }

            // Her trigger'dan başlayarak çalıştır
            foreach (var triggerNode in triggerNodes)
            {
                await ExecuteNodeChainAsync(
                    dbContext, execution, graph, triggerNode,
                    null, memory, visitedNodes, cancellationToken);
            }

            sw.Stop();

            // Başarılı tamamla
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

            _metrics.RecordWorkflowCompleted(execution.WorkflowId, sw.ElapsedMilliseconds);

            // İstatistik güncelle
            var statsUpdater = scope.ServiceProvider.GetRequiredService<WorkflowStatsUpdater>();
            await statsUpdater.RecordExecutionAsync(execution.WorkflowId, true, sw.ElapsedMilliseconds, cancellationToken);

            await WriteLog(dbContext, execution, null, LogLevel.Info, 
                $"Workflow başarıyla tamamlandı ({sw.ElapsedMilliseconds}ms)");
            
            // SSE stream'i tamamla
            _logStream.Complete(executionId);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _metrics.RecordWorkflowFailed(execution.WorkflowId, "İptal edildi", sw.ElapsedMilliseconds);
            var statsUpdater = scope.ServiceProvider.GetRequiredService<WorkflowStatsUpdater>();
            await statsUpdater.RecordExecutionAsync(execution.WorkflowId, false, sw.ElapsedMilliseconds);
            await FailExecution(dbContext, execution, $"İptal edildi ({sw.ElapsedMilliseconds}ms)");
            _logStream.Complete(executionId);
        }
        catch (SuspendExecutionException ex)
        {
            sw.Stop();
            using var repoScope = _serviceProvider.CreateScope();
            var stateRepo = repoScope.ServiceProvider.GetRequiredService<IStateRepository>();
            await stateRepo.SaveCheckpointAsync(
                execution.Id, 
                ex.NodeId, 
                null, 
                JsonDocument.Parse(JsonSerializer.Serialize(memory)), 
                "Suspended"
            );
            
            await UpdateExecutionStatusAsync(dbContext, execution, ExecutionStatus.Paused, cancellationToken);
            await WriteLog(dbContext, execution, ex.NodeId, LogLevel.Info, $"Workflow duraklatıldı: {ex.Message}");
            _logStream.Complete(executionId);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _metrics.RecordWorkflowFailed(execution.WorkflowId, ex.Message, sw.ElapsedMilliseconds);
            var statsUpdater = scope.ServiceProvider.GetRequiredService<WorkflowStatsUpdater>();
            await statsUpdater.RecordExecutionAsync(execution.WorkflowId, false, sw.ElapsedMilliseconds);
            
            // Rollback/Saga pattern tetikle
            await ExecuteRollbackAsync(dbContext, execution, memory, cancellationToken);
            
            // Otomatik snapshot al
            var snapshotSvc = scope.ServiceProvider.GetRequiredService<SnapshotService>();
            await snapshotSvc.TakeAutomaticAsync(executionId, execution.CurrentNodeId, memory);
            _logger.LogError(ex, "Workflow execution hatası: {ExecutionId} ({Elapsed}ms)", 
                executionId, sw.ElapsedMilliseconds);
            await FailExecution(dbContext, execution, ex.Message);
            _logStream.Complete(executionId);

            // GLOBAL ERROR HANDLER
            if (workflow.GlobalErrorWorkflowId.HasValue)
            {
                try
                {
                    var jobService = scope.ServiceProvider.GetRequiredService<WorkflowJobService>();
                    var errorData = new {
                        sourceWorkflowId = workflow.Id,
                        sourceWorkflowName = workflow.Name,
                        executionId = execution.Id,
                        errorMessage = ex.Message,
                        failedNodeId = execution.CurrentNodeId
                    };
                    jobService.EnqueueWorkflow(workflow.GlobalErrorWorkflowId.Value, errorData);
                    _logger.LogInformation("Global Error Workflow tetiklendi: {ErrorWorkflowId}", workflow.GlobalErrorWorkflowId.Value);
                }
                catch (Exception handlerEx)
                {
                    _logger.LogError(handlerEx, "Global Error Workflow tetiklenirken hata oluştu.");
                }
            }
        }
    }

    /// <summary>
    /// Duraklatılmış bir Execution'ı (veya hata almış) bir Checkpoint noktasından itibaren devam ettirir.
    /// </summary>
    public async Task ResumeAsync(Guid executionId, Guid nodeId, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();
        var stateRepo = scope.ServiceProvider.GetRequiredService<IStateRepository>();

        var execution = await dbContext.Executions
            .Include(e => e.Workflow)
            .FirstOrDefaultAsync(e => e.Id == executionId, cancellationToken);
            
        if (execution == null || (execution.Status != ExecutionStatus.Paused && execution.Status != ExecutionStatus.Failed))
        {
            _logger.LogWarning("Execution ({ExecutionId}) devam ettirilemez durumda: {Status}", executionId, execution?.Status);
            return;
        }

        var checkpoint = await stateRepo.GetCheckpointAsync(executionId, nodeId) ?? await stateRepo.GetLatestCheckpointAsync(executionId);
        if (checkpoint == null) 
        {
            _logger.LogError("Checkpoint bulunamadı: {ExecutionId}", executionId);
            return;
        }

        var workflow = execution.Workflow;
        var graph = JsonSerializer.Deserialize<WorkflowGraphDto>(workflow.GraphData!.RootElement.GetRawText());
        var node = graph?.Nodes.FirstOrDefault(n => n.Id == checkpoint.NodeId);
        if (node == null)
            return;

        var memory = checkpoint.ExecutionMemory != null 
            ? JsonSerializer.Deserialize<Dictionary<string, object?>>(checkpoint.ExecutionMemory.RootElement.GetRawText()) ?? []
            : new Dictionary<string, object?>();

        var visitedNodes = new HashSet<Guid>();
        
        await UpdateExecutionStatusAsync(dbContext, execution, ExecutionStatus.Running, cancellationToken);
        await WriteLog(dbContext, execution, node.Id, LogLevel.Info, $"Workflow devam ettiriliyor: {workflow.Name} ({node.Name})");
        
        var sw = Stopwatch.StartNew();
        try 
        {
            // Belirtilen node'dan tekrar başlat
            await ExecuteNodeChainAsync(dbContext, execution, graph!, node, null, memory, visitedNodes, cancellationToken);
            
            sw.Stop();
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

            _metrics.RecordWorkflowCompleted(execution.WorkflowId, sw.ElapsedMilliseconds);
            await WriteLog(dbContext, execution, null, LogLevel.Info, $"Workflow tamamlandı ({sw.ElapsedMilliseconds}ms)");
            _logStream.Complete(executionId);
        }
        catch (SuspendExecutionException ex)
        {
            sw.Stop();
            await stateRepo.SaveCheckpointAsync(
                execution.Id, 
                ex.NodeId, 
                null, 
                JsonDocument.Parse(JsonSerializer.Serialize(memory)), 
                "Suspended"
            );
            
            await UpdateExecutionStatusAsync(dbContext, execution, ExecutionStatus.Paused, cancellationToken);
            await WriteLog(dbContext, execution, ex.NodeId, LogLevel.Info, $"Workflow duraklatıldı: {ex.Message}");
            _logStream.Complete(executionId);
        }
        catch(Exception ex)
        {
            sw.Stop();
            
            // Rollback/Saga pattern tetikle
            await ExecuteRollbackAsync(dbContext, execution, memory, cancellationToken);
            
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
        HashSet<Guid> visitedNodes,
        CancellationToken cancellationToken)
    {
        // Çevrimsel bağımlılık kontrolü
        if (!visitedNodes.Add(node.Id))
        {
            await WriteLog(dbContext, execution, node.Id, LogLevel.Warning,
                $"Çevrimsel bağımlılık algılandı, node atlanıyor: {node.Name}");
            return;
        }

        try
        {
            // Pause kontrolü — duraklatılmışsa bekle
            var pauseToken = _pauseTokens.GetOrCreate(execution.Id);
            await pauseToken.WaitIfPausedAsync(cancellationToken);

            // Current node'u güncelle
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
                $"Node çalıştırılıyor: {node.Name} ({node.Type})");

            // Handler'ı bul
            if (!_handlers.TryGetValue(node.Type, out var handler))
            {
                // Custom node fallback — custom.* veya DB'de tanımlı olanlar
                if (_handlers.TryGetValue("custom.*", out var customHandler))
                {
                    handler = customHandler;
                }
                else
                {
                    _logger.LogWarning("Handler bulunamadı: {NodeType}, veri geçiriliyor", node.Type);
                    await ExecuteNextNodes(dbContext, execution, graph, node, inputData, "output", 
                        memory, visitedNodes, cancellationToken);
                    return;
                }
            }

            // Retry mekanizması ile node çalıştır
            // Önce parametrelerdeki {{...}} ifadelerini resolve et
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

            var result = await ExecuteNodeWithRetryAsync(
                dbContext, execution, handler, resolvedNode, inputData, memory, cancellationToken);

            if (!result.Success)
            {
                // continueOnError parametresini kontrol et
                var continueOnError = node.Parameters.TryGetValue("continueOnError", out var coe) 
                    && coe?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
                
                if (continueOnError)
                {
                    await WriteLog(dbContext, execution, node.Id, LogLevel.Warning,
                        $"Node hatası (devam ediliyor): {result.Error}");
                }
                else
                {
                    var hasErrorConnection = graph.Connections.Any(c => c.SourceNodeId == node.Id && c.SourcePort == "error");
                    
                    if (hasErrorConnection)
                    {
                        await WriteLog(dbContext, execution, node.Id, LogLevel.Warning,
                            $"Node hatası yakalandı (error pinine yönlendiriliyor): {result.Error}");
                        
                        memory[$"node_{node.Id}_error"] = result.Error;
                        
                        await ExecuteNextNodes(dbContext, execution, graph, node, new { error = result.Error }, 
                            "error", memory, visitedNodes, cancellationToken);
                        
                        return;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Node hatası ({node.Name}): {result.Error}");
                    }
                }
            }

            // Memory'e yaz
            memory[$"node_{node.Id}"] = result.OutputData;

            // Sonraki node'ları çalıştır
            await ExecuteNextNodes(dbContext, execution, graph, node, result.OutputData, 
                result.OutputPort, memory, visitedNodes, cancellationToken);
        }
        finally
        {
            // Ziyaret setinden çıkar (aynı node farklı yollardan tekrar ziyaret edilebilir)
            visitedNodes.Remove(node.Id);
        }
    }

    /// <summary>
    /// Node'u retry ve timeout ile çalıştırır
    /// </summary>
    private async Task<NodeExecutionResult> ExecuteNodeWithRetryAsync(
        SonOfAntonDbContext dbContext,
        ExecutionEntity execution,
        INodeHandler handler,
        WorkflowNodeDto node,
        object? inputData,
        Dictionary<string, object?> memory,
        CancellationToken cancellationToken)
    {
        // Node-spesifik timeout
        var nodeTimeout = DefaultNodeTimeout;
        if (node.Parameters.TryGetValue("timeout", out var timeoutParam) 
            && int.TryParse(timeoutParam?.ToString(), out var timeoutMs))
        {
            nodeTimeout = TimeSpan.FromMilliseconds(timeoutMs);
        }

        // Node-spesifik retry sayısı
        var maxRetries = MaxNodeRetries;
        if (node.Parameters.TryGetValue("retries", out var retryParam) 
            && int.TryParse(retryParam?.ToString(), out var retryCount))
        {
            maxRetries = Math.Min(retryCount, 5); // Maksimum 5
        }

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (attempt > 0)
            {
                var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt - 1) * 500);
                await WriteLog(dbContext, execution, node.Id, LogLevel.Warning,
                    $"Retry {attempt}/{maxRetries}, {delay.TotalMilliseconds}ms bekleniyor...");
                await Task.Delay(delay, cancellationToken);
            }

            try
            {
                // Node log'larını topla
                var pendingLogs = new List<(string msg, LogLevel level, object? data)>();

                // Context oluştur
                var context = new NodeExecutionContext
                {
                    ExecutionId = execution.Id,
                    Node = node,
                    InputData = inputData,
                    Memory = memory,
                    CancellationToken = cancellationToken,
                    Log = (msg, level, data) =>
                    {
                        pendingLogs.Add((msg, level, data));
                    },
                    RegisterCompensation = (compensation) =>
                    {
                        var compListStr = memory.TryGetValue("__compensating_actions", out var cl) ? cl?.ToString() : null;
                        var compList = string.IsNullOrEmpty(compListStr) 
                            ? new List<CompensatingAction>() 
                            : JsonSerializer.Deserialize<List<CompensatingAction>>(compListStr) ?? new List<CompensatingAction>();
                        
                        compList.Add(compensation);
                        memory["__compensating_actions"] = JsonSerializer.Serialize(compList);
                    }
                };

                // Timeout ile çalıştır
                var sw = Stopwatch.StartNew();
                NodeExecutionResult result;
                
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(nodeTimeout);
                    context.CancellationToken = cts.Token;
                    
                    try
                    {
                        result = await handler.ExecuteAsync(context);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        // Timeout oldu
                        result = NodeExecutionResult.Fail(
                            $"Node timeout ({nodeTimeout.TotalSeconds}s aşıldı)");
                    }
                }
                
                sw.Stop();

                // Biriken log'ları yaz
                foreach (var (msg, level, data) in pendingLogs)
                {
                    await WriteLog(dbContext, execution, node.Id, level, msg, data);
                }

                // Node metriği kaydet
                _metrics.RecordNodeExecution(node.Type, sw.ElapsedMilliseconds, result.Success);

                if (result.Success)
                {
                    if (attempt > 0)
                    {
                        await WriteLog(dbContext, execution, node.Id, LogLevel.Info,
                            $"Node başarılı (retry {attempt}, {sw.ElapsedMilliseconds}ms)");
                    }
                    return result;
                }

                // Retry edilebilecek hata mı kontrol et
                if (attempt == maxRetries)
                {
                    return result; // Son deneme, hatayı döndür
                }
            }
            catch (OperationCanceledException)
            {
                throw; // İptal durumunu yukarı ilet
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                await WriteLog(dbContext, execution, node.Id, LogLevel.Warning,
                    $"Node hatası (retry {attempt + 1}): {ex.Message}");
            }
        }

        return NodeExecutionResult.Fail("Maksimum retry sayısına ulaşıldı");
    }

    private async Task ExecuteNextNodes(
        SonOfAntonDbContext dbContext,
        ExecutionEntity execution,
        WorkflowGraphDto graph,
        WorkflowNodeDto currentNode,
        object? outputData,
        string outputPort,
        Dictionary<string, object?> memory,
        HashSet<Guid> visitedNodes,
        CancellationToken cancellationToken)
    {
        // Belirtilen port'tan çıkan bağlantıları bul
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
                    outputData, memory, visitedNodes, cancellationToken);
            }
        }
    }

    private async Task UpdateExecutionStatusAsync(
        SonOfAntonDbContext dbContext,
        ExecutionEntity execution,
        ExecutionStatus status,
        CancellationToken cancellationToken)
    {
        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            execution.Status = status;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _dbLock.Release();
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

        await WriteLog(dbContext, execution, null, LogLevel.Error, $"Workflow başarısız: {error}");
    }

    private async Task ExecuteRollbackAsync(
        SonOfAntonDbContext dbContext,
        ExecutionEntity execution,
        Dictionary<string, object?> memory,
        CancellationToken cancellationToken)
    {
        if (!memory.TryGetValue("__compensating_actions", out var actionsObj)) return;
        
        var compListStr = actionsObj?.ToString();
        if (string.IsNullOrEmpty(compListStr)) return;
        
        var actions = JsonSerializer.Deserialize<List<CompensatingAction>>(compListStr);
        if (actions == null || actions.Count == 0) return;
        
        actions.Reverse(); // LIFO Order
        
        await WriteLog(dbContext, execution, null, LogLevel.Warning, "Saga Rollback başlatıldı. Telafi adımları çalıştırılıyor.");
        
        foreach (var action in actions)
        {
            if (!_handlers.TryGetValue(action.TargetNodeType, out var handler)) continue;
            
            await WriteLog(dbContext, execution, action.OriginalNodeId, LogLevel.Info, 
                $"Geri alma işlemi (Rollback) çalıştırılıyor: {action.TargetNodeType}");
                
            var mockNode = new WorkflowNodeDto 
            { 
               Id = Guid.NewGuid(), 
               Type = action.TargetNodeType, 
               Name = "Rollback",
               Parameters = action.Parameters
            };
            
            var pendingLogs = new List<(string msg, LogLevel level, object? data)>();
            var context = new NodeExecutionContext
            {
                ExecutionId = execution.Id,
                Node = mockNode,
                InputData = null,
                Memory = memory,
                CancellationToken = cancellationToken,
                Log = (msg, level, data) => pendingLogs.Add((msg, level, data))
            };
            
            try 
            {
                var result = await handler.ExecuteAsync(context);
                
                foreach (var (msg, level, data) in pendingLogs)
                    await WriteLog(dbContext, execution, action.OriginalNodeId, level, msg, data);

                if (!result.Success) 
                {
                   await WriteLog(dbContext, execution, action.OriginalNodeId, LogLevel.Error, $"Rollback adımında hata: {result.Error}");
                }
                else
                {
                   await WriteLog(dbContext, execution, action.OriginalNodeId, LogLevel.Info, "Rollback adımı başarıyla tamamlandı.");
                }
            } 
            catch (Exception ex) 
            {
                foreach (var (msg, level, data) in pendingLogs)
                    await WriteLog(dbContext, execution, action.OriginalNodeId, level, msg, data);

                await WriteLog(dbContext, execution, action.OriginalNodeId, LogLevel.Error, $"Rollback adımında istisna: {ex.Message}");
            }
        }
        
        await WriteLog(dbContext, execution, null, LogLevel.Warning, "Saga Rollback tamamlandı.");
    }
}

