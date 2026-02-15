using Microsoft.AspNetCore.Authorization;
using SonOfAnton.Shared.Core.Identity;

namespace SonOfAnton.Backend.Infrastructure.Identity;

/// <summary>
/// Permission claim'ini kontrol eden AuthorizationHandler.
/// Kullanıcının ClaimsPrincipal'ında ilgili permission claim'i veya SystemAdmin varsa erişime izin verir.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // System.Admin her şeye erişir
        if (context.User.HasClaim(Permissions.ClaimType, Permissions.SystemAdmin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Belirli izin kontrolü
        if (context.User.HasClaim(Permissions.ClaimType, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
