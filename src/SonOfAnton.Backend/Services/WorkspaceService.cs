using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;

namespace SonOfAnton.Backend.Services;

/// <summary>
/// Workspace CRUD ve üye yönetimi servisi
/// </summary>
public class WorkspaceService
{
    private readonly SonOfAntonDbContext _dbContext;
    private readonly ILogger<WorkspaceService> _logger;

    public WorkspaceService(SonOfAntonDbContext dbContext, ILogger<WorkspaceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    // ─── Workspace CRUD ─────────────────────────────────────────

    /// <summary>
    /// Kullanıcının erişebildiği workspace listesini döner.
    /// Owner olduğu veya üyesi olduğu workspace'ler listelenir.
    /// </summary>
    public async Task<List<WorkspaceDto>> GetWorkspacesAsync(Guid userId, string? tenantId = null)
    {
        var query = _dbContext.Workspaces
            .Include(w => w.Members)
            .Include(w => w.OwnerUser)
            .Where(w => w.Members.Any(m => m.UserId == userId));

        if (!string.IsNullOrEmpty(tenantId))
            query = query.Where(w => w.TenantId == tenantId);

        return await query
            .OrderByDescending(w => w.IsDefault)
            .ThenBy(w => w.Name)
            .Select(w => new WorkspaceDto
            {
                Id = w.Id,
                Name = w.Name,
                Description = w.Description,
                TenantId = w.TenantId,
                OwnerUserId = w.OwnerUserId,
                OwnerDisplayName = w.OwnerUser != null ? w.OwnerUser.DisplayName : null,
                Icon = w.Icon,
                Color = w.Color,
                IsDefault = w.IsDefault,
                MemberCount = w.Members.Count,
                WorkflowCount = w.Workflows.Count,
                FolderCount = w.Folders.Count,
                CreatedAt = w.CreatedAt,
                UpdatedAt = w.UpdatedAt
            })
            .ToListAsync();
    }

    /// <summary>
    /// Tekil workspace detayını döner.
    /// </summary>
    public async Task<WorkspaceDto?> GetWorkspaceAsync(Guid workspaceId)
    {
        return await _dbContext.Workspaces
            .Include(w => w.OwnerUser)
            .Include(w => w.Members)
            .Where(w => w.Id == workspaceId)
            .Select(w => new WorkspaceDto
            {
                Id = w.Id,
                Name = w.Name,
                Description = w.Description,
                TenantId = w.TenantId,
                OwnerUserId = w.OwnerUserId,
                OwnerDisplayName = w.OwnerUser != null ? w.OwnerUser.DisplayName : null,
                Icon = w.Icon,
                Color = w.Color,
                IsDefault = w.IsDefault,
                MemberCount = w.Members.Count,
                WorkflowCount = w.Workflows.Count,
                FolderCount = w.Folders.Count,
                CreatedAt = w.CreatedAt,
                UpdatedAt = w.UpdatedAt
            })
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Yeni workspace oluşturur ve oluşturanı Owner olarak ekler.
    /// </summary>
    public async Task<WorkspaceDto> CreateWorkspaceAsync(CreateWorkspaceRequest request, Guid userId, string? tenantId = null)
    {
        var workspace = new WorkspaceEntity
        {
            Name = request.Name,
            Description = request.Description,
            Icon = request.Icon,
            Color = request.Color,
            TenantId = tenantId,
            OwnerUserId = userId
        };

        _dbContext.Workspaces.Add(workspace);

        // Oluşturanı Owner olarak ekle
        var member = new WorkspaceMemberEntity
        {
            WorkspaceId = workspace.Id,
            UserId = userId,
            Role = WorkspaceMemberRole.Owner
        };

        _dbContext.WorkspaceMembers.Add(member);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Workspace oluşturuldu: {WorkspaceId} - {Name} (Sahip: {UserId})",
            workspace.Id, workspace.Name, userId);

        return new WorkspaceDto
        {
            Id = workspace.Id,
            Name = workspace.Name,
            Description = workspace.Description,
            TenantId = workspace.TenantId,
            OwnerUserId = workspace.OwnerUserId,
            Icon = workspace.Icon,
            Color = workspace.Color,
            IsDefault = workspace.IsDefault,
            MemberCount = 1,
            WorkflowCount = 0,
            FolderCount = 0,
            CreatedAt = workspace.CreatedAt,
            UpdatedAt = workspace.UpdatedAt
        };
    }

    /// <summary>
    /// Workspace bilgilerini günceller.
    /// </summary>
    public async Task<WorkspaceDto?> UpdateWorkspaceAsync(Guid workspaceId, UpdateWorkspaceRequest request)
    {
        var workspace = await _dbContext.Workspaces.FindAsync(workspaceId);
        if (workspace == null) return null;

        workspace.Name = request.Name;
        workspace.Description = request.Description;
        workspace.Icon = request.Icon;
        workspace.Color = request.Color;
        workspace.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Workspace güncellendi: {WorkspaceId} - {Name}", workspace.Id, workspace.Name);

        return await GetWorkspaceAsync(workspaceId);
    }

    /// <summary>
    /// Workspace'i siler. Varsayılan workspace silinemez.
    /// </summary>
    public async Task<bool> DeleteWorkspaceAsync(Guid workspaceId)
    {
        var workspace = await _dbContext.Workspaces.FindAsync(workspaceId);
        if (workspace == null) return false;

        if (workspace.IsDefault)
        {
            _logger.LogWarning("Varsayılan workspace silinemez: {WorkspaceId}", workspaceId);
            return false;
        }

        _dbContext.Workspaces.Remove(workspace);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Workspace silindi: {WorkspaceId}", workspaceId);
        return true;
    }

    // ─── Üye Yönetimi ───────────────────────────────────────────

    /// <summary>
    /// Workspace üyelerini listeler.
    /// </summary>
    public async Task<List<WorkspaceMemberDto>> GetMembersAsync(Guid workspaceId)
    {
        return await _dbContext.WorkspaceMembers
            .Include(m => m.User)
            .Where(m => m.WorkspaceId == workspaceId)
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.JoinedAt)
            .Select(m => new WorkspaceMemberDto
            {
                UserId = m.UserId,
                Email = m.User.Email,
                DisplayName = m.User.DisplayName,
                Role = m.Role.ToString(),
                JoinedAt = m.JoinedAt
            })
            .ToListAsync();
    }

