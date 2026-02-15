using System.Collections.Concurrent;
using System.Threading.Channels;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Backend.A2A;

/// <summary>
/// A2A Task yaşam döngüsü yönetimi — task oluşturma, izleme, iptal
/// </summary>
public class A2ATaskManager
{
    private readonly AgentRegistry _registry;
    private readonly ILogger<A2ATaskManager> _logger;
    private readonly ConcurrentDictionary<Guid, A2ATaskDto> _tasks = new();

    public A2ATaskManager(AgentRegistry registry, ILogger<A2ATaskManager> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// Yeni task oluşturup agent'a gönderir (task/send)
    /// </summary>
    public async Task<A2ATaskDto> SendAsync(A2ATaskRequest request, CancellationToken ct = default)
    {
        var agent = _registry.Get(request.AgentId)
            ?? throw new InvalidOperationException($"Agent bulunamadı: {request.AgentId}");

        var task = new A2ATaskDto
        {
            Id = Guid.NewGuid(),
            AgentId = request.AgentId,
            State = A2ATaskState.Submitted,
            Input = request.Input,
            CreatedAt = DateTime.UtcNow
        };

        _tasks[task.Id] = task;
        _logger.LogInformation("A2A Task oluşturuldu: {TaskId} -> {AgentId}", task.Id, request.AgentId);

        // Agent'ı çalıştır
        task.State = A2ATaskState.Working;

        try
        {
            var result = await agent.ProcessAsync(request.Input, ct);
            task.State = result.State;
            task.Output = result.Output;
            task.Artifacts = result.Artifacts;
            task.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            task.State = A2ATaskState.Failed;
            task.Output = ex.Message;
            task.CompletedAt = DateTime.UtcNow;
            _logger.LogError(ex, "A2A Task başarısız: {TaskId}", task.Id);
        }

        return task;
    }

    /// <summary>
    /// Task durumunu sorgular (task/get)
    /// </summary>
    public A2ATaskDto? GetTask(Guid taskId)
    {
        return _tasks.TryGetValue(taskId, out var task) ? task : null;
    }

    /// <summary>
    /// Task iptal eder (task/cancel)
    /// </summary>
    public bool CancelTask(Guid taskId)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.State = A2ATaskState.Canceled;
            task.CompletedAt = DateTime.UtcNow;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Tüm aktif task'ları listele
    /// </summary>
    public IReadOnlyList<A2ATaskDto> GetActiveTasks()
    {
        return _tasks.Values
            .Where(t => t.State is A2ATaskState.Submitted or A2ATaskState.Working or A2ATaskState.InputRequired)
            .ToList();
    }
}
