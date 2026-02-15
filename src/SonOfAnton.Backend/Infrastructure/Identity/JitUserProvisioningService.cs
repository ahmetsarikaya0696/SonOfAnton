using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Entities;

namespace SonOfAnton.Backend.Infrastructure.Identity;

/// <summary>
/// Just-in-Time (JIT) User Provisioning servisi.
/// OIDC token doğrulandığında çağrılır. 
/// Kullanıcı veritabanında yoksa otomatik olarak oluşturulur.
/// </summary>
public class JitUserProvisioningService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JitUserProvisioningService> _logger;

    public JitUserProvisioningService(
        IServiceProvider serviceProvider,
        ILogger<JitUserProvisioningService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Kullanıcıyı doğrulanmış token claim'lerinden provizyon eder.
    /// Yoksa oluşturur, varsa son giriş zamanını günceller.
    /// </summary>
    public async Task ProvisionUserAsync(ClaimsPrincipal principal, string tenantId)
    {
        var externalId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? principal.FindFirst("sub")?.Value
                      ?? principal.FindFirst("oid")?.Value;

        if (string.IsNullOrEmpty(externalId))
        {
            _logger.LogWarning("JIT Provisioning: Token'da ExternalIdentityId bulunamadı, işlem atlanıyor.");
            return;
        }

        var email = principal.FindFirst(ClaimTypes.Email)?.Value
                 ?? principal.FindFirst("email")?.Value
                 ?? principal.FindFirst("preferred_username")?.Value
                 ?? $"{externalId}@unknown";

        var displayName = principal.FindFirst(ClaimTypes.Name)?.Value
                       ?? principal.FindFirst("name")?.Value
                       ?? principal.FindFirst(ClaimTypes.GivenName)?.Value;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

            // Tenant var mı kontrol et, yoksa varsayılan oluştur
            var tenant = await dbContext.Tenants.FindAsync(tenantId);
            if (tenant is null)
            {
                tenant = new TenantEntity
                {
                    Id = tenantId,
                    Name = tenantId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                dbContext.Tenants.Add(tenant);
                _logger.LogInformation("Yeni tenant oluşturuldu: {TenantId}", tenantId);
            }

            // Kullanıcı var mı?
            var existingUser = await dbContext.Users
                .FirstOrDefaultAsync(u => u.ExternalIdentityId == externalId);

            if (existingUser is null)
            {
                // Yeni kullanıcı oluştur
                var newUser = new UserEntity
                {
                    Id = Guid.NewGuid(),
                    ExternalIdentityId = externalId,
                    Email = email,
                    DisplayName = displayName,
                    TenantId = tenantId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                };

                dbContext.Users.Add(newUser);
                await dbContext.SaveChangesAsync();

                _logger.LogInformation(
                    "JIT Provisioning: Yeni kullanıcı oluşturuldu. UserId: {UserId}, Email: {Email}, Tenant: {TenantId}",
                    newUser.Id, email, tenantId);
            }
            else
            {
                // Son giriş zamanını güncelle
                existingUser.LastLoginAt = DateTime.UtcNow;

                // E-posta veya isim değişmişse güncelle
                if (!string.IsNullOrEmpty(email) && existingUser.Email != email)
                    existingUser.Email = email;

                if (!string.IsNullOrEmpty(displayName) && existingUser.DisplayName != displayName)
                    existingUser.DisplayName = displayName;

                await dbContext.SaveChangesAsync();

                _logger.LogDebug(
                    "JIT Provisioning: Mevcut kullanıcı güncellendi. UserId: {UserId}, Tenant: {TenantId}",
                    existingUser.Id, tenantId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JIT Provisioning sırasında hata oluştu (ExternalId: {ExternalId})", externalId);
        }
    }
}
