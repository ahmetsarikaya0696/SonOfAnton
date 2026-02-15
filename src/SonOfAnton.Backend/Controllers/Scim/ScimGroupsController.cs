using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Controllers.Scim;

/// <summary>
/// SCIM 2.0 Groups endpoint (RFC 7644).
/// Kurumun kimlik sağlayıcısından gelen grup senkronizasyonu.
/// </summary>
[ApiController]
[Route("scim/v2/[controller]")]
[Authorize(Policy = Permissions.UsersManage)]
[Produces("application/scim+json")]
public class GroupsController : ControllerBase
{
    private readonly ScimService _scimService;

    public GroupsController(ScimService scimService)
    {
        _scimService = scimService;
    }

    private string GetTenantId() =>
        HttpContext.Items["TenantId"] as string ?? "default";

    /// <summary>SCIM: Grup listesi</summary>
    [HttpGet]
    public async Task<IActionResult> ListGroups(
        [FromQuery] int startIndex = 1,
        [FromQuery] int count = 100)
    {
        var result = await _scimService.ListGroupsAsync(GetTenantId(), startIndex, count);
        return Ok(result);
    }

    /// <summary>SCIM: Tek grup getir</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetGroup(Guid id)
    {
        var result = await _scimService.GetGroupAsync(GetTenantId(), id);
        return result is not null
            ? Ok(result)
            : NotFound(new ScimError { Status = "404", Detail = "Group not found" });
    }

    /// <summary>SCIM: Grup oluştur</summary>
    [HttpPost]
    public async Task<IActionResult> CreateGroup([FromBody] ScimGroup scimGroup)
    {
        var result = await _scimService.CreateGroupAsync(GetTenantId(), scimGroup);
        return Created($"/scim/v2/Groups/{result.Id}", result);
    }

    /// <summary>SCIM: Grup sil</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteGroup(Guid id)
    {
        var success = await _scimService.DeleteGroupAsync(GetTenantId(), id);
        return success
            ? NoContent()
            : NotFound(new ScimError { Status = "404", Detail = "Group not found" });
    }
}
