using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.A2A;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/a2a")]
public class A2AController : ControllerBase
{
    private readonly AgentRegistry _registry;
    private readonly A2ATaskManager _taskManager;

    public A2AController(AgentRegistry registry, A2ATaskManager taskManager)
    {
        _registry = registry;
        _taskManager = taskManager;
    }

    /// <summary>
    /// Agent card — A2A spesifikasyonuna uygun tüm agent'ları döndürür
    /// </summary>
    [HttpGet("agents")]
    public ActionResult<List<AgentCardDto>> GetAgents()
    {
        return Ok(_registry.GetAllCards());
    }

    /// <summary>
    /// Tek agent card — /.well-known/agent.json benzeri
    /// </summary>
    [HttpGet("agents/{agentId}")]
    public ActionResult<AgentCardDto> GetAgent(string agentId)
    {
        var agent = _registry.Get(agentId);
        if (agent == null) return NotFound();
        return Ok(agent.GetCard());
    }

    /// <summary>
    /// Task gönder (task/send)
    /// </summary>
    [HttpPost("tasks/send")]
    public async Task<ActionResult<A2ATaskDto>> SendTask([FromBody] A2ATaskRequest request, CancellationToken ct)
    {
        try
        {
            var task = await _taskManager.SendAsync(request, ct);
            return Ok(task);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Task durumu sorgula (task/get)
    /// </summary>
    [HttpGet("tasks/{taskId:guid}")]
    public ActionResult<A2ATaskDto> GetTask(Guid taskId)
    {
        var task = _taskManager.GetTask(taskId);
        if (task == null) return NotFound();
        return Ok(task);
    }

    /// <summary>
    /// Task iptal (task/cancel)
    /// </summary>
    [HttpPost("tasks/{taskId:guid}/cancel")]
    public IActionResult CancelTask(Guid taskId)
    {
        return _taskManager.CancelTask(taskId)
            ? Ok(new { message = "Task cancelled" })
            : NotFound();
    }

    /// <summary>
    /// Aktif task listesi
    /// </summary>
    [HttpGet("tasks")]
    public ActionResult<List<A2ATaskDto>> GetActiveTasks()
    {
        return Ok(_taskManager.GetActiveTasks());
    }
}
