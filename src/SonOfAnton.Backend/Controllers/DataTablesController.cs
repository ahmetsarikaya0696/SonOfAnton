using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.Services;
using SonOfAnton.Backend.Services.DataStore;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.Models;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/data-tables")]
[Authorize] // Requires auth to manage tables
public class DataTablesController : ControllerBase
{
    private readonly IDataTableService _dataTableService;

    public DataTablesController(
        IDataTableService dataTableService)
    {
        _dataTableService = dataTableService;
    }

    private string GetTenantId() => Request.Headers["X-Tenant-ID"].FirstOrDefault() ?? "default";

    [HttpGet]
    public async Task<IActionResult> GetTables([FromQuery] Guid? workspaceId)
    {
        var tenantId = GetTenantId();
        var tables = await _dataTableService.GetTablesAsync(tenantId, workspaceId);
        return Ok(tables);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTable(Guid id)
    {
        var tenantId = GetTenantId();
        var table = await _dataTableService.GetTableAsync(id, tenantId);
        if (table == null) return NotFound();
        return Ok(table);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTable([FromBody] DataTableCreateDto dto, [FromQuery] Guid? workspaceId)
    {
        var tenantId = GetTenantId();
        var userId = GetCurrentUserId();
        
        var table = await _dataTableService.CreateTableAsync(dto, tenantId, workspaceId, userId);
        return CreatedAtAction(nameof(GetTable), new { id = table.Id }, table);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTable(Guid id, [FromBody] DataTableUpdateDto dto)
    {
        var tenantId = GetTenantId();
        try
        {
            var table = await _dataTableService.UpdateTableAsync(id, dto, tenantId);
            return Ok(table);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTable(Guid id)
    {
        var tenantId = GetTenantId();
        var success = await _dataTableService.DeleteTableAsync(id, tenantId);
        if (!success) return NotFound();
        return NoContent();
    }

    // ─── Rows ───────────────────────────────────────────────
    [HttpGet("{id}/rows")]
    public async Task<IActionResult> GetRows(Guid id)
    {
        var tenantId = GetTenantId();
        var rows = await _dataTableService.GetRowsAsync(id, tenantId);
        return Ok(rows);
    }

    [HttpPost("{id}/rows")]
    public async Task<IActionResult> AddRow(Guid id, [FromBody] DataRowCreateDto dto)
    {
        var tenantId = GetTenantId();
        try
        {
            var row = await _dataTableService.CreateRowAsync(id, dto, tenantId);
            return Ok(row);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPut("{tableId}/rows/{rowId}")]
    public async Task<IActionResult> UpdateRow(Guid tableId, Guid rowId, [FromBody] DataRowUpdateDto dto)
    {
        var tenantId = GetTenantId();
        try
        {
            // Note: tableId can visually group routes, service primarily filters by rowId and tenant.
            var row = await _dataTableService.UpdateRowAsync(rowId, dto, tenantId);
            return Ok(row);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpDelete("{tableId}/rows/{rowId}")]
    public async Task<IActionResult> DeleteRow(Guid tableId, Guid rowId)
    {
        var tenantId = GetTenantId();
        var success = await _dataTableService.DeleteRowAsync(rowId, tenantId);
        if (!success) return NotFound();
        return NoContent();
    }

    // ─── CSV Import/Export ──────────────────────────────────
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportCsv([FromQuery] Guid? workspaceId, [FromQuery] Guid? existingTableId)
    {
        var tenantId = GetTenantId();
        var userId = GetCurrentUserId();

        if (Request.Form.Files.Count == 0) return BadRequest("No files uploaded.");
        var file = Request.Form.Files[0];

        try
        {
            using var stream = file.OpenReadStream();
            var tableId = await _dataTableService.ImportCsvAsync(
                stream, 
                file.FileName, 
                tenantId, 
                workspaceId, 
                userId, 
                existingTableId);

            return Ok(new { tableId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id}/export")]
    public async Task<IActionResult> ExportCsv(Guid id)
    {
        var tenantId = GetTenantId();
        try
        {
            var stream = await _dataTableService.ExportCsvAsync(id, tenantId);
            return File(stream, "text/csv", $"TableExport_{id}.csv");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdStr, out var userId)) return userId;
        return null;
    }
}
