using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Backend.Infrastructure.Identity;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;
using System.Security.Cryptography;
using System.Text.Json;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/apikeys")]
[Authorize(Policy = Permissions.ApiKeysManage)]
public class ApiKeysController : ControllerBase
{
    private readonly SonOfAntonDbContext _dbContext;
    private readonly ITenantResolver _tenantResolver;
    private readonly IUserContext _userContext;

    public ApiKeysController(
        SonOfAntonDbContext dbContext,
        ITenantResolver tenantResolver,
        IUserContext userContext)
    {
        _dbContext = dbContext;
        _tenantResolver = tenantResolver;
        _userContext = userContext;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.ApiKeysRead)]
    public async Task<IActionResult> List()
    {
        var tenantId = _userContext.TenantId;
        
        var keys = await _dbContext.Set<ApiKeyEntity>()
            .Where(k => k.TenantId == tenantId)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyDto
            {
                Id = k.Id,
                Name = k.Name,
                KeyPrefix = k.KeyPrefix,
                IsActive = k.IsActive,
                RateLimitPerMinute = k.RateLimitPerMinute,
                ExpiresAt = k.ExpiresAt,
                LastUsedAt = k.LastUsedAt,
                CreatedAt = k.CreatedAt,
                Scopes = k.Scopes != null ? JsonSerializer.Deserialize<List<string>>(k.Scopes, new JsonSerializerOptions()) ?? new List<string>() : new List<string>()
            })
            .ToListAsync();

        return Ok(keys);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApiKeyRequest request)
    {
        var tenantId = _userContext.TenantId;
        
        // Generate random secure token
        var plainKey = "sk_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
            
        var keyHash = ApiKeyAuthenticationHandler.HashApiKey(plainKey);
        var keyPrefix = plainKey.Substring(0, 8) + "...";

        var entity = new ApiKeyEntity
        {
            Name = request.Name,
            KeyPrefix = keyPrefix,
            KeyHash = keyHash,
            TenantId = tenantId,
            CreatedByUserId = _userContext.CurrentUserId,
            Scopes = request.Scopes.Any() ? JsonSerializer.SerializeToDocument(request.Scopes) : null,
            RateLimitPerMinute = request.RateLimitPerMinute > 0 ? request.RateLimitPerMinute : 60,
            ExpiresAt = request.ExpiresAt,
            IsActive = true
        };

        _dbContext.Add(entity);
        await _dbContext.SaveChangesAsync();

        // Plain key is strictly returned ONCE here
        return Ok(new CreateApiKeyResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            KeyPrefix = entity.KeyPrefix,
            PlainKey = plainKey,
            Scopes = request.Scopes,
            RateLimitPerMinute = entity.RateLimitPerMinute,
            ExpiresAt = entity.ExpiresAt,
            CreatedAt = entity.CreatedAt
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tenantId = _userContext.TenantId;
        var key = await _dbContext.Set<ApiKeyEntity>()
            .FirstOrDefaultAsync(k => k.Id == id && k.TenantId == tenantId);

        if (key == null) return NotFound();

        _dbContext.Remove(key);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpPatch("{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid id)
    {
        var tenantId = _userContext.TenantId;
        var key = await _dbContext.Set<ApiKeyEntity>()
            .FirstOrDefaultAsync(k => k.Id == id && k.TenantId == tenantId);

        if (key == null) return NotFound();

        key.IsActive = !key.IsActive;
        await _dbContext.SaveChangesAsync();

        return Ok(new { isActive = key.IsActive });
    }

    [HttpPatch("{id:guid}/scopes")]
    public async Task<IActionResult> UpdateScopes(Guid id, [FromBody] UpdateApiKeyScopesRequest request)
    {
        var tenantId = _userContext.TenantId;
        var key = await _dbContext.Set<ApiKeyEntity>()
            .FirstOrDefaultAsync(k => k.Id == id && k.TenantId == tenantId);

        if (key == null) return NotFound();

        key.Scopes = request.Scopes.Any() ? JsonSerializer.SerializeToDocument(request.Scopes) : null;
        await _dbContext.SaveChangesAsync();

        return Ok(new { scopes = request.Scopes });
    }
}
