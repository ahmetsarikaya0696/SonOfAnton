namespace SonOfAnton.Shared.Entities;

/// <summary>
/// Workspace rolü — bir kullanıcının workspace içindeki yetki seviyesi
/// </summary>
public enum WorkspaceMemberRole
{
    /// <summary>Sadece okuma yetkisi</summary>
    Viewer = 0,
    /// <summary>Okuma + düzenleme yetkisi</summary>
    Editor = 1,
    /// <summary>Okuma + düzenleme + üye yönetimi</summary>
    Admin = 2,
    /// <summary>Tam yetki (workspace sahibi)</summary>
    Owner = 3
}

/// <summary>
/// Workspace (Çalışma Alanı) entity.
/// Make.com benzeri, ekipler için proje/klasör/workflow organizasyonu sağlar.
/// Her workspace bir tenant'a aittir ve birden fazla üyeye sahip olabilir.
/// </summary>
public class WorkspaceEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Workspace adı</summary>
    public string Name { get; set; } = "Yeni Çalışma Alanı";

    /// <summary>Workspace açıklaması</summary>
    public string? Description { get; set; }

    /// <summary>Ait olduğu tenant</summary>
    public string? TenantId { get; set; }

    /// <summary>Workspace'i oluşturan kullanıcı</summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>Workspace ikonu (feather icon adı)</summary>
    public string? Icon { get; set; }

    /// <summary>Workspace rengi (hex)</summary>
    public string? Color { get; set; }

    /// <summary>Varsayılan workspace mi? (her tenant'ın bir varsayılan workspace'i olur)</summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ─── Navigation ─────────────────────────────────────────────
    public TenantEntity? Tenant { get; set; }
    public UserEntity? OwnerUser { get; set; }
    public ICollection<WorkspaceMemberEntity> Members { get; set; } = [];
    public ICollection<FolderEntity> Folders { get; set; } = [];
    public ICollection<WorkflowEntity> Workflows { get; set; } = [];
}

/// <summary>
/// Workspace üyeliği — kullanıcı ↔ workspace ilişkisi.
/// Her üyenin workspace içinde bir rolü vardır (Owner/Admin/Editor/Viewer).
/// </summary>
public class WorkspaceMemberEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkspaceId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Kullanıcının workspace içindeki rolü</summary>
    public WorkspaceMemberRole Role { get; set; } = WorkspaceMemberRole.Viewer;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // ─── Navigation ─────────────────────────────────────────────
    public WorkspaceEntity Workspace { get; set; } = null!;
    public UserEntity User { get; set; } = null!;
}
