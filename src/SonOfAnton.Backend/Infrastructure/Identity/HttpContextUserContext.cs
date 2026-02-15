using System.Security.Claims;
using SonOfAnton.Shared.Core.Identity;

namespace SonOfAnton.Backend.Infrastructure.Identity;

/// <summary>
/// IUserContext implementasyonu — HttpContext.User üzerinden kullanıcı bilgisini çözümler.
/// Sadece Infrastructure/Presentation katmanında yaşar, domain bu sınıfı bilmez.
/// </summary>
public class HttpContextUserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid CurrentUserId
    {
        get
        {
            // Dahili kullanıcı ID'si — JIT provisioning sırasında eklenen claim
            var userIdClaim = User?.FindFirst("sonofanton_user_id")?.Value;
            return Guid.TryParse(userIdClaim, out var id) ? id : Guid.Empty;
        }
    }

    public string? ExternalIdentityId =>
        User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User?.FindFirst("sub")?.Value
        ?? User?.FindFirst("oid")?.Value;

    public string? TenantId =>
        User?.FindFirst("tenant_id")?.Value;

    public string? Email =>
        User?.FindFirst(ClaimTypes.Email)?.Value
        ?? User?.FindFirst("email")?.Value;

    public string? DisplayName =>
        User?.FindFirst(ClaimTypes.Name)?.Value
        ?? User?.FindFirst("name")?.Value;

    public IReadOnlyList<string> Permissions =>
        User?.Claims
            .Where(c => c.Type == Shared.Core.Identity.Permissions.ClaimType)
            .Select(c => c.Value)
            .Distinct()
            .ToList()
            .AsReadOnly()
        ?? (IReadOnlyList<string>)Array.Empty<string>();

    public bool HasPermission(string permission)
    {
        if (!IsAuthenticated) return false;

        // System.Admin her şeye erişebilir
        if (Permissions.Contains(Shared.Core.Identity.Permissions.SystemAdmin))
            return true;

        return Permissions.Contains(permission);
    }
}
