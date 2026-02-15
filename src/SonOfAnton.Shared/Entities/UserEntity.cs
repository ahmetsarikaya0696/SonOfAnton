namespace SonOfAnton.Shared.Entities;

/// <summary>
/// Kullanıcı domain modeli — şifre tutmaz.
/// Dış kimlik sağlayıcısındaki benzersiz kimlik (ExternalIdentityId) üzerinden eşleştirilir.
/// </summary>
public class UserEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Dış kimlik sağlayıcısındaki benzersiz kimlik (OIDC sub/oid claim)
    /// </summary>
    public string ExternalIdentityId { get; set; } = string.Empty;

    /// <summary>
    /// Kullanıcının e-posta adresi
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Kullanıcının görünen adı
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Kullanıcının ait olduğu tenant kimliği
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Kullanıcı aktif mi? SCIM deactivation için kullanılır.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    // ─── Navigation ─────────────────────────────────────────────
    public TenantEntity Tenant { get; set; } = null!;
    public ICollection<UserTenantRoleEntity> UserRoles { get; set; } = [];
}
