namespace SonOfAnton.Shared.Entities;

/// <summary>
/// Kullanıcı ↔ Tenant Rol ilişkisi (many-to-many).
/// Bir kullanıcı birden fazla role sahip olabilir, bir rol birden fazla kullanıcıya atanabilir.
/// </summary>
public class UserTenantRoleEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid TenantRoleId { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // ─── Navigation ─────────────────────────────────────────────
    public UserEntity User { get; set; } = null!;
    public TenantRoleEntity TenantRole { get; set; } = null!;
}
