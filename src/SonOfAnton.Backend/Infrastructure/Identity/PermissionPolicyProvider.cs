using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using SonOfAnton.Shared.Core.Identity;

namespace SonOfAnton.Backend.Infrastructure.Identity;

/// <summary>
/// Dinamik authorization policy provider.
/// Her izin string'i için otomatik olarak policy oluşturur.
/// 
/// Kullanım: [Authorize(Policy = "Workflows.Execute")]
/// → Bu otomatik olarak "Workflows.Execute" permission claim'i gerektiren bir policy üretir.
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // Tanımlı bir permission ise dinamik policy oluştur
        if (Permissions.All.Contains(policyName))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        // Bilinmeyen policy → fallback provider'a delege et
        return _fallbackProvider.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _fallbackProvider.GetDefaultPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _fallbackProvider.GetFallbackPolicyAsync();
    }
}
