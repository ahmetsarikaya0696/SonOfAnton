using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoldersController : ControllerBase
{
    private readonly SonOfAntonDbContext _dbContext;
    private readonly ILogger<FoldersController> _logger;

    public FoldersController(SonOfAntonDbContext dbContext, ILogger<FoldersController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Tüm klasörleri düz liste olarak getir
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<FolderDto>>> GetAll()
    {
        var folders = await _dbContext.Folders
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .Select(f => new FolderDto
            {
                Id = f.Id,
                Name = f.Name,
                Icon = f.Icon,
                Color = f.Color,
                SortOrder = f.SortOrder,
                ParentFolderId = f.ParentFolderId,
                CreatedAt = f.CreatedAt,
                WorkflowCount = f.Workflows.Count
            })
            .ToListAsync();

        return Ok(folders);
    }

    /// <summary>
    /// Klasör ağacını getir (recursive)
    /// </summary>
    [HttpGet("tree")]
    public async Task<ActionResult<List<FolderTreeDto>>> GetTree()
    {
        var allFolders = await _dbContext.Folders
            .Include(f => f.Workflows)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .ToListAsync();

        var rootFolders = allFolders.Where(f => f.ParentFolderId == null).ToList();
        var tree = rootFolders.Select(f => BuildTree(f, allFolders)).ToList();

        return Ok(tree);
    }

    /// <summary>
    /// Klasör oluştur
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<FolderDto>> Create([FromBody] CreateFolderRequest request)
    {
        var folder = new FolderEntity
        {
            Name = request.Name,
            Icon = request.Icon,
            Color = request.Color,
            ParentFolderId = request.ParentFolderId
        };

        _dbContext.Folders.Add(folder);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Klasör oluşturuldu: {FolderId} - {Name}", folder.Id, folder.Name);

        return Ok(new FolderDto
        {
            Id = folder.Id,
            Name = folder.Name,
            Icon = folder.Icon,
            Color = folder.Color,
            SortOrder = folder.SortOrder,
            ParentFolderId = folder.ParentFolderId,
            CreatedAt = folder.CreatedAt
        });
    }

    /// <summary>
    /// Klasör güncelle
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FolderDto>> Update(Guid id, [FromBody] CreateFolderRequest request)
    {
        var folder = await _dbContext.Folders.FindAsync(id);
        if (folder == null) return NotFound();

        folder.Name = request.Name;
        folder.Icon = request.Icon;
        folder.Color = request.Color;
        folder.ParentFolderId = request.ParentFolderId;

        await _dbContext.SaveChangesAsync();

        return Ok(new FolderDto
        {
            Id = folder.Id,
            Name = folder.Name,
            Icon = folder.Icon,
            Color = folder.Color,
            SortOrder = folder.SortOrder,
            ParentFolderId = folder.ParentFolderId,
            CreatedAt = folder.CreatedAt
        });
    }

    /// <summary>
    /// Klasör sil
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var folder = await _dbContext.Folders.FindAsync(id);
        if (folder == null) return NotFound();

        _dbContext.Folders.Remove(folder);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Klasör silindi: {FolderId}", id);
        return NoContent();
    }

    /// <summary>
    /// Klasör içindeki workflow'ları getir
    /// </summary>
    [HttpGet("{id:guid}/workflows")]
    public async Task<ActionResult<List<WorkflowDto>>> GetWorkflowsInFolder(Guid id)
    {
        var workflows = await _dbContext.Workflows
            .Where(w => w.FolderId == id)
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

        return Ok(workflows);
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
