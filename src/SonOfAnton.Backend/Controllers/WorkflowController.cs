using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.Models;

namespace SonOfAnton.Backend.Controllers;

/// <summary>
/// Workflow CRUD ve yönetim iþlemleri.
/// 
/// Workflow, birbiriyle baðlý node'lardan oluþan bir iþ akýþý tanýmýdýr.
/// Bu controller ile workflow oluþturabilir, güncelleyebilir, silebilir
/// ve çalýþtýrabilirsiniz.
/// 
/// <para><b>Junior Geliþtiriciler Ýçin:</b></para>
/// <para>
/// Workflow = Bir iþ akýþý þablonu (örn: "Yeni müþteri gelince e-posta gönder")
/// Execution = Bu þablonun bir kez çalýþtýrýlmasý (örn: "Ahmet için e-posta gönderildi")
/// </para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class WorkflowController : ControllerBase
{
    private static readonly List<Workflow> _workflows = [];
    private readonly WorkflowJobService _jobService;

    public WorkflowController(WorkflowJobService jobService)
    {
        _jobService = jobService;
    }

    /// <summary>
    /// Tüm workflow'larý listele.
    /// </summary>
    /// <returns>Kayýtlý tüm workflow'larýn listesi.</returns>
    /// <response code="200">Baþarýlý - Workflow listesi döner (boþ olabilir).</response>
    /// <remarks>
    /// Örnek istek:
    /// 
    ///     GET /api/workflow
    /// 
    /// Sayfalama yoktur, tüm workflow'lar döner.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Workflow>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<Workflow>> GetAll()
    {
        return Ok(_workflows);
    }

    /// <summary>
    /// Belirli bir workflow'u ID ile getir.
    /// </summary>
    /// <param name="id">Workflow'un benzersiz kimliði (GUID formatýnda).</param>
    /// <returns>Ýstenen workflow detaylarý.</returns>
    /// <response code="200">Baþarýlý - Workflow bulundu.</response>
    /// <response code="404">Workflow bulunamadý.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Workflow), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Workflow> Get(Guid id)
    {
        var workflow = _workflows.FirstOrDefault(w => w.Id == id);
        if (workflow == null)
            return NotFound();

        return Ok(workflow);
    }

    /// <summary>
    /// Yeni bir workflow oluþtur.
    /// </summary>
    /// <param name="workflow">Oluþturulacak workflow bilgileri.</param>
    /// <returns>Oluþturulan workflow (ID ve tarihler otomatik atanýr).</returns>
    /// <response code="201">Baþarýlý - Workflow oluþturuldu.</response>
    /// <remarks>
    /// Örnek istek:
    /// 
    ///     POST /api/workflow
    ///     {
    ///         "name": "Müþteri Hoþgeldin Akýþý",
    ///         "description": "Yeni müþteriye hoþgeldin e-postasý gönderir",
    ///         "nodes": [],
    ///         "connections": []
    ///     }
    /// 
    /// <para><b>Not:</b> Id, CreatedAt ve UpdatedAt alanlarý otomatik atanýr.</para>
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(Workflow), StatusCodes.Status201Created)]
    public ActionResult<Workflow> Create([FromBody] Workflow workflow)
    {
        workflow.Id = Guid.NewGuid();
        workflow.CreatedAt = DateTime.UtcNow;
        workflow.UpdatedAt = DateTime.UtcNow;
        _workflows.Add(workflow);

        return CreatedAtAction(nameof(Get), new { id = workflow.Id }, workflow);
    }

    /// <summary>
    /// Var olan bir workflow'u güncelle.
    /// </summary>
    /// <param name="id">Güncellenecek workflow'un ID'si.</param>
    /// <param name="workflow">Yeni workflow bilgileri.</param>
    /// <returns>Güncellenen workflow.</returns>
    /// <response code="200">Baþarýlý - Workflow güncellendi.</response>
    /// <response code="404">Workflow bulunamadý.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Workflow), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Workflow> Update(Guid id, [FromBody] Workflow workflow)
    {
        var existing = _workflows.FirstOrDefault(w => w.Id == id);
        if (existing == null)
            return NotFound();

        existing.Name = workflow.Name;
        existing.Description = workflow.Description;
        existing.Nodes = workflow.Nodes;
        existing.Connections = workflow.Connections;
        existing.IsActive = workflow.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        return Ok(existing);
    }

    /// <summary>
    /// Workflow'u sil.
    /// </summary>
    /// <param name="id">Silinecek workflow'un ID'si.</param>
    /// <returns>Baþarýlý silme durumunda 204 NoContent.</returns>
    /// <response code="204">Baþarýlý - Workflow silindi.</response>
    /// <response code="404">Workflow bulunamadý.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult Delete(Guid id)
    {
        var workflow = _workflows.FirstOrDefault(w => w.Id == id);
        if (workflow == null)
            return NotFound();

        _workflows.Remove(workflow);
        return NoContent();
    }

    /// <summary>
    /// Workflow'u çalýþtýr (Hangfire üzerinden arka planda).
    /// </summary>
    /// <param name="id">Çalýþtýrýlacak workflow'un ID'si.</param>
    /// <param name="triggerData">Workflow'a gönderilecek baþlangýç verisi (opsiyonel).</param>
    /// <returns>Execution ID ve bilgi mesajý.</returns>
    /// <response code="200">Baþarýlý - Workflow kuyruða eklendi.</response>
    /// <response code="404">Workflow bulunamadý.</response>
    /// <remarks>
    /// Workflow hemen çalýþmaz, Hangfire kuyruðuna eklenir.
    /// Dönen executionId ile execution durumunu takip edebilirsiniz.
    /// 
    ///     POST /api/workflow/{id}/execute
    ///     { "key": "value" }
    /// 
    /// </remarks>
    [HttpPost("{id}/execute")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult Execute(Guid id, [FromBody] object? triggerData = null)
    {
        var workflow = _workflows.FirstOrDefault(w => w.Id == id);
        if (workflow == null)
            return NotFound();

        var executionId = _jobService.EnqueueWorkflow(id, triggerData);

        return Ok(new { message = "Workflow execution started", workflowId = id, executionId });
    }

    /// <summary>
    /// Workflow'u aktifleþtir (schedule ve webhook tetikleyicileri çalýþýr hale gelir).
    /// </summary>
    /// <param name="id">Aktifleþtirilecek workflow'un ID'si.</param>
    /// <returns>Güncellenen workflow.</returns>
    /// <response code="200">Baþarýlý - Workflow aktifleþtirildi.</response>
    /// <response code="404">Workflow bulunamadý.</response>
    [HttpPost("{id}/activate")]
    [ProducesResponseType(typeof(Workflow), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult Activate(Guid id)
    {
        var workflow = _workflows.FirstOrDefault(w => w.Id == id);
        if (workflow == null)
            return NotFound();

        workflow.IsActive = true;
        workflow.UpdatedAt = DateTime.UtcNow;

        return Ok(workflow);
    }

    /// <summary>
    /// Workflow'u devre dýþý býrak (tetikleyiciler durur).
    /// </summary>
    /// <param name="id">Devre dýþý býrakýlacak workflow'un ID'si.</param>
    /// <returns>Güncellenen workflow.</returns>
    /// <response code="200">Baþarýlý - Workflow devre dýþý býrakýldý.</response>
    /// <response code="404">Workflow bulunamadý.</response>
    [HttpPost("{id}/deactivate")]
    [ProducesResponseType(typeof(Workflow), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult Deactivate(Guid id)
    {
        var workflow = _workflows.FirstOrDefault(w => w.Id == id);
        if (workflow == null)
            return NotFound();

        workflow.IsActive = false;
        workflow.UpdatedAt = DateTime.UtcNow;

        return Ok(workflow);
    }
}

