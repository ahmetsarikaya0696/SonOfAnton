using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Shared.Models;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkflowController : ControllerBase
{
    private static readonly List<Workflow> _workflows = [];

    [HttpGet]
    public ActionResult<IEnumerable<Workflow>> GetAll()
    {
        return Ok(_workflows);
    }

    [HttpGet("{id}")]
    public ActionResult<Workflow> Get(Guid id)
    {
        var workflow = _workflows.FirstOrDefault(w => w.Id == id);
        if (workflow == null)
            return NotFound();

        return Ok(workflow);
    }

    [HttpPost]
    public ActionResult<Workflow> Create([FromBody] Workflow workflow)
    {
        workflow.Id = Guid.NewGuid();
        workflow.CreatedAt = DateTime.UtcNow;
        workflow.UpdatedAt = DateTime.UtcNow;
        _workflows.Add(workflow);

        return CreatedAtAction(nameof(Get), new { id = workflow.Id }, workflow);
    }

    [HttpPut("{id}")]
    public ActionResult<Workflow> Update(Guid id, [FromBody] Workflow workflow)
    {
        var existing = _workflows.FirstOrDefault(w => w.Id == id);
        if (existing == null)
            return NotFound();

        existing.Name = workflow.Name;
        existing.Description = workflow.Description;
        existing.Nodes = workflow.Nodes;
        existing.Connections = workflow.Connections;
        existing.IsActive = workflow.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        return Ok(existing);
    }

    [HttpDelete("{id}")]
    public ActionResult Delete(Guid id)
    {
        var workflow = _workflows.FirstOrDefault(w => w.Id == id);
        if (workflow == null)
            return NotFound();

        _workflows.Remove(workflow);
        return NoContent();
    }

    [HttpPost("{id}/execute")]
    public async Task<ActionResult> Execute(Guid id)
    {
        var workflow = _workflows.FirstOrDefault(w => w.Id == id);
        if (workflow == null)
            return NotFound();

        // TODO: Implement actual workflow execution
        // For now, simulate execution
        await Task.Delay(100);

        return Ok(new { message = "Workflow execution started", workflowId = id });
    }

    [HttpPost("{id}/activate")]
    public ActionResult Activate(Guid id)
    {
        var workflow = _workflows.FirstOrDefault(w => w.Id == id);
        if (workflow == null)
            return NotFound();

        workflow.IsActive = true;
        workflow.UpdatedAt = DateTime.UtcNow;

        return Ok(workflow);
    }

    [HttpPost("{id}/deactivate")]
    public ActionResult Deactivate(Guid id)
    {
        var workflow = _workflows.FirstOrDefault(w => w.Id == id);
        if (workflow == null)
            return NotFound();

        workflow.IsActive = false;
        workflow.UpdatedAt = DateTime.UtcNow;

        return Ok(workflow);
    }
}

