using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;

namespace SonOfAnton.Backend.Services;

/// <summary>
/// SCIM 2.0 protokol mantığını iş mantığına çeviren servis.
/// Kullanıcı ve grup CRUD işlemlerini yönetir.
/// </summary>
public class ScimService
{
    private readonly SonOfAntonDbContext _dbContext;
    private readonly ILogger<ScimService> _logger;

    public ScimService(SonOfAntonDbContext dbContext, ILogger<ScimService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    // ─── Users ──────────────────────────────────────────────────

    public async Task<ScimListResponse<ScimUser>> ListUsersAsync(string tenantId, int startIndex = 1, int count = 100)
    {
        var query = _dbContext.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId);

        var total = await query.CountAsync();
        var users = await query
            .OrderBy(u => u.CreatedAt)
            .Skip(startIndex - 1)
            .Take(count)
            .ToListAsync();

        return new ScimListResponse<ScimUser>
        {
            TotalResults = total,
            StartIndex = startIndex,
            ItemsPerPage = users.Count,
            Resources = users.Select(MapToScimUser).ToList()
        };
    }

    public async Task<ScimUser?> GetUserAsync(string tenantId, Guid userId)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId);

        return user is null ? null : MapToScimUser(user);
    }

    public async Task<ScimUser> CreateUserAsync(string tenantId, ScimUser scimUser)
    {
        var email = scimUser.Emails?.FirstOrDefault(e => e.Primary)?.Value
                 ?? scimUser.Emails?.FirstOrDefault()?.Value
                 ?? scimUser.UserName;

        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            ExternalIdentityId = scimUser.ExternalId ?? scimUser.UserName,
            Email = email,
            DisplayName = scimUser.DisplayName ?? scimUser.Name?.Formatted,
            TenantId = tenantId,
            IsActive = scimUser.Active,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("SCIM: Kullanıcı oluşturuldu - {UserId} (Tenant: {TenantId})", user.Id, tenantId);

        return MapToScimUser(user);
    }

    public async Task<ScimUser?> UpdateUserAsync(string tenantId, Guid userId, ScimUser scimUser)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId);

        if (user is null) return null;

        var email = scimUser.Emails?.FirstOrDefault(e => e.Primary)?.Value
                 ?? scimUser.Emails?.FirstOrDefault()?.Value;

        if (!string.IsNullOrEmpty(email)) user.Email = email;
        if (!string.IsNullOrEmpty(scimUser.DisplayName)) user.DisplayName = scimUser.DisplayName;
        user.IsActive = scimUser.Active;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("SCIM: Kullanıcı güncellendi - {UserId}", userId);
        return MapToScimUser(user);
    }

    public async Task<bool> PatchUserAsync(string tenantId, Guid userId, ScimPatchRequest patchRequest)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId);

        if (user is null) return false;

        foreach (var op in patchRequest.Operations)
        {
            switch (op.Op.ToLowerInvariant())
            {
                case "replace":
                    ApplyUserPatch(user, op);
                    break;
                case "remove":
                    // Gerekli alanlar kaldırılamaz
                    break;
            }
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("SCIM: Kullanıcı patch uygulandı - {UserId}", userId);
        return true;
    }

    public async Task<bool> DeleteUserAsync(string tenantId, Guid userId)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId);

        if (user is null) return false;

        // Soft delete: sadece deaktive et
        user.IsActive = false;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("SCIM: Kullanıcı deaktive edildi - {UserId}", userId);
        return true;
    }

    // ─── Groups ─────────────────────────────────────────────────

    public async Task<ScimListResponse<ScimGroup>> ListGroupsAsync(string tenantId, int startIndex = 1, int count = 100)
    {
        var query = _dbContext.TenantRoles
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId);

        var total = await query.CountAsync();
        var roles = await query
            .Include(r => r.UserRoles)
                .ThenInclude(ur => ur.User)
            .OrderBy(r => r.CreatedAt)
            .Skip(startIndex - 1)
            .Take(count)
            .ToListAsync();

        return new ScimListResponse<ScimGroup>
        {
            TotalResults = total,
            StartIndex = startIndex,
            ItemsPerPage = roles.Count,
            Resources = roles.Select(MapToScimGroup).ToList()
        };
    }

    public async Task<ScimGroup?> GetGroupAsync(string tenantId, Guid groupId)
    {
        var role = await _dbContext.TenantRoles
            .AsNoTracking()
            .Include(r => r.UserRoles)
                .ThenInclude(ur => ur.User)
            .FirstOrDefaultAsync(r => r.Id == groupId && r.TenantId == tenantId);

        return role is null ? null : MapToScimGroup(role);
    }

    public async Task<ScimGroup> CreateGroupAsync(string tenantId, ScimGroup scimGroup)
    {
        var role = new TenantRoleEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExternalRoleName = scimGroup.DisplayName,
            MappedPermissions = [], // Varsayılan boş — admin sonra eşleştirir
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TenantRoles.Add(role);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("SCIM: Grup oluşturuldu - {GroupId} ({DisplayName})", role.Id, scimGroup.DisplayName);

        return MapToScimGroup(role);
    }

    public async Task<bool> DeleteGroupAsync(string tenantId, Guid groupId)
    {
        var role = await _dbContext.TenantRoles
            .FirstOrDefaultAsync(r => r.Id == groupId && r.TenantId == tenantId);

        if (role is null) return false;

        _dbContext.TenantRoles.Remove(role);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("SCIM: Grup silindi - {GroupId}", groupId);
        return true;
    }

    // ─── Mapping Helpers ────────────────────────────────────────

    private static ScimUser MapToScimUser(UserEntity user)
    {
        return new ScimUser
        {
            Id = user.Id.ToString(),
            ExternalId = user.ExternalIdentityId,
            UserName = user.Email,
            DisplayName = user.DisplayName,
            Active = user.IsActive,
            Emails = [new ScimEmail { Value = user.Email, Type = "work", Primary = true }],
            Meta = new ScimMeta
            {
                ResourceType = "User",
                Created = user.CreatedAt,
                LastModified = user.LastLoginAt ?? user.CreatedAt
            }
        };
    }

    private static ScimGroup MapToScimGroup(TenantRoleEntity role)
    {
        return new ScimGroup
        {
            Id = role.Id.ToString(),
            DisplayName = role.ExternalRoleName,
            Members = role.UserRoles?.Select(ur => new ScimMember
            {
                Value = ur.UserId.ToString(),
                Display = ur.User?.DisplayName ?? ur.User?.Email
            }).ToList(),
            Meta = new ScimMeta
            {
                ResourceType = "Group",
                Created = role.CreatedAt
            }
        };
    }

    private static void ApplyUserPatch(UserEntity user, ScimPatchOperation op)
    {
        var value = op.Value?.ToString();
        switch (op.Path?.ToLowerInvariant())
        {
            case "active":
                if (bool.TryParse(value, out var active))
                    user.IsActive = active;
                break;
            case "displayname":
                if (!string.IsNullOrEmpty(value))
                    user.DisplayName = value;
                break;
            case "emails[type eq \"work\"].value":
                if (!string.IsNullOrEmpty(value))
                    user.Email = value;
                break;
        }
    }
}
