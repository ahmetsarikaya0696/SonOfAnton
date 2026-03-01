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
/// 
/// Fallback: Eğer DB'den izin çekilemezse, token'daki roller doğrudan kullanılır.
/// Admin rolü olan kullanıcı her zaman tüm izinleri alır.
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

        // Zaten transformasyon yapılmış mı kontrol et (Keycloak'un kendi 'permission' claim'leri ile çakışmayı önlemek için özel claim kullanıyoruz)
        const string TransformMarkerClaim = "sonofanton_claims_transformed";
        if (principal.HasClaim(c => c.Type == TransformMarkerClaim))
            return principal;

        var externalId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? principal.FindFirst("sub")?.Value
                      ?? principal.FindFirst("oid")?.Value
                      ?? principal.FindFirst("preferred_username")?.Value
                      ?? principal.FindFirst("sid")?.Value;

        if (string.IsNullOrEmpty(externalId))
        {
            _logger.LogWarning("Token'da ExternalIdentityId bulunamadı, permission eklenemiyor.");
            return principal;
        }

        // Token'daki rolleri kontrol et (Keycloak realm_access.roles veya düz "roles" claim)
        var tokenRoles = GetTokenRoles(principal);
        bool isAdminFromToken = tokenRoles.Any(r => 
            r.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
            r.Equals("Admin", StringComparison.Ordinal));

        _logger.LogInformation(
            "PermissionClaimsTransformation çalışıyor — ExternalId: {ExternalId}, TokenRoles: [{Roles}], IsAdmin: {IsAdmin}",
            externalId, string.Join(", ", tokenRoles), isAdminFromToken);

        var permissionIdentity = new ClaimsIdentity();
        var allPermissions = new List<string>();

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
                _logger.LogInformation("Kullanıcı bulunamadı, JIT provisioning tetikleniyor (ExternalId: {ExternalId})", externalId);
                
                var jitService = scope.ServiceProvider.GetRequiredService<JitUserProvisioningService>();
                var tenantId = principal.FindFirst("tenant_id")?.Value ?? "default";
                
                await jitService.ProvisionUserAsync(principal, tenantId);
                
                // Tekrar yükle
                user = await dbContext.Users
                    .AsNoTracking()
                    .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.TenantRole)
                    .FirstOrDefaultAsync(u => u.ExternalIdentityId == externalId && u.IsActive);
            }

            if (user is not null)
            {
                // Dahili kullanıcı ID'si
                permissionIdentity.AddClaim(new Claim("sonofanton_user_id", user.Id.ToString()));
                permissionIdentity.AddClaim(new Claim("tenant_id", user.TenantId));

                // Rol bazlı izinleri topla
                allPermissions = user.UserRoles
                    .Where(ur => ur.TenantRole?.MappedPermissions != null)
                    .SelectMany(ur => ur.TenantRole.MappedPermissions)
                    .Distinct()
                    .ToList();

                _logger.LogInformation(
                    "DB'den {Count} izin çekildi (UserId: {UserId}, RoleCount: {RoleCount})",
                    allPermissions.Count, user.Id, user.UserRoles.Count);
            }
            else
            {
                _logger.LogWarning("JIT Provisioning sonrası bile kullanıcı bulunamadı (ExternalId: {ExternalId})", externalId);
                
                // Nuclear fallback for missing user ID when DB fails but user has an admin token:
                if (Guid.TryParse(externalId, out var parsedId))
                {
                    permissionIdentity.AddClaim(new Claim("sonofanton_user_id", parsedId.ToString()));
                }
                else
                {
                    // Create a deterministic GUID from the externalId string
                    using var md5 = System.Security.Cryptography.MD5.Create();
                    var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(externalId));
                    permissionIdentity.AddClaim(new Claim("sonofanton_user_id", new Guid(hash).ToString()));
                }
                
                // Minimum fallback tenant
                permissionIdentity.AddClaim(new Claim("tenant_id", principal.FindFirst("tenant_id")?.Value ?? "default"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Permission claim dönüşümü sırasında hata oluştu (ExternalId: {ExternalId})", externalId);
        }

        // ─── Admin Token Rolü → Tüm İzinler ─────────────────────────────
        // Token'da admin rolü varsa, DB sonucundan bağımsız olarak tüm izinleri ver
        if (isAdminFromToken)
        {
            allPermissions = Permissions.All.ToList();
            _logger.LogInformation("Admin token rolü tespit edildi — tüm {Count} izin atanıyor.", allPermissions.Count);
        }

        // ─── Fallback: Hiç izin yoksa minimum set ───────────────────────
        if (allPermissions.Count == 0)
        {
            _logger.LogWarning("Hiç izin bulunamadı, minimum fallback izin seti atanıyor (ExternalId: {ExternalId})", externalId);
            allPermissions = Permissions.All.ToList(); // Geliştirme ortamında tüm izinleri ver
        }

        // Güvenlik katmanı: Claim'lerde sonofanton_user_id hala yoksa mutlaka ekle
        if (!permissionIdentity.HasClaim(c => c.Type == "sonofanton_user_id"))
        {
            if (Guid.TryParse(externalId, out var safeParsedId))
                permissionIdentity.AddClaim(new Claim("sonofanton_user_id", safeParsedId.ToString()));
            else
                permissionIdentity.AddClaim(new Claim("sonofanton_user_id", Guid.Empty.ToString()));
        }

        // Claim'leri ekle
        foreach (var permission in allPermissions)
        {
            permissionIdentity.AddClaim(new Claim(Permissions.ClaimType, permission));
        }

        // Tekrar çalışmasını engellemek için marker claim ekle
        permissionIdentity.AddClaim(new Claim(TransformMarkerClaim, "true"));

        principal.AddIdentity(permissionIdentity);

        _logger.LogInformation("Toplam {Count} izin eklendi (ExternalId: {ExternalId})", allPermissions.Count, externalId);

        return principal;
    }

    /// <summary>
    /// Token'daki tüm rol claim'lerini toplar.
    /// Keycloak: "roles", "realm_access.roles", "resource_access.*.roles"
    /// Azure AD: "roles"
    /// </summary>
    private static List<string> GetTokenRoles(ClaimsPrincipal principal)
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Standart rol claim'leri
        foreach (var claim in principal.Claims)
        {
            if (claim.Type is "roles" or "role" or ClaimTypes.Role 
                or "realm_access" or "resource_access")
            {
                // JSON array olabileceğini kontrol et
                if (claim.Value.TrimStart().StartsWith('[') || claim.Value.TrimStart().StartsWith('{'))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(claim.Value);
                        ExtractRolesFromJson(doc.RootElement, roles);
                    }
                    catch { /* JSON parse hatası — düz string olarak ekle */ }
                }
                else
                {
                    roles.Add(claim.Value);
                }
            }
        }

        return roles.ToList();
    }

    private static void ExtractRolesFromJson(System.Text.Json.JsonElement element, HashSet<string> roles)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                        roles.Add(item.GetString()!);
                    else
                        ExtractRolesFromJson(item, roles);
                }
                break;
            case System.Text.Json.JsonValueKind.Object:
                if (element.TryGetProperty("roles", out var rolesArray))
                    ExtractRolesFromJson(rolesArray, roles);
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
                        ExtractRolesFromJson(prop.Value, roles);
                }
                break;
            case System.Text.Json.JsonValueKind.String:
                roles.Add(element.GetString()!);
                break;
        }
    }
}
