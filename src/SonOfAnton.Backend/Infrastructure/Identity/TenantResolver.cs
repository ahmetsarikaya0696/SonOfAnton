using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Core.Identity;

namespace SonOfAnton.Backend.Infrastructure.Identity;

/// <summary>
/// Subdomain veya X-Tenant-ID header üzerinden tenant bilgisini çözümler.
/// Veritabanından tenant OIDC konfigürasyonunu yükler.
/// </summary>
public class TenantResolver : ITenantResolver
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantResolver> _logger;

    public TenantResolver(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<TenantResolver> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TenantInfo?> ResolveAsync(string? host, string? tenantHeader)
    {
        var tenantId = ResolveTenantId(host, tenantHeader);
        if (string.IsNullOrEmpty(tenantId))
            return null;

        // Veritabanından tenant OIDC bilgilerini yükle
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);

        if (tenant is null)
        {
            _logger.LogWarning("Tenant bulunamadı veya pasif: {TenantId}", tenantId);
            return null;
        }

        return new TenantInfo(
            tenant.Id,
            tenant.Name,
            tenant.OidcAuthority,
            tenant.OidcClientId,
            tenant.OidcClientSecret);
    }

    private string? ResolveTenantId(string? host, string? tenantHeader)
    {
        var resolveBy = _configuration.GetValue<string>("MultiTenancy:ResolveBy") ?? "Header";

        return resolveBy.ToLowerInvariant() switch
        {
            "subdomain" => ResolveFromSubdomain(host),
            "header" => ResolveFromHeader(tenantHeader),
            _ => ResolveFromHeader(tenantHeader) ?? ResolveFromSubdomain(host)
        };
    }

    private static string? ResolveFromSubdomain(string? host)
    {
        if (string.IsNullOrEmpty(host)) return null;

        // kurumA.uygulama.com → "kurumA"
        var parts = host.Split('.');
        return parts.Length >= 3 ? parts[0] : null;
    }

    private static string? ResolveFromHeader(string? tenantHeader)
    {
        return string.IsNullOrWhiteSpace(tenantHeader) ? null : tenantHeader.Trim();
    }
}
