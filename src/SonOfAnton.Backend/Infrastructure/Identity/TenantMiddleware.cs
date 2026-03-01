using SonOfAnton.Shared.Core.Identity;

namespace SonOfAnton.Backend.Infrastructure.Identity;

/// <summary>
/// Her HTTP isteğinde tenant bilgisini çözümleyip HttpContext.Items'a yerleştiren middleware.
/// Authentication middleware'inden ÖNCE çalışmalıdır.
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context, ITenantResolver tenantResolver, IConfiguration configuration)
    {
        var host = context.Request.Host.Host;
        var tenantHeader = context.Request.Headers["X-Tenant-ID"].FirstOrDefault();

        var tenantInfo = await tenantResolver.ResolveAsync(host, tenantHeader);

        if (tenantInfo is not null)
        {
            context.Items["TenantInfo"] = tenantInfo;
            context.Items["TenantId"] = tenantInfo.TenantId;
            _logger.LogDebug("Tenant çözümlendi: {TenantId}", tenantInfo.TenantId);
        }
        else
        {
            // Varsayılan tenant
            var defaultTenantId = configuration.GetValue<string>("MultiTenancy:DefaultTenantId") ?? "default";
            context.Items["TenantId"] = defaultTenantId;
            _logger.LogDebug("Varsayılan tenant kullanılıyor: {TenantId}", defaultTenantId);
        }

        await _next(context);
    }
}

/// <summary>
/// TenantMiddleware extension method
/// </summary>
public static class TenantMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantMiddleware>();
    }
}
