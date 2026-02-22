using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.Engine.Handlers;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Controllers;

/// <summary>
/// Özel node tanımları yönetimi.
/// Kullanıcılar kendi node tiplerini tanımlayabilir ve yönetebilir.
/// Tüm işlemler tenant kapsamındadır.
/// </summary>
[ApiController]
[Route("api/custom-nodes")]
[Produces("application/json")]
public class CustomNodesController : ControllerBase
{
    private readonly CustomNodeService _customNodeService;
    private readonly CustomNodeHandler _customNodeHandler;
    private readonly ILogger<CustomNodesController> _logger;

    public CustomNodesController(
        CustomNodeService customNodeService,
        CustomNodeHandler customNodeHandler,
        ILogger<CustomNodesController> logger)
    {
        _customNodeService = customNodeService;
        _customNodeHandler = customNodeHandler;
        _logger = logger;
    }

    /// <summary>
    /// Tenant'a ait tüm özel node tanımlarını listele.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CustomNodeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CustomNodeDto>>> GetAll()
    {
        var nodes = await _customNodeService.GetByTenantAsync();
        return Ok(nodes);
    }

    /// <summary>
    /// Belirli bir özel node tanımının detayını getir.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomNodeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomNodeDto>> Get(Guid id)
    {
        var node = await _customNodeService.GetByIdAsync(id);
        if (node == null)
            return NotFound();
        return Ok(node);
    }

    /// <summary>
    /// Yeni özel node tanımı oluştur.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomNodeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomNodeDto>> Create([FromBody] CreateCustomNodeDto dto)
    {
        try
        {
            var result = await _customNodeService.CreateAsync(dto);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Özel node tanımını güncelle.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomNodeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomNodeDto>> Update(Guid id, [FromBody] CreateCustomNodeDto dto)
    {
        try
        {
            var result = await _customNodeService.UpdateAsync(id, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Özel node tanımını sil.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _customNodeService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Özel node script'ini test et.
    /// </summary>
    [HttpPost("{id:guid}/test")]
    [ProducesResponseType(typeof(TestCustomNodeResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestCustomNodeResultDto>> Test(Guid id, [FromBody] TestCustomNodeDto dto)
    {
        var tenantId = HttpContext.User.FindFirst("tenant_id")?.Value ?? "default";
        var entity = await _customNodeService.GetEntityByTypeAsync(tenantId, "");

        // ID ile doğrudan entity al
        var node = await _customNodeService.GetByIdAsync(id);
        if (node == null)
            return NotFound();

        var entityById = await _customNodeService.GetEntityByTypeAsync(
            node.TenantId, node.Type);
        if (entityById == null)
            return NotFound();

        var result = await _customNodeHandler.TestExecuteAsync(entityById, dto);
        return Ok(result);
    }
}
