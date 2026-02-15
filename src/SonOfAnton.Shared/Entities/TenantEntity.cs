namespace SonOfAnton.Shared.Entities;

/// <summary>
/// Kurum (tenant) bilgisini tutan entity.
/// Her tenant'ın kendi OIDC konfigürasyonu olabilir. (Multi-tenant SaaS)
/// </summary>
public class TenantEntity
{
    /// <summary>
    /// Tenant kimliği (örn: "kurumA", "varsayilan")
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Kurumun görünen adı
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tenant'a özel OIDC Authority URL'i (null ise varsayılan kullanılır)
    /// </summary>
    public string? OidcAuthority { get; set; }

    /// <summary>
    /// Tenant'a özel OIDC Client ID
    /// </summary>
    public string? OidcClientId { get; set; }

    /// <summary>
    /// Tenant'a özel OIDC Client Secret (şifrelenmiş saklanmalı)
    /// </summary>
    public string? OidcClientSecret { get; set; }

    /// <summary>
    /// Tenant aktif mi?
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ─── Navigation ─────────────────────────────────────────────
    public ICollection<UserEntity> Users { get; set; } = [];
    public ICollection<TenantRoleEntity> Roles { get; set; } = [];
}
