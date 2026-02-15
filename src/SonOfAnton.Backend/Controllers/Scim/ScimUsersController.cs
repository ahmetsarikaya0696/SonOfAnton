using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Controllers.Scim;

/// <summary>
/// SCIM 2.0 Users endpoint (RFC 7644).
/// Kurumun kimlik sağlayıcısı (Entra ID, Okta vb.) bu API'ye otomatik tetikleme gönderir.
/// </summary>
[ApiController]
[Route("scim/v2/[controller]")]
[Authorize(Policy = Permissions.UsersManage)]
[Produces("application/scim+json")]
public class UsersController : ControllerBase
{
    private readonly ScimService _scimService;

    public UsersController(ScimService scimService)
    {
        _scimService = scimService;
    }

    private string GetTenantId() =>
        HttpContext.Items["TenantId"] as string ?? "default";

    /// <summary>SCIM: Kullanıcı listesi (filter destekli)</summary>
    [HttpGet]
    public async Task<IActionResult> ListUsers(
        [FromQuery] int startIndex = 1,
        [FromQuery] int count = 100)
    {
        var result = await _scimService.ListUsersAsync(GetTenantId(), startIndex, count);
        return Ok(result);
    }

    /// <summary>SCIM: Tek kullanıcı getir</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var result = await _scimService.GetUserAsync(GetTenantId(), id);
        return result is not null
            ? Ok(result)
            : NotFound(new ScimError { Status = "404", Detail = "User not found" });
    }

    /// <summary>SCIM: Kullanıcı oluştur</summary>
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] ScimUser scimUser)
    {
        var result = await _scimService.CreateUserAsync(GetTenantId(), scimUser);
        return Created($"/scim/v2/Users/{result.Id}", result);
    }

    /// <summary>SCIM: Kullanıcı güncelle (tam)</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] ScimUser scimUser)
    {
        var result = await _scimService.UpdateUserAsync(GetTenantId(), id, scimUser);
        return result is not null
            ? Ok(result)
            : NotFound(new ScimError { Status = "404", Detail = "User not found" });
    }

    /// <summary>SCIM: Kısmi güncelleme (deactivation vb.)</summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> PatchUser(Guid id, [FromBody] ScimPatchRequest patchRequest)
    {
        var success = await _scimService.PatchUserAsync(GetTenantId(), id, patchRequest);
        return success
            ? NoContent()
            : NotFound(new ScimError { Status = "404", Detail = "User not found" });
    }

    /// <summary>SCIM: Kullanıcı sil (soft delete)</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var success = await _scimService.DeleteUserAsync(GetTenantId(), id);
        return success
            ? NoContent()
            : NotFound(new ScimError { Status = "404", Detail = "User not found" });
    }
}
