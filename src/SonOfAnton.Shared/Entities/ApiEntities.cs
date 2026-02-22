using System.Text.Json;

namespace SonOfAnton.Shared.Entities;

/// <summary>
/// Bir workflow'un REST API endpoint'i olarak yayınlanma bilgisi.
/// Örn: POST /api/published/order-processor
/// </summary>
public class WorkflowApiEndpointEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Yayınlanan workflow</summary>
    public Guid WorkflowId { get; set; }

    /// <summary>URL route slug, ör: "order-processor" → /api/published/order-processor</summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>Kabul edilen HTTP metodu: GET, POST, PUT, DELETE</summary>
    public string HttpMethod { get; set; } = "POST";

    /// <summary>Endpoint açıklaması (OpenAPI docs için)</summary>
    public string? Description { get; set; }

    /// <summary>Beklenen input parametreleri şeması (JSON Schema formatında)</summary>
    public JsonDocument? InputSchema { get; set; }

    /// <summary>Response şablonu (template engine ile render edilir)</summary>
    public string? OutputTemplate { get; set; }

    /// <summary>Response content type, varsayılan: application/json</summary>
    public string ResponseContentType { get; set; } = "application/json";

    /// <summary>API key zorunlu mu?</summary>
    public bool RequireApiKey { get; set; } = true;

    /// <summary>Execution sonucunu bekleyip response'da dön?</summary>
    public bool WaitForResult { get; set; } = true;

    /// <summary>Sonuç bekleme zaman aşımı (saniye)</summary>
    public int WaitTimeoutSeconds { get; set; } = 30;

    /// <summary>Bu endpoint üzerinden dakikada max istek sayısı</summary>
    public int RateLimitPerMinute { get; set; } = 60;

    public bool IsActive { get; set; } = true;

    /// <summary>Tenant</summary>
    public string? TenantId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public WorkflowEntity? Workflow { get; set; }
    public TenantEntity? Tenant { get; set; }
}

/// <summary>
/// API key entity — dış sistemlerin API'ye erişimi için
/// Key hash'lenerek saklanır, plain key sadece oluşturulurken gösterilir.
/// </summary>
public class ApiKeyEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Anahtar adı (görüntüleme amaçlı)</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Anahtarın ilk 8 karakteri (gösterim için: sk_xxxx...)</summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>Anahtarın SHA256 hash'i (doğrulama için)</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>Ait olduğu tenant</summary>
    public string? TenantId { get; set; }

    /// <summary>Oluşturan kullanıcı</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>İzin verilen scope'lar (JSON array)</summary>
    public JsonDocument? Scopes { get; set; }

    /// <summary>Dakika başına max istek sayısı (0 = sınırsız)</summary>
    public int RateLimitPerMinute { get; set; } = 60;

    public bool IsActive { get; set; } = true;

    /// <summary>Son kullanma tarihi (null = süresiz)</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Son kullanıldığı zaman</summary>
    public DateTime? LastUsedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public TenantEntity? Tenant { get; set; }
    public UserEntity? CreatedByUser { get; set; }
}
