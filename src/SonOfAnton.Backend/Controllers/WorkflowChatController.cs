using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.Agents;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Serialization;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/workflow/chat")]
public class WorkflowChatController : ControllerBase
{
    private readonly WorkflowAssistantService _assistantService;
    private readonly WorkflowYamlSerializer _yamlSerializer;
    private readonly ILogger<WorkflowChatController> _logger;

    public WorkflowChatController(
        WorkflowAssistantService assistantService,
        WorkflowYamlSerializer yamlSerializer,
        ILogger<WorkflowChatController> logger)
    {
        _assistantService = assistantService;
        _yamlSerializer = yamlSerializer;
        _logger = logger;
    }

    /// <summary>
    /// Chat mesajı gönder — AI workflow önerisi döndürür
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<WorkflowChatResponse>> Chat(
        [FromBody] WorkflowChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Mesaj gerekli" });

        var response = await _assistantService.ProcessChatAsync(request, ct);

        // YAML preview oluştur
        if (response.SuggestedGraph != null && string.IsNullOrEmpty(response.YamlPreview))
        {
            try
            {
                response.YamlPreview = _yamlSerializer.Serialize(response.SuggestedGraph);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "YAML preview oluşturulamadı");
            }
        }

        return Ok(response);
    }

    /// <summary>
    /// YAML import — YAML'dan graph oluşturur
    /// </summary>
    [HttpPost("import-yaml")]
    public ActionResult<YamlValidationResult> ImportYaml([FromBody] YamlImportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.YamlContent))
            return BadRequest(new { error = "YAML içeriği gerekli" });

        var validation = _yamlSerializer.Validate(request.YamlContent);

        return Ok(new YamlValidationResult
        {
            IsValid = validation.IsValid,
            Errors = validation.Errors,
            ParsedGraph = validation.ParsedGraph
        });
    }

    /// <summary>
    /// Graph'ı YAML'a dönüştür
    /// </summary>
    [HttpPost("export-yaml")]
    public ActionResult<PlanArtifactDto> ExportYaml([FromBody] WorkflowGraphDto graph)
    {
        try
        {
            var yaml = _yamlSerializer.Serialize(graph);
            return Ok(new PlanArtifactDto
            {
                Type = Shared.Enums.ArtifactType.Yaml,
                Content = yaml,
                FileName = "workflow.yaml"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"YAML oluşturulamadı: {ex.Message}" });
        }
    }
}
