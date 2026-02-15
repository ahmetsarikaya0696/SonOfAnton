namespace SonOfAnton.Shared.Entities;

/// <summary>
/// Tenant'a özel roller.
/// Kurumdan gelen dış rol adı (ExternalRoleName) sistemin anladığı izinlere (MappedPermissions) eşleştirilir.
/// Örn: "Manager" → ["Workflows.Read", "Workflows.Execute"]
/// </summary>
public class TenantRoleEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Bu rolün ait olduğu tenant
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Kurumun kendi sistemindeki rol adı (OIDC token'dan gelen)
    /// Örn: "Manager", "Supervisor", "Admin"
    /// </summary>
    public string ExternalRoleName { get; set; } = string.Empty;

    /// <summary>
    /// Bu rolün eşleştiği SonOfAnton izinleri (JSON dizisi)
    /// Örn: ["Workflows.Read", "Workflows.Execute", "Executions.Read"]
    /// </summary>
    public string[] MappedPermissions { get; set; } = [];

    /// <summary>
    /// Rol açıklaması
    /// </summary>
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ─── Navigation ─────────────────────────────────────────────
    public TenantEntity Tenant { get; set; } = null!;
    public ICollection<UserTenantRoleEntity> UserRoles { get; set; } = [];
}
