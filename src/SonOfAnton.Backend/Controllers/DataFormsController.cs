using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.Services;
using SonOfAnton.Backend.Services.DataStore;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.Models;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/data-forms")]
public class DataFormsController : ControllerBase
{
    private readonly IDataFormService _dataFormService;

    public DataFormsController(
        IDataFormService dataFormService)
    {
        _dataFormService = dataFormService;
    }

    private string GetTenantId() => Request.Headers["X-Tenant-ID"].FirstOrDefault() ?? "default";

    // ─── Private/Auth Management Endpoints ────────────────────
    
    [HttpGet("table/{tableId}")]
    [Authorize]
    public async Task<IActionResult> GetFormsForTable(Guid tableId)
    {
        var tenantId = GetTenantId();
        var forms = await _dataFormService.GetFormsForTableAsync(tableId, tenantId);
        return Ok(forms);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetForm(Guid id)
    {
        var tenantId = GetTenantId();
        var form = await _dataFormService.GetFormAsync(id, tenantId);
        if (form == null) return NotFound();
        return Ok(form);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateForm([FromBody] DataFormCreateDto dto, [FromQuery] Guid? workspaceId)
    {
        var tenantId = GetTenantId();
        var form = await _dataFormService.CreateFormAsync(dto, tenantId, workspaceId);
        return CreatedAtAction(nameof(GetForm), new { id = form.Id }, form);
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateForm(Guid id, [FromBody] DataFormUpdateDto dto)
    {
        var tenantId = GetTenantId();
        try
        {
            var form = await _dataFormService.UpdateFormAsync(id, dto, tenantId);
            return Ok(form);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteForm(Guid id)
    {
        var tenantId = GetTenantId();
        var success = await _dataFormService.DeleteFormAsync(id, tenantId);
        if (!success) return NotFound();
        return NoContent();
    }

    // ─── Public Endpoints (No Auth Required) ──────────────────
    
    [HttpGet("public/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicForm(string slug)
    {
        var form = await _dataFormService.GetFormBySlugAsync(slug);
        if (form == null) return NotFound("Form not found or inactive.");
        return Ok(form);
    }

    [HttpPost("public/{slug}/submit")]
    [AllowAnonymous]
    public async Task<IActionResult> SubmitPublicForm(string slug, [FromBody] DataFormSubmitDto dto)
    {
        var success = await _dataFormService.SubmitFormAsync(slug, dto);
        if (!success) return NotFound("Form not found or inactive.");
        return Ok(new { success = true, timestamp = DateTime.UtcNow });
    }
}
