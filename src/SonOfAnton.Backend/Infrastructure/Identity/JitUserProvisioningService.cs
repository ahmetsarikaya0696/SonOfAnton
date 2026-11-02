using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Entities;

namespace SonOfAnton.Backend.Infrastructure.Identity;

public sealed class JitUserProvisioningService(SonOfAntonDbContext db)
{
    public async Task ProvisionUserAsync(ClaimsPrincipal principal, string tenantId)
    {
        var subject = principal.FindFirst("sub")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("Token subject is required.");
        var email = principal.FindFirst("email")?.Value ?? principal.FindFirst("preferred_username")?.Value ?? subject;
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({tenantId}))");
        var user = await db.Users.FirstOrDefaultAsync(u => u.ExternalIdentityId == subject);
        if (user is not null)
        {
            if (!user.IsActive || user.TenantId != tenantId) throw new InvalidOperationException("Inactive user or tenant mismatch.");
            user.LastLoginAt = DateTime.UtcNow;
            if (!await db.Tenants.AnyAsync(t => t.Id == tenantId && t.IsActive)) throw new InvalidOperationException("Inactive tenant.");
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return;
        }
        // A transaction prevents partial users without a default workspace on provisioning failure.
        var tenant = await db.Tenants.FindAsync(tenantId);
        if (tenant is null)
        {
            if (tenantId != "default") throw new InvalidOperationException("Tenant must be provisioned by an administrator.");
            db.Tenants.Add(new TenantEntity { Id = tenantId, Name = tenantId, IsActive = true });
        }
        else if (!tenant.IsActive) throw new InvalidOperationException("Inactive tenant.");
        user = new UserEntity { ExternalIdentityId = subject, Email = email, DisplayName = principal.FindFirst("name")?.Value,
            TenantId = tenantId, IsActive = true, LastLoginAt = DateTime.UtcNow };
        db.Users.Add(user);
        var workspace = new WorkspaceEntity { Name = $"Benim Workspace'im {user.Id.ToString()[..8]}", TenantId = tenantId, OwnerUserId = user.Id, IsDefault = true };
        db.Workspaces.Add(workspace);
        db.WorkspaceMembers.Add(new WorkspaceMemberEntity { WorkspaceId = workspace.Id, UserId = user.Id, Role = WorkspaceMemberRole.Owner });
        db.UserQuotas.Add(new UserQuotaEntity { UserId = user.Id, MaxWorkflows = 10, MaxExecutionsPerMonth = 1000,
            CurrentMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc) });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
