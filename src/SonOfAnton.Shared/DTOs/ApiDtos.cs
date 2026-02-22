namespace SonOfAnton.Shared.DTOs;

// ─── Workflow API Endpoint DTOs ────────────────────────────────

/// <summary>
/// Yayınlanan workflow API endpoint bilgisi
/// </summary>
public class WorkflowApiEndpointDto
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public string? WorkflowName { get; set; }
    public string Route { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = "POST";
    public string? Description { get; set; }
    public object? InputSchema { get; set; }
    public string? OutputTemplate { get; set; }
    public string ResponseContentType { get; set; } = "application/json";
    public bool RequireApiKey { get; set; }
    public bool WaitForResult { get; set; }
    public int WaitTimeoutSeconds { get; set; }
    public int RateLimitPerMinute { get; set; }
    public bool IsActive { get; set; }
    public string? FullUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Workflow API endpoint oluşturma isteği
/// </summary>
public class CreateWorkflowApiEndpointRequest
{
    public Guid WorkflowId { get; set; }

    /// <summary>URL route slug (benzersiz olmalı), ör: "order-processor"</summary>
    public string Route { get; set; } = string.Empty;

    public string HttpMethod { get; set; } = "POST";
    public string? Description { get; set; }

    /// <summary>Beklenen input parametreleri JSON Schema</summary>
    public object? InputSchema { get; set; }

    /// <summary>Response template (Mustache-like)</summary>
    public string? OutputTemplate { get; set; }

    public string ResponseContentType { get; set; } = "application/json";
    public bool RequireApiKey { get; set; } = true;
    public bool WaitForResult { get; set; } = true;
    public int WaitTimeoutSeconds { get; set; } = 30;
    public int RateLimitPerMinute { get; set; } = 60;
}

/// <summary>
/// Workflow API endpoint güncelleme isteği
/// </summary>
public class UpdateWorkflowApiEndpointRequest
{
    public string? Route { get; set; }
    public string? HttpMethod { get; set; }
    public string? Description { get; set; }
    public object? InputSchema { get; set; }
    public string? OutputTemplate { get; set; }
    public string? ResponseContentType { get; set; }
    public bool? RequireApiKey { get; set; }
    public bool? WaitForResult { get; set; }
    public int? WaitTimeoutSeconds { get; set; }
    public int? RateLimitPerMinute { get; set; }
    public bool? IsActive { get; set; }
}

// ─── API Key DTOs ──────────────────────────────────────────────

/// <summary>
/// API key görüntüleme (hash gösterilmez)
/// </summary>
public class ApiKeyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = [];
    public int RateLimitPerMinute { get; set; }
    public bool IsActive { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Yeni API key oluşturma isteği
/// </summary>
public class CreateApiKeyRequest
{
    public string Name { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = [];
    public int RateLimitPerMinute { get; set; } = 60;
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// API key oluşturma yanıtı — plain key SADECE burada gösterilir
/// </summary>
public class CreateApiKeyResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>⚠️ Tam API anahtarı — sadece bir kez gösterilir, bir daha erişilemez</summary>
    public string PlainKey { get; set; } = string.Empty;

    public List<string> Scopes { get; set; } = [];
    public int RateLimitPerMinute { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// API key scope güncelleme isteği
/// </summary>
public class UpdateApiKeyScopesRequest
{
    public List<string> Scopes { get; set; } = [];
}

// ─── Webhook Response Template DTOs ────────────────────────────

/// <summary>
/// Webhook response template bilgisi
/// </summary>
public class WebhookResponseTemplateDto
{
    /// <summary>Response body template (Mustache-like: {{result.data}})</summary>
    public string? ResponseTemplate { get; set; }

    /// <summary>Response content type</summary>
    public string ResponseContentType { get; set; } = "application/json";

    /// <summary>Execution sonucunu bekle?</summary>
    public bool WaitForResult { get; set; }

    /// <summary>Bekleme zaman aşımı (saniye)</summary>
    public int WaitTimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Webhook response template güncelleme isteği
/// </summary>
public class UpdateWebhookResponseTemplateRequest
{
    public string? ResponseTemplate { get; set; }
    public string? ResponseContentType { get; set; }
    public bool? WaitForResult { get; set; }
    public int? WaitTimeoutSeconds { get; set; }
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
    public string? ResponseTemplate { get; set; }
    public string? ResponseContentType { get; set; }
    public bool? WaitForResult { get; set; }
    public int? WaitTimeoutSeconds { get; set; }
}

public class ToggleWebhookResponse
{
    public bool IsActive { get; set; }
}

public class WebhookDto
{
    public Guid Id { get; set; }
    public string WebhookId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int TriggerCount { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Url { get; set; } = string.Empty;
}
