using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Shared.Core;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Backend.Controllers;

/// <summary>
/// Ralph Döngü Motoru (Loop Engine) yönetim API'si.
/// Döngü başlatma, durdurma, durum sorgulama işlemleri.
/// </summary>
[ApiController]
[Route("api/loops")]
public class RalphLoopController : ControllerBase
{
    private readonly ILoopStateStore _stateStore;
    private readonly ILogger<RalphLoopController> _logger;

    public RalphLoopController(
        ILoopStateStore stateStore,
        ILogger<RalphLoopController> logger)
    {
        _stateStore = stateStore;
        _logger = logger;
    }

    /// <summary>Tüm aktif döngüleri listeler</summary>
    [HttpGet("active")]
    public async Task<ActionResult<IReadOnlyList<LoopExecutionStateDto>>> GetActiveLoops()
    {
        var loops = await _stateStore.GetActiveLoopsAsync();
        return Ok(loops);
    }

    /// <summary>Belirli bir döngünün durumunu getirir</summary>
    [HttpGet("{loopId}")]
    public async Task<ActionResult<LoopExecutionStateDto>> GetLoopState(string loopId)
    {
        var state = await _stateStore.GetStateAsync(loopId);
        if (state == null) return NotFound();
        return Ok(state);
    }

    /// <summary>Döngü durumunun özetini getirir (dashboard için)</summary>
    [HttpGet("{loopId}/status")]
    public async Task<ActionResult<LoopStatusDto>> GetLoopStatus(string loopId)
    {
        var state = await _stateStore.GetStateAsync(loopId);
        if (state == null) return NotFound();

        var elapsed = (state.CompletedAt ?? DateTime.UtcNow) - state.StartedAt;
        var progress = state.MaxIterations > 0
            ? (double)state.CurrentIteration / state.MaxIterations * 100
            : 0;

        return Ok(new LoopStatusDto(
            state.LoopId,
            state.State,
            state.CurrentIteration,
            state.MaxIterations,
            Math.Round(progress, 1),
            elapsed,
            state.ExitReason?.ToString()));
    }

    /// <summary>Yeni döngü başlatır</summary>
    [HttpPost("start")]
    public async Task<ActionResult<LoopExecutionStateDto>> StartLoop([FromBody] LoopStartRequest request)
    {
        // Zaten aktif bir döngü var mı kontrol et
        var existing = await _stateStore.GetStateAsync(request.LoopId);
        if (existing != null && (existing.State == LoopState.Running || existing.State == LoopState.Paused))
        {
            return Conflict(new { error = $"'{request.LoopId}' zaten aktif bir döngüye sahip." });
        }

        var state = await _stateStore.SaveStateAsync(
            request.LoopId,
            LoopState.Running,
            request.MaxIterations,
            request.InitialContextJson);

        _logger.LogInformation("Döngü başlatıldı: {LoopId}, MaxIterations={Max}",
            request.LoopId, request.MaxIterations);

        return CreatedAtAction(nameof(GetLoopState), new { loopId = request.LoopId }, state);
    }

    /// <summary>Döngüyü durdurur</summary>
    [HttpPost("stop")]
    public async Task<IActionResult> StopLoop([FromBody] LoopStopRequest request)
    {
        var state = await _stateStore.GetStateAsync(request.LoopId);
        if (state == null) return NotFound();

        if (state.State != LoopState.Running && state.State != LoopState.Paused)
        {
            return BadRequest(new { error = "Döngü zaten aktif değil." });
        }

        await _stateStore.CompleteLoopAsync(
            request.LoopId,
            LoopExitReason.ManualStop,
            request.Reason ?? "Manuel durdurma");

        _logger.LogInformation("Döngü durduruldu: {LoopId}, Reason={Reason}",
            request.LoopId, request.Reason);

        return Ok(new { message = "Döngü durduruldu." });
    }

    /// <summary>Döngüyü duraklatır</summary>
    [HttpPost("{loopId}/pause")]
    public async Task<IActionResult> PauseLoop(string loopId)
    {
        var state = await _stateStore.GetStateAsync(loopId);
        if (state == null) return NotFound();

        if (state.State != LoopState.Running)
        {
            return BadRequest(new { error = "Sadece çalışan döngüler duraklatılabilir." });
        }

        await _stateStore.UpdateStateAsync(loopId, LoopState.Paused, state.CurrentIteration);
        return Ok(new { message = "Döngü duraklatıldı." });
    }

    /// <summary>Duraklatılmış döngüyü devam ettirir</summary>
    [HttpPost("{loopId}/resume")]
    public async Task<IActionResult> ResumeLoop(string loopId)
    {
        var state = await _stateStore.GetStateAsync(loopId);
        if (state == null) return NotFound();

        if (state.State != LoopState.Paused)
        {
            return BadRequest(new { error = "Sadece duraklatılmış döngüler devam ettirilebilir." });
        }

        await _stateStore.UpdateStateAsync(loopId, LoopState.Running, state.CurrentIteration);
        return Ok(new { message = "Döngü devam ettiriliyor." });
    }
}
