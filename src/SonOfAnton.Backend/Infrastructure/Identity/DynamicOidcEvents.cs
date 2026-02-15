using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using SonOfAnton.Shared.Core.Identity;

namespace SonOfAnton.Backend.Infrastructure.Identity;

/// <summary>
/// OIDC middleware event'lerini override ederek tenant'a göre Authority/ClientId parametrelerini
/// dinamik olarak değiştiren sınıf.
/// </summary>
public class DynamicOidcEvents : OpenIdConnectEvents
{
    private readonly ILogger<DynamicOidcEvents> _logger;

    public DynamicOidcEvents(ILogger<DynamicOidcEvents> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Identity Provider'a yönlendirme öncesi — tenant'a göre Authority/ClientId değiştirilir.
    /// </summary>
    public override Task RedirectToIdentityProvider(RedirectContext context)
    {
        var tenantInfo = context.HttpContext.Items["TenantInfo"] as TenantInfo;

        if (tenantInfo is not null)
        {
            if (!string.IsNullOrEmpty(tenantInfo.OidcAuthority))
            {
                context.Options.Authority = tenantInfo.OidcAuthority;
                _logger.LogInformation("OIDC Authority tenant'a göre değiştirildi: {Authority} (Tenant: {TenantId})",
                    tenantInfo.OidcAuthority, tenantInfo.TenantId);
            }

            if (!string.IsNullOrEmpty(tenantInfo.OidcClientId))
            {
                context.Options.ClientId = tenantInfo.OidcClientId;
            }

            if (!string.IsNullOrEmpty(tenantInfo.OidcClientSecret))
            {
                context.Options.ClientSecret = tenantInfo.OidcClientSecret;
            }
        }

        return base.RedirectToIdentityProvider(context);
    }

    /// <summary>
    /// Token doğrulandıktan sonra — JIT provisioning tetiklenir.
    /// </summary>
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var jitService = context.HttpContext.RequestServices.GetRequiredService<JitUserProvisioningService>();
        var tenantId = context.HttpContext.Items["TenantId"] as string ?? "default";

        if (context.Principal is not null)
        {
            await jitService.ProvisionUserAsync(context.Principal, tenantId);
        }

        await base.TokenValidated(context);
    }
}
