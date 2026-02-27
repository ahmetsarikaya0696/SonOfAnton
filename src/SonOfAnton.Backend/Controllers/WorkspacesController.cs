using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Permissions.WorkspacesRead)]
public class WorkspacesController : ControllerBase
{
    private readonly WorkspaceService _workspaceService;
    private readonly ILogger<WorkspacesController> _logger;

    public WorkspacesController(WorkspaceService workspaceService, ILogger<WorkspacesController> logger)
    {
        _workspaceService = workspaceService;
        _logger = logger;
    }

    /// <summary>
    /// Kullanıcının erişebildiği workspace listesi
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<WorkspaceDto>>> GetAll()
    {
        // TODO: IUserContext'ten userId ve tenantId al — şimdilik Guid.Empty ile çalış
        var userId = Guid.Empty;
        var workspaces = await _workspaceService.GetWorkspacesAsync(userId);
        return Ok(workspaces);
    }

    /// <summary>
    /// Workspace detayı
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkspaceDto>> Get(Guid id)
    {
        var workspace = await _workspaceService.GetWorkspaceAsync(id);
        if (workspace == null) return NotFound();
        return Ok(workspace);
    }

    /// <summary>
    /// Yeni workspace oluştur
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.WorkspacesCreate)]
    public async Task<ActionResult<WorkspaceDto>> Create([FromBody] CreateWorkspaceRequest request)
    {
        // TODO: IUserContext'ten userId ve tenantId al
        var userId = Guid.Empty;
        var workspace = await _workspaceService.CreateWorkspaceAsync(request, userId);
        return Ok(workspace);
    }

    /// <summary>
    /// Workspace güncelle
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.WorkspacesManage)]
    public async Task<ActionResult<WorkspaceDto>> Update(Guid id, [FromBody] UpdateWorkspaceRequest request)
    {
        var result = await _workspaceService.UpdateWorkspaceAsync(id, request);
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// Workspace sil
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.WorkspacesManage)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _workspaceService.DeleteWorkspaceAsync(id);
        if (!result) return BadRequest("Workspace silinemedi (varsayılan workspace olabilir).");
        return NoContent();
    }

    // ─── Üye Yönetimi ───────────────────────────────────────────

    /// <summary>
    /// Workspace üyelerini listele
    /// </summary>
    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<List<WorkspaceMemberDto>>> GetMembers(Guid id)
    {
        var members = await _workspaceService.GetMembersAsync(id);
        return Ok(members);
    }

    /// <summary>
    /// Workspace'e üye ekle
    /// </summary>
    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = Permissions.WorkspaceMembersManage)]
    public async Task<ActionResult<WorkspaceMemberDto>> AddMember(Guid id, [FromBody] AddWorkspaceMemberRequest request)
    {
        var member = await _workspaceService.AddMemberAsync(id, request);
        if (member == null) return BadRequest("Üye eklenemedi (kullanıcı bulunamadı veya zaten üye).");
        return Ok(member);
    }

    /// <summary>
    /// Workspace üye rolünü güncelle
    /// </summary>
    [HttpPut("{id:guid}/members/{userId:guid}")]
    [Authorize(Policy = Permissions.WorkspaceMembersManage)]
    public async Task<IActionResult> UpdateMemberRole(Guid id, Guid userId, [FromBody] UpdateWorkspaceMemberRequest request)
    {
        var result = await _workspaceService.UpdateMemberRoleAsync(id, userId, request.Role);
        if (!result) return BadRequest("Rol güncellenemedi.");
        return NoContent();
    }

    /// <summary>
    /// Workspace'ten üye çıkar
    /// </summary>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [Authorize(Policy = Permissions.WorkspaceMembersManage)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        var result = await _workspaceService.RemoveMemberAsync(id, userId);
        if (!result) return BadRequest("Üye çıkarılamadı (Owner çıkarılamaz).");
        return NoContent();
    }

    // ─── Workspace İçerik ───────────────────────────────────────

    /// <summary>
    /// Workspace klasör ağacı
    /// </summary>
    [HttpGet("{id:guid}/folders")]
    public async Task<ActionResult<List<FolderTreeDto>>> GetFolders(Guid id)
    {
        var tree = await _workspaceService.GetWorkspaceFolderTreeAsync(id);
        return Ok(tree);
    }

    /// <summary>
    /// Workspace workflow'ları
    /// </summary>
    [HttpGet("{id:guid}/workflows")]
    public async Task<ActionResult<List<WorkflowDto>>> GetWorkflows(Guid id)
    {
        var workflows = await _workspaceService.GetWorkspaceWorkflowsAsync(id);
        return Ok(workflows);
    }
}
