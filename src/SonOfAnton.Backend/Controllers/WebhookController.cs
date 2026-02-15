using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Backend.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using SonOfAnton.Shared.Core.Identity;

namespace SonOfAnton.Backend.Controllers;

/// <summary>
/// Webhook endpoint'leri - dış servislerden workflow tetikleme
/// </summary>
[ApiController]
[Route("api/webhook")]
[Authorize(Policy = Permissions.WebhooksManage)]
public class WebhookController : ControllerBase
{
    private readonly SonOfAntonDbContext _dbContext;
    private readonly WorkflowJobService _jobService;
    private readonly ILogger<WebhookController> _logger;
    
    public WebhookController(
        SonOfAntonDbContext dbContext,
        WorkflowJobService jobService,
        ILogger<WebhookController> logger)
    {
        _dbContext = dbContext;
        _jobService = jobService;
        _logger = logger;
    }
    
    /// <summary>
    /// Webhook ile workflow tetikle
    /// URL: POST /api/webhook/{webhookId}
    /// </summary>
    [HttpPost("{webhookId}")]
    [AllowAnonymous] // Webhook trigger'ı dış sistemler tarafından çağrılır
    public async Task<IActionResult> TriggerWebhook(
        string webhookId,
        [FromBody] JsonElement? payload = null)
    {
        _logger.LogInformation("Webhook alındı: {WebhookId}", webhookId);
        
        // Webhook'u bul
        var webhook = await _dbContext.Webhooks
            .Include(w => w.Workflow)
            .FirstOrDefaultAsync(w => w.WebhookId == webhookId && w.IsActive);
        
        if (webhook == null)
        {
            _logger.LogWarning("Webhook bulunamadı veya aktif değil: {WebhookId}", webhookId);
            return NotFound(new { error = "Webhook not found or inactive" });
        }
        
        // Rate limiting kontrolü
        if (webhook.LastTriggeredAt.HasValue)
        {
            var timeSinceLastTrigger = DateTime.UtcNow - webhook.LastTriggeredAt.Value;
            if (timeSinceLastTrigger < TimeSpan.FromSeconds(webhook.MinIntervalSeconds))
            {
                _logger.LogWarning("Webhook rate limited: {WebhookId}", webhookId);
                return StatusCode(429, new { error = "Too many requests", retryAfterSeconds = webhook.MinIntervalSeconds - timeSinceLastTrigger.TotalSeconds });
            }
        }
        
        // Webhook signature kontrolü (opsiyonel)
        if (!string.IsNullOrEmpty(webhook.Secret))
        {
            var signature = Request.Headers["X-Webhook-Signature"].FirstOrDefault();
            if (!ValidateSignature(payload, webhook.Secret, signature))
            {
                _logger.LogWarning("Webhook signature geçersiz: {WebhookId}", webhookId);
                return Unauthorized(new { error = "Invalid signature" });
            }
        }
        
        // Trigger data hazırla
        var triggerData = new Dictionary<string, object?>
        {
            ["webhook_id"] = webhookId,
            ["triggered_at"] = DateTime.UtcNow,
            ["source_ip"] = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ["headers"] = GetSelectedHeaders(),
            ["payload"] = payload.HasValue ? JsonSerializer.Deserialize<object>(payload.Value.GetRawText()) : null
        };
        
        // Workflow'u tetikle
        var executionId = _jobService.EnqueueWorkflow(webhook.WorkflowId, triggerData);
        
        // Webhook istatistiklerini güncelle
        webhook.LastTriggeredAt = DateTime.UtcNow;
        webhook.TriggerCount++;
        await _dbContext.SaveChangesAsync();
        
        _logger.LogInformation(
            "Webhook workflow tetikledi: {WebhookId} -> Workflow: {WorkflowId}, Execution: {ExecutionId}",
            webhookId, webhook.WorkflowId, executionId);
        
        return Ok(new
        {
            success = true,
            executionId = executionId,
            message = "Workflow triggered successfully"
        });
    }
    
