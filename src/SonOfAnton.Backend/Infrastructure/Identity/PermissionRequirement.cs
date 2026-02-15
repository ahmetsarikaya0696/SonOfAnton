using Microsoft.AspNetCore.Authorization;

namespace SonOfAnton.Backend.Infrastructure.Identity;

/// <summary>
/// Permission bazlı yetkilendirme gereksinimi.
/// Belirli bir permission claim'inin mevcut olmasını gerektirir.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }
}
