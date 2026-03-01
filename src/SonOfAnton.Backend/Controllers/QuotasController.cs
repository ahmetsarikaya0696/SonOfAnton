using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.Entities;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QuotasController : ControllerBase
{
    private readonly IQuotaService _quotaService;

    public QuotasController(IQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    private Guid GetUserId()
    {
        var idClaim = User.Claims.FirstOrDefault(c => c.Type == "uid" || c.Type == "sub" || c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        return Guid.TryParse(idClaim, out var id) ? id : Guid.Empty;
    }

    [HttpGet]
    public async Task<ActionResult<UserQuotaEntity>> GetMyQuota()
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var quota = await _quotaService.GetUserQuotaAsync(userId);
        return Ok(quota);
    }
}
