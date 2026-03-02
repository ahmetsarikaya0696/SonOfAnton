using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Shared.Core;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Backend.Controllers;

/// <summary>
/// Planlama belgeleri (task_plan.md, findings.md, progress.md) CRUD API'si.
/// Ajanlar ve frontend tarafından kullanılır.
/// </summary>
[ApiController]
[Route("api/planning")]
public class PlanningController : ControllerBase
{
    private readonly IMarkdownPlanningService _planningService;
    private readonly ILogger<PlanningController> _logger;

    public PlanningController(
        IMarkdownPlanningService planningService,
        ILogger<PlanningController> logger)
    {
        _planningService = planningService;
        _logger = logger;
    }

    /// <summary>Belirtilen ajanın tüm belgelerini listeler</summary>
    [HttpGet("documents/{agentId}")]
    public async Task<ActionResult<IReadOnlyList<PlanningDocumentDto>>> GetAllDocuments(string agentId)
    {
        var documents = await _planningService.GetAllDocumentsAsync(agentId);
        return Ok(documents);
    }

    /// <summary>Belirtilen tipte belgeyi okur</summary>
    [HttpGet("documents/{agentId}/{fileType}")]
    public async Task<ActionResult<PlanningDocumentDto>> GetDocument(string agentId, PlanFileType fileType)
    {
        var document = await _planningService.ReadDocumentAsync(agentId, fileType);
        if (document == null) return NotFound();
        return Ok(document);
    }

    /// <summary>Belge yazar (oluşturur veya günceller)</summary>
    [HttpPut("documents/{agentId}/{fileType}")]
    public async Task<ActionResult<PlanningDocumentDto>> WriteDocument(
        string agentId,
        PlanFileType fileType,
        [FromBody] WriteDocumentRequest request)
    {
        var document = await _planningService.WriteDocumentAsync(agentId, fileType, request.Content);
        return Ok(document);
    }

    /// <summary>Belgenin sonuna içerik ekler</summary>
    [HttpPost("documents/{agentId}/{fileType}/append")]
    public async Task<ActionResult<PlanningDocumentDto>> AppendToDocument(
        string agentId,
        PlanFileType fileType,
        [FromBody] WriteDocumentRequest request)
    {
        var document = await _planningService.AppendToDocumentAsync(agentId, fileType, request.Content);
        return Ok(document);
    }

    /// <summary>Belge versiyonlarını listeler</summary>
    [HttpGet("documents/{agentId}/{fileType}/history")]
    public async Task<ActionResult<IReadOnlyList<PlanningDocumentDto>>> GetDocumentHistory(
        string agentId,
        PlanFileType fileType)
    {
        var history = await _planningService.GetDocumentHistoryAsync(agentId, fileType);
        return Ok(history);
    }

    /// <summary>Belgeyi siler</summary>
    [HttpDelete("documents/{agentId}/{fileType}")]
    public async Task<IActionResult> DeleteDocument(string agentId, PlanFileType fileType)
    {
        await _planningService.DeleteDocumentAsync(agentId, fileType);
        return NoContent();
    }

    /// <summary>Multipart form-data ile .md dosya yükler ve belge olarak kaydeder</summary>
    [HttpPost("upload/{agentId}/{fileType}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<PlanningDocumentDto>> UploadDocument(
        string agentId,
        PlanFileType fileType,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Dosya boş veya eksik." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".md" && extension != ".markdown" && extension != ".txt")
            return BadRequest(new { error = "Sadece .md, .markdown ve .txt uzantılı dosyalar kabul edilir." });

        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync(cancellationToken);

        var document = await _planningService.WriteDocumentAsync(agentId, fileType, content, cancellationToken);
        return Ok(document);
    }
}

/// <summary>Belge yazma isteği</summary>
public record WriteDocumentRequest
{
    public required string Content { get; init; }
}
