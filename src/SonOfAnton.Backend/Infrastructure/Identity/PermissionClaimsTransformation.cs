using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Core.Identity;

namespace SonOfAnton.Backend.Infrastructure.Identity;

/// <summary>
/// Her request'te çalışan claims dönüşüm servisi.
/// Kurumun OIDC token'ındaki rolleri, sistemin anladığı standart izinlere dönüştürür.
/// 
/// Akış:
/// 1. Token'dan sub/oid claim → Veritabanında kullanıcıyı bul
/// 2. Kullanıcının tenant rollerini çek
/// 3. Her rolün MappedPermissions dizisini topla
/// 4. ClaimsPrincipal'a "permission" claim'leri ekle
/// </summary>
public class PermissionClaimsTransformation : IClaimsTransformation
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PermissionClaimsTransformation> _logger;

    public PermissionClaimsTransformation(
        IServiceProvider serviceProvider,
        ILogger<PermissionClaimsTransformation> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not { IsAuthenticated: true })
            return principal;

        // Zaten permission claim'leri eklenmiş mi? (her request'te tekrar çalışmasını engelle)
        if (principal.HasClaim(c => c.Type == Permissions.ClaimType))
            return principal;

        var externalId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? principal.FindFirst("sub")?.Value
                      ?? principal.FindFirst("oid")?.Value;

        if (string.IsNullOrEmpty(externalId))
        {
            _logger.LogWarning("Token'da ExternalIdentityId bulunamadı, permission eklenemiyor.");
            return principal;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

            // Kullanıcıyı ve rollerini yükle
            var user = await dbContext.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.TenantRole)
                .FirstOrDefaultAsync(u => u.ExternalIdentityId == externalId && u.IsActive);

            if (user is null)
            {
                _logger.LogDebug("Kullanıcı bulunamadı (ExternalId: {ExternalId}), belki JIT provisioning henüz çalışmadı.", externalId);
                return principal;
            }

            // Yeni identity oluştur ve permission claim'lerini ekle
            var permissionIdentity = new ClaimsIdentity();

            // Dahili kullanıcı ID'si
            permissionIdentity.AddClaim(new Claim("sonofanton_user_id", user.Id.ToString()));

            // Tenant ID
            permissionIdentity.AddClaim(new Claim("tenant_id", user.TenantId));

            // Rol bazlı izinleri topla
            var allPermissions = user.UserRoles
                .SelectMany(ur => ur.TenantRole.MappedPermissions)
                .Distinct()
                .ToList();

            foreach (var permission in allPermissions)
            {
                permissionIdentity.AddClaim(new Claim(Permissions.ClaimType, permission));
            }

            principal.AddIdentity(permissionIdentity);

            _logger.LogDebug("Kullanıcıya {Count} izin eklendi (UserId: {UserId}, Tenant: {TenantId})",
                allPermissions.Count, user.Id, user.TenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Permission claim dönüşümü sırasında hata oluştu.");
        }

        return principal;
    }
}
