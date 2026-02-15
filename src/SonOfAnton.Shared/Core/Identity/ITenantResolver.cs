namespace SonOfAnton.Shared.Core.Identity;

/// <summary>
/// Tenant çözümleme sonucu — OIDC konfigürasyonu dahil
/// </summary>
public record TenantInfo(
    string TenantId,
    string Name,
    string? OidcAuthority,
    string? OidcClientId,
    string? OidcClientSecret);

/// <summary>
/// Gelen HTTP isteğinden tenant bilgisini çözümleyen soyutlama.
/// Subdomain, header veya path segment üzerinden tenant belirlenir.
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// İstek bağlamından tenant bilgisini çözümler.
    /// Bulunamazsa null döner (varsayılan tenant kullanılır).
    /// </summary>
    Task<TenantInfo?> ResolveAsync(string? host, string? tenantHeader);
}