    /// <summary>
    /// Yeni webhook oluştur
    /// </summary>
    [HttpPost("create")]
    public async Task<IActionResult> CreateWebhook([FromBody] CreateWebhookRequest request)
    {
        // Workflow'un var olduğunu kontrol et
        var workflow = await _dbContext.Workflows.FindAsync(request.WorkflowId);
        if (workflow == null)
        {
            return NotFound(new { error = "Workflow not found" });
        }
        
        // Benzersiz webhook ID oluştur
        var webhookId = GenerateWebhookId();
        
        // Secret oluştur (opsiyonel)
        string? secret = null;
        if (request.GenerateSecret)
        {
            secret = GenerateSecret();
        }
        
        var webhook = new WebhookEntity
        {
            WebhookId = webhookId,
            WorkflowId = request.WorkflowId,
            Name = request.Name ?? $"Webhook for {workflow.Name}",
            Description = request.Description,
            Secret = secret,
            IsActive = true,
            MinIntervalSeconds = request.MinIntervalSeconds ?? 1
        };
        
        _dbContext.Webhooks.Add(webhook);
        await _dbContext.SaveChangesAsync();
        
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        
        return Ok(new
        {
            id = webhook.Id,
            webhookId = webhook.WebhookId,
            url = $"{baseUrl}/api/webhook/{webhook.WebhookId}",
            secret = secret,
            name = webhook.Name,
            isActive = webhook.IsActive
        });
    }
    
    /// <summary>
    /// Webhook listele
    /// </summary>
    [HttpGet("list/{workflowId:guid}")]
    public async Task<IActionResult> ListWebhooks(Guid workflowId)
    {
        var webhooks = await _dbContext.Webhooks
            .Where(w => w.WorkflowId == workflowId)
            .Select(w => new
            {
                w.Id,
                w.WebhookId,
                w.Name,
                w.Description,
                w.IsActive,
                w.TriggerCount,
                w.LastTriggeredAt,
                w.CreatedAt,
                Url = $"{Request.Scheme}://{Request.Host}/api/webhook/{w.WebhookId}"
            })
            .ToListAsync();
        
        return Ok(webhooks);
    }
    
    /// <summary>
    /// Webhook sil
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteWebhook(Guid id)
    {
        var webhook = await _dbContext.Webhooks.FindAsync(id);
        if (webhook == null)
        {
            return NotFound();
        }
        
        _dbContext.Webhooks.Remove(webhook);
        await _dbContext.SaveChangesAsync();
        
        return NoContent();
    }
    
    /// <summary>
    /// Webhook aktif/pasif yap
    /// </summary>
    [HttpPatch("{id:guid}/toggle")]
    public async Task<IActionResult> ToggleWebhook(Guid id)
    {
        var webhook = await _dbContext.Webhooks.FindAsync(id);
        if (webhook == null)
        {
            return NotFound();
        }
        
        webhook.IsActive = !webhook.IsActive;
        await _dbContext.SaveChangesAsync();
        
        return Ok(new { isActive = webhook.IsActive });
    }
    
    private bool ValidateSignature(JsonElement? payload, string secret, string? signature)
    {
        if (string.IsNullOrEmpty(signature)) return false;
        
        var payloadString = payload.HasValue ? payload.Value.GetRawText() : "";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadString));
        var expectedSignature = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
        
        return signature.Equals(expectedSignature, StringComparison.OrdinalIgnoreCase);
    }
    
    private Dictionary<string, string> GetSelectedHeaders()
    {
        var selectedHeaders = new[] { "Content-Type", "User-Agent", "X-Request-Id", "X-Forwarded-For" };
        return Request.Headers
            .Where(h => selectedHeaders.Contains(h.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(h => h.Key, h => h.Value.ToString());
    }
    
    private static string GenerateWebhookId()
    {
        // URL-safe benzersiz ID: wh_xxxxxxxxxxxxx
        var bytes = RandomNumberGenerator.GetBytes(12);
        return "wh_" + Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
    
    private static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

/// <summary>
/// Webhook oluşturma isteği
/// </summary>
public class CreateWebhookRequest
{
    public Guid WorkflowId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool GenerateSecret { get; set; } = true;
    public int? MinIntervalSeconds { get; set; }
}

/// <summary>
/// Webhook entity
/// </summary>
public class WebhookEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string WebhookId { get; set; } = string.Empty;
    public Guid WorkflowId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Secret { get; set; }
    public bool IsActive { get; set; } = true;
    public int MinIntervalSeconds { get; set; } = 1;
    public int TriggerCount { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public SonOfAnton.Shared.Entities.WorkflowEntity? Workflow { get; set; }
}
