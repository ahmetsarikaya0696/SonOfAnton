using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Core.Identity;

namespace SonOfAnton.Backend.Infrastructure.Identity;

public sealed class PermissionClaimsTransformation(SonOfAntonDbContext db) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true || principal.Identity.AuthenticationType == ApiKeyAuthenticationOptions.DefaultScheme)
            return principal;
        var subject = principal.FindFirst("sub")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tenant = principal.FindFirst("tenant_id")?.Value ?? "default";
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.ExternalIdentityId == subject && u.TenantId == tenant && u.IsActive);
        if (user is null) return new ClaimsPrincipal(new ClaimsIdentity());
        // Remove externally supplied application claims. Only this transformation grants application access.
        var claims = principal.Claims.Where(c => c.Type is not ("permission" or "sonofanton_user_id" or "tenant_id")).ToList();
        claims.Add(new("sonofanton_user_id", user.Id.ToString()));
        claims.Add(new("tenant_id", tenant));
        var roles = ReadRoles(principal);
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        if (roles.Contains("admin")) permissions.UnionWith(Permissions.All);
        else
        {
            permissions.UnionWith([Permissions.WorkflowsRead, Permissions.ExecutionsRead, Permissions.WorkspacesRead, Permissions.FoldersRead]);
            if (roles.Contains("editor")) permissions.UnionWith([
                Permissions.WorkflowsCreate, Permissions.WorkflowsEdit, Permissions.WorkflowsDelete, Permissions.WorkflowsExecute,
                Permissions.ExecutionsCancel, Permissions.ExecutionsPause, Permissions.FoldersManage,
                Permissions.WorkspacesCreate, Permissions.WorkspacesManage, Permissions.ChatUse,
                Permissions.ApiKeysRead, Permissions.ApiKeysManage]);
            var mapped = await db.UserTenantRoles.Where(x => x.UserId == user.Id && x.TenantRole.TenantId == tenant)
                .Select(x => x.TenantRole.MappedPermissions).ToListAsync();
            foreach (var set in mapped) permissions.UnionWith(set);
        }
        claims.AddRange(permissions.Select(p => new Claim(Permissions.ClaimType, p)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, principal.Identity.AuthenticationType, "preferred_username", "roles"));
    }

    internal static HashSet<string> ReadRoles(ClaimsPrincipal principal)
    {
        var roles = principal.FindAll("roles").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var realm = principal.FindFirst("realm_access")?.Value;
        if (realm is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(realm);
                if (doc.RootElement.TryGetProperty("roles", out var values))
                    foreach (var value in values.EnumerateArray()) if (value.GetString() is { } role) roles.Add(role);
            }
            catch (JsonException) { }
        }
        return roles;
    }
}
