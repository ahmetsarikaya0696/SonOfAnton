using System.Text.Json;
using System.Threading.Channels;
using SonOfAnton.Backend.Agents;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Enums;
using SonOfAnton.Shared.Events;
using SonOfAnton.Shared.Models;
using SonOfAnton.Shared.VirtualFileSystem;

namespace SonOfAnton.Backend.Orchestrator;

/// <summary>
/// Ana orchestrator servisi
/// State machine mantığını yönetir ve agent'ları koordine eder.
/// CodeGeneration + WorkflowAssistant modları desteklenir.
/// </summary>
public class ProjectOrchestrator
{
    private readonly AgentService _agentService;
    private readonly WorkflowAssistantService _workflowAssistant;
    private readonly IVirtualFileSystem _vfs;
    private readonly ILogger<ProjectOrchestrator> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ProjectOrchestrator(
        AgentService agentService,
        WorkflowAssistantService workflowAssistant,
        IVirtualFileSystem vfs,
        ILogger<ProjectOrchestrator> logger)
    {
        _agentService = agentService;
        _workflowAssistant = workflowAssistant;
        _vfs = vfs;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    /// <summary>
    /// Kullanıcı isteğini işler ve event stream döndürür (CodeGeneration modu)
    /// </summary>
    public async IAsyncEnumerable<AgentEvent> ProcessRequestAsync(
        string userPrompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<AgentEvent>();
        _ = Task.Run(async () => await ExecuteOrchestrationAsync(userPrompt, channel.Writer, ct), ct);

        await foreach (var evt in channel.Reader.ReadAllAsync(ct))
        {
            yield return evt;
        }
    }

    /// <summary>
    /// Workflow assist modu — doğal dilden workflow düzenleme
    /// </summary>
    public async IAsyncEnumerable<AgentEvent> ProcessWorkflowAssistAsync(
        string userPrompt,
        WorkflowGraphDto? currentGraph,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<AgentEvent>();
        _ = Task.Run(async () => await ExecuteWorkflowAssistAsync(userPrompt, currentGraph, channel.Writer, ct), ct);

        await foreach (var evt in channel.Reader.ReadAllAsync(ct))
        {
            yield return evt;
        }
    }

    private async Task ExecuteWorkflowAssistAsync(
        string userPrompt,
        WorkflowGraphDto? currentGraph,
        ChannelWriter<AgentEvent> writer,
        CancellationToken ct)
    {
        try
        {
            await writer.WriteAsync(CreateLog("🔧 Workflow düzenleme asistanı başlatılıyor..."), ct);

            var request = new WorkflowChatRequest
            {
                Message = userPrompt,
                CurrentGraph = currentGraph
            };

            var response = await _workflowAssistant.ProcessChatAsync(request, ct);

            // Chat mesajı event'i
            await writer.WriteAsync(new AgentEvent
            {
                Type = EventType.ChatMessage,
                Payload = response.Message
            }, ct);

            // Önerilen graph varsa diff event'i gönder
            if (response.SuggestedGraph != null)
            {
                await writer.WriteAsync(new AgentEvent
                {
                    Type = EventType.WorkflowDiff,
                    Payload = JsonSerializer.Serialize(new
                    {
                        before = currentGraph,
                        after = response.SuggestedGraph,
                        yaml = response.YamlPreview
                    }, _jsonOptions)
                }, ct);
            }

            await writer.WriteAsync(new AgentEvent
            {
                Type = EventType.Finish,
                Payload = JsonSerializer.Serialize(new { message = "Workflow asistan yanıtı hazır" }, _jsonOptions)
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow assist failed");
            await writer.WriteAsync(CreateError($"Workflow asistan hatası: {ex.Message}"), ct);
        }
        finally
        {
            writer.Complete();
        }
    }

    private async Task ExecuteOrchestrationAsync(
        string userPrompt, 
        ChannelWriter<AgentEvent> writer,
        CancellationToken ct)
    {
        var context = new OrchestratorContext
        {
            UserPrompt = userPrompt,
            CancellationToken = ct,
            EventEmitter = (evt, token) => Task.CompletedTask
        };

        try
        {
            _vfs.Clear();
            
            // INIT
            context.CurrentState = OrchestratorState.Init;
            await writer.WriteAsync(CreateLog($"?? Starting analysis for: {userPrompt[..Math.Min(100, userPrompt.Length)]}..."), ct);

            // PLANNING
            context.CurrentState = OrchestratorState.Planning;
            await writer.WriteAsync(CreateLog("?? Analyst is creating project plan..."), ct);

            ProjectPlan? plan = null;
            context.RetryCount = 0;

            while (plan == null && context.RetryCount < OrchestratorContext.MaxRetries)
            {
                try
                {
                    plan = await _agentService.AnalyzeAsync(userPrompt, ct);
                    
                    if (plan == null)
                    {
                        context.RetryCount++;
                        await writer.WriteAsync(CreateLog($"?? Invalid plan format, retrying ({context.RetryCount}/{OrchestratorContext.MaxRetries})..."), ct);
                    }
                }
                catch (Exception ex)
                {
                    context.RetryCount++;
                    _logger.LogError(ex, "Analyst failed");
                    await writer.WriteAsync(CreateError($"Analyst error: {ex.Message}"), ct);
                }
            }

            if (plan == null)
            {
                context.CurrentState = OrchestratorState.Error;
                await writer.WriteAsync(CreateError("? Failed to create project plan after maximum retries"), ct);
                return;
            }

            context.Plan = plan;
            context.CurrentState = OrchestratorState.Validating;
            
            // Plan event'i
            await writer.WriteAsync(new AgentEvent
            {
                Type = EventType.Plan,
                Payload = JsonSerializer.Serialize(plan, _jsonOptions)
            }, ct);
            
            await writer.WriteAsync(CreateLog($"? Plan created: {plan.ProjectName} with {plan.Tasks.Count} tasks"), ct);

            // VISUALIZING
            context.CurrentState = OrchestratorState.Visualizing;
            await writer.WriteAsync(CreateLog("?? Generating architecture diagram..."), ct);

            try
            {
                var diagram = await _agentService.VisualizeAsync(plan, ct);
                await writer.WriteAsync(new AgentEvent
                {
                    Type = EventType.Diagram,
                    Payload = diagram
                }, ct);
                await writer.WriteAsync(CreateLog("? Diagram generated"), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Visualizer failed, continuing without diagram");
                await writer.WriteAsync(CreateLog("?? Diagram generation failed, continuing..."), ct);
            }

            // EXECUTION LOOP
            context.CurrentState = OrchestratorState.Executing;
            
            for (var i = 0; i < plan.Tasks.Count; i++)
            {
                var task = plan.Tasks[i];
                context.CurrentTaskIndex = i;
                
                await writer.WriteAsync(CreateTaskUpdate(task.Id, DevTaskStatus.InProgress), ct);
                await writer.WriteAsync(CreateLog($"?? [{i + 1}/{plan.Tasks.Count}] Working on: {task.Description}"), ct);

                // Context Loading - ba��ml� dosyalar� y�kle
                var relatedFiles = new Dictionary<string, string>();
                foreach (var depId in task.DependsOn)
                {
                    var depTask = plan.Tasks.FirstOrDefault(t => t.Id == depId);
                    if (depTask != null)
                    {
                        var content = _vfs.ReadFile(depTask.TargetFile);
                        if (content != null)
                        {
                            relatedFiles[depTask.TargetFile] = content;
                        }
                    }
                }

                // CODING
                context.CurrentState = OrchestratorState.Coding;
                string? code = null;
                
                try
                {
                    code = await _agentService.GenerateCodeAsync(task, plan.ArchitectureRules, relatedFiles, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Coder failed for task {TaskId}", task.Id);
                    await writer.WriteAsync(CreateError($"Code generation failed: {ex.Message}"), ct);
                    await writer.WriteAsync(CreateTaskUpdate(task.Id, DevTaskStatus.Failed), ct);
                    continue;
                }

                if (string.IsNullOrEmpty(code))
                {
                    await writer.WriteAsync(CreateTaskUpdate(task.Id, DevTaskStatus.Failed), ct);
                    continue;
                }

                // AUDITING
                context.CurrentState = OrchestratorState.Auditing;
                context.RetryCount = 0;
                var auditPassed = false;

                while (!auditPassed && context.RetryCount < OrchestratorContext.MaxRetries)
                {
                    try
                    {
                        var auditResult = await _agentService.AuditCodeAsync(
                            code, task.TargetFile, plan.ArchitectureRules, ct);

                        if (auditResult?.Passed == true)
                        {
                            auditPassed = true;
                            await writer.WriteAsync(CreateLog($"? Code passed audit: {auditResult.Summary}"), ct);
                        }
                        else
                        {
                            context.RetryCount++;
                            var issueCount = auditResult?.Issues.Count ?? 0;
                            await writer.WriteAsync(CreateLog($"?? Audit found {issueCount} issues, fixing ({context.RetryCount}/{OrchestratorContext.MaxRetries})..."), ct);

                            // FIXING
                            context.CurrentState = OrchestratorState.Fixing;
                            code = await _agentService.FixCodeAsync(
                                code, auditResult!, plan.ArchitectureRules, ct);
                            context.CurrentState = OrchestratorState.Auditing;
                        }
                    }
                    catch (Exception ex)
                    {
                        context.RetryCount++;
                        _logger.LogWarning(ex, "Audit/Fix cycle failed");
                        await writer.WriteAsync(CreateLog($"?? Audit error, retrying..."), ct);
                    }
                }

                if (!auditPassed)
                {
                    await writer.WriteAsync(CreateLog("?? Max retries reached, using last version"), ct);
                }

                // COMMIT - VFS'ye yaz
                _vfs.WriteFile(task.TargetFile, code);
                
                await writer.WriteAsync(new AgentEvent
                {
                    Type = EventType.Code,
                    Payload = JsonSerializer.Serialize(new { path = task.TargetFile, content = code }, _jsonOptions),
                    TaskId = task.Id
                }, ct);

                task.IsCompleted = true;
                task.Status = DevTaskStatus.Completed;
                await writer.WriteAsync(CreateTaskUpdate(task.Id, DevTaskStatus.Completed), ct);
                await writer.WriteAsync(CreateLog($"? Task completed: {task.TargetFile}"), ct);
            }

            // FINALIZE
            context.CurrentState = OrchestratorState.AwaitingApproval;
            await writer.WriteAsync(CreateLog("?? All tasks completed! VFS contains:"), ct);
            
            foreach (var file in _vfs.GetAllFiles())
            {
                await writer.WriteAsync(CreateLog($"  ?? {file}"), ct);
            }

            await writer.WriteAsync(new AgentEvent
            {
                Type = EventType.Finish,
                Payload = JsonSerializer.Serialize(new 
                { 
                    message = "Project generation complete",
                    files = _vfs.GetAllFiles().ToList(),
                    readyToFlush = true
                }, _jsonOptions)
            }, ct);

            context.CurrentState = OrchestratorState.Completed;
        }
        catch (OperationCanceledException)
        {
            await writer.WriteAsync(CreateLog("?? Operation cancelled"), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orchestration failed");
            await writer.WriteAsync(CreateError($"Orchestration error: {ex.Message}"), ct);
        }
        finally
        {
            writer.Complete();
        }
    }

    /// <summary>
    /// VFS i�eri�ini diske yazar
    /// </summary>
    public async Task<bool> FlushToDiskAsync(string basePath, CancellationToken ct = default)
    {
        try
        {
            await _vfs.FlushToDiskAsync(basePath, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush VFS to disk at {Path}", basePath);
            return false;
        }
    }

    private static AgentEvent CreateLog(string message) => new()
    {
        Type = EventType.Log,
        Payload = message
    };

    private static AgentEvent CreateError(string message) => new()
    {
        Type = EventType.Error,
        Payload = message
    };

    private static AgentEvent CreateTaskUpdate(int taskId, DevTaskStatus status) => new()
    {
        Type = EventType.TaskUpdate,
        Payload = status.ToString(),
        TaskId = taskId
    };
}

