using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/credentials")]
[Authorize]
public class CredentialsController : ControllerBase
{
    private readonly SonOfAntonDbContext _dbContext;
    private readonly IUserContext _userContext;
    private readonly IEncryptionService _encryptionService;

    public CredentialsController(
        SonOfAntonDbContext dbContext,
        IUserContext userContext,
        IEncryptionService encryptionService)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _encryptionService = encryptionService;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var tenantId = _userContext.TenantId;
        var userId = _userContext.CurrentUserId;
        
        var credentials = await _dbContext.Set<CredentialEntity>()
            .Where(c => c.TenantId == tenantId && c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CredentialDto
            {
                Id = c.Id,
                Name = c.Name,
                SystemName = c.SystemName,
                Description = c.Description,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync();

        return Ok(credentials);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCredentialRequest request)
    {
        var tenantId = _userContext.TenantId;
        var userId = _userContext.CurrentUserId;

        var encryptedData = _encryptionService.Encrypt(request.RawData);

        var entity = new CredentialEntity
        {
            Name = request.Name,
            SystemName = request.SystemName,
            Description = request.Description,
            EncryptedData = encryptedData,
            TenantId = tenantId ?? "default",
            UserId = userId
        };

        _dbContext.Add(entity);
        await _dbContext.SaveChangesAsync();

        return Ok(new CredentialDto
        {
            Id = entity.Id,
            Name = entity.Name,
            SystemName = entity.SystemName,
            Description = entity.Description,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCredentialRequest request)
    {
        var tenantId = _userContext.TenantId;
        var userId = _userContext.CurrentUserId;

        var entity = await _dbContext.Set<CredentialEntity>()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && c.UserId == userId);

        if (entity == null) return NotFound();

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(request.RawData))
        {
            entity.EncryptedData = _encryptionService.Encrypt(request.RawData);
        }

        await _dbContext.SaveChangesAsync();

        return Ok(new CredentialDto
        {
            Id = entity.Id,
            Name = entity.Name,
            SystemName = entity.SystemName,
            Description = entity.Description,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tenantId = _userContext.TenantId;
        var userId = _userContext.CurrentUserId;

        var entity = await _dbContext.Set<CredentialEntity>()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && c.UserId == userId);

        if (entity == null) return NotFound();

        _dbContext.Remove(entity);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }
}
