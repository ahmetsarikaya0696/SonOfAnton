using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Core.Identity;
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
                      ?? principal.FindFirst("oid")?.Value
                      ?? principal.FindFirst("preferred_username")?.Value
                      ?? principal.FindFirst("sid")?.Value;

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
                
                // Varsayılan workspace oluştur
                var workspace = new WorkspaceEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Benim Workspace'im",
                    Description = "Otomatik oluşturulan varsayılan workspace",
                    TenantId = tenantId,
                    OwnerUserId = newUser.Id,
                    IsDefault = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.Workspaces.Add(workspace);

                // Kendini üye olarak ekle
                var member = new WorkspaceMemberEntity
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspace.Id,
                    UserId = newUser.Id,
                    Role = WorkspaceMemberRole.Owner,
                    JoinedAt = DateTime.UtcNow
                };
                dbContext.WorkspaceMembers.Add(member);

                // Kota tanımla
                var quota = new UserQuotaEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = newUser.Id,
                    MaxWorkflows = 10,
                    MaxExecutionsPerMonth = 1000,
                    CurrentMonthExecutions = 0,
                    CurrentMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.UserQuotas.Add(quota);

                await dbContext.SaveChangesAsync();

                // Rollerini ata
                var externalRoles = principal.FindAll("roles").Select(c => c.Value).ToList();
                if (principal.IsInRole("admin")) externalRoles.Add("Admin");
                if (!externalRoles.Any()) externalRoles.Add("User");

                // Mevcut rolleri bul
                var tenantRoles = await dbContext.TenantRoles
                    .Where(r => r.TenantId == tenantId && externalRoles.Contains(r.ExternalRoleName))
                    .ToListAsync();

                // Tenant'ta hiç rol yoksa ve 'default' ise temel rolleri oluştur
                if (!tenantRoles.Any() && (tenantId == "default" || tenantId == "SonOfAnton"))
                {
                    var userRole = new TenantRoleEntity
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ExternalRoleName = "User",
                        Description = "Varsayılan kullanıcı rolü",
                        MappedPermissions = [
                            Permissions.WorkflowsRead, Permissions.WorkflowsCreate, Permissions.WorkflowsEdit, Permissions.WorkflowsExecute,
                            Permissions.WorkspacesRead, Permissions.WorkspacesCreate, Permissions.WorkspacesManage,
                            Permissions.FoldersRead, Permissions.FoldersManage,
                            Permissions.ApiKeysRead, Permissions.ApiKeysManage,
                            Permissions.ChatUse, Permissions.SearchExecute,
                            Permissions.AgentsRead, Permissions.ExecutionsRead,
                            Permissions.WebhooksRead, Permissions.WebhooksManage
                        ]
                    };
                    var adminRole = new TenantRoleEntity
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ExternalRoleName = "Admin",
                        Description = "Yönetici rolü",
                        MappedPermissions = Permissions.All.ToArray()
                    };
                    dbContext.TenantRoles.AddRange(userRole, adminRole);
                    await dbContext.SaveChangesAsync();
                    
                    // Kullanıcının external rollerine göre eşleştir
                    tenantRoles = new List<TenantRoleEntity>();
                    if (externalRoles.Contains("Admin")) tenantRoles.Add(adminRole);
                    // Her kullanıcıya en az User rolünü ver
                    tenantRoles.Add(userRole);
                }
                // Eğer tenantRoles hâlâ boşsa (farklı tenant ama rol eşleşmesi yok), en azından "User" rolünü bul ve ata
                else if (!tenantRoles.Any())
                {
                    var fallbackUserRole = await dbContext.TenantRoles
                        .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.ExternalRoleName == "User");
                    if (fallbackUserRole != null)
                    {
                        tenantRoles = new List<TenantRoleEntity> { fallbackUserRole };
                        _logger.LogInformation("JIT: Fallback olarak 'User' rolü atanıyor (ExternalId: {ExternalId})", externalId);
                    }
                }

                foreach (var tr in tenantRoles)
                {
                    dbContext.UserTenantRoles.Add(new UserTenantRoleEntity
                    {
                        UserId = newUser.Id,
                        TenantRoleId = tr.Id,
                        AssignedAt = DateTime.UtcNow
                    });
                }

                await dbContext.SaveChangesAsync();

                _logger.LogInformation(
                    "JIT Provisioning: Yeni kullanıcı, workspace, kota ve {Count} rol oluşturuldu/atandı. UserId: {UserId}, Email: {Email}, Tenant: {TenantId}",
                    tenantRoles.Count, newUser.Id, email, tenantId);
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