    /// <summary>
    /// Workspace'e yeni üye ekler (e-posta ile).
    /// </summary>
    public async Task<WorkspaceMemberDto?> AddMemberAsync(Guid workspaceId, AddWorkspaceMemberRequest request)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
        {
            _logger.LogWarning("Üye eklenemedi: kullanıcı bulunamadı ({Email})", request.Email);
            return null;
        }

        // Zaten üye mi kontrol et
        var existing = await _dbContext.WorkspaceMembers
            .AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == user.Id);
        if (existing)
        {
            _logger.LogWarning("Kullanıcı zaten workspace üyesi: {UserId} → {WorkspaceId}", user.Id, workspaceId);
            return null;
        }

        var role = Enum.TryParse<WorkspaceMemberRole>(request.Role, true, out var parsedRole)
            ? parsedRole
            : WorkspaceMemberRole.Viewer;

        var member = new WorkspaceMemberEntity
        {
            WorkspaceId = workspaceId,
            UserId = user.Id,
            Role = role
        };

        _dbContext.WorkspaceMembers.Add(member);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Workspace üyesi eklendi: {UserId} → {WorkspaceId} ({Role})",
            user.Id, workspaceId, role);

        return new WorkspaceMemberDto
        {
            UserId = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = role.ToString(),
            JoinedAt = member.JoinedAt
        };
    }

    /// <summary>
    /// Workspace üyesinin rolünü günceller.
    /// </summary>
    public async Task<bool> UpdateMemberRoleAsync(Guid workspaceId, Guid userId, string role)
    {
        var member = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId);
        if (member == null) return false;

        // Owner rolü değiştirilemez
        if (member.Role == WorkspaceMemberRole.Owner)
        {
            _logger.LogWarning("Owner rolü değiştirilemez: {UserId} → {WorkspaceId}", userId, workspaceId);
            return false;
        }

        if (!Enum.TryParse<WorkspaceMemberRole>(role, true, out var newRole))
            return false;

        member.Role = newRole;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Workspace üye rolü güncellendi: {UserId} → {WorkspaceId} ({Role})",
            userId, workspaceId, newRole);

        return true;
    }

    /// <summary>
    /// Workspace'ten üye çıkarır. Owner çıkarılamaz.
    /// </summary>
    public async Task<bool> RemoveMemberAsync(Guid workspaceId, Guid userId)
    {
        var member = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId);
        if (member == null) return false;

        if (member.Role == WorkspaceMemberRole.Owner)
        {
            _logger.LogWarning("Owner workspace'ten çıkarılamaz: {UserId} → {WorkspaceId}", userId, workspaceId);
            return false;
        }

        _dbContext.WorkspaceMembers.Remove(member);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Workspace üyesi çıkarıldı: {UserId} → {WorkspaceId}", userId, workspaceId);
        return true;
    }

    /// <summary>
    /// Kullanıcının workspace'te belirli bir minimum role sahip olup olmadığını kontrol eder.
    /// </summary>
    public async Task<bool> HasAccessAsync(Guid workspaceId, Guid userId, WorkspaceMemberRole minRole = WorkspaceMemberRole.Viewer)
    {
        var member = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId);

        return member != null && member.Role >= minRole;
    }

    // ─── Workspace İçerik ───────────────────────────────────────

    /// <summary>
    /// Workspace'e ait klasör ağacını döner.
    /// </summary>
    public async Task<List<FolderTreeDto>> GetWorkspaceFolderTreeAsync(Guid workspaceId)
    {
        var allFolders = await _dbContext.Folders
            .Include(f => f.Workflows)
            .Where(f => f.WorkspaceId == workspaceId)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .ToListAsync();

        var rootFolders = allFolders.Where(f => f.ParentFolderId == null).ToList();
        return rootFolders.Select(f => BuildTree(f, allFolders)).ToList();
    }

    /// <summary>
    /// Workspace'e ait workflow listesini döner.
    /// </summary>
    public async Task<List<WorkflowDto>> GetWorkspaceWorkflowsAsync(Guid workspaceId)
    {
        return await _dbContext.Workflows
            .Where(w => w.WorkspaceId == workspaceId)
            .OrderByDescending(w => w.UpdatedAt)
            .Select(w => new WorkflowDto
            {
                Id = w.Id,
                Name = w.Name,
                Description = w.Description,
                TriggerType = w.TriggerType,
                IsActive = w.IsActive,
                CreatedAt = w.CreatedAt,
                UpdatedAt = w.UpdatedAt,
                RunCount = w.RunCount,
                SuccessCount = w.SuccessCount,
                FailureCount = w.FailureCount,
                AvgDurationMs = w.AvgDurationMs,
                LastRunAt = w.LastRunAt,
                FolderId = w.FolderId
            })
            .ToListAsync();
    }

    private static FolderTreeDto BuildTree(FolderEntity folder, List<FolderEntity> allFolders)
    {
        var children = allFolders
            .Where(f => f.ParentFolderId == folder.Id)
            .Select(f => BuildTree(f, allFolders))
            .ToList();

        return new FolderTreeDto
        {
            Id = folder.Id,
            Name = folder.Name,
            Icon = folder.Icon,
            Color = folder.Color,
            SortOrder = folder.SortOrder,
            Children = children,
            Workflows = folder.Workflows.Select(w => new WorkflowDto
            {
                Id = w.Id,
                Name = w.Name,
                Description = w.Description,
                TriggerType = w.TriggerType,
                IsActive = w.IsActive,
                RunCount = w.RunCount,
                LastRunAt = w.LastRunAt
            }).ToList()
        };
    }
}
