using System.Text.Json;

namespace SonOfAnton.Shared.Entities;

/// <summary>
/// Custom node script tipi
/// </summary>
public enum CustomNodeScriptType
{
    /// <summary>JavaScript (Jint engine)</summary>
    JavaScript,
    /// <summary>C# Script (Roslyn)</summary>
    CSharpScript
}

/// <summary>
/// Kullanıcı tarafından tanımlanmış özel node tipi.
/// Tenant-scoped: her tenant kendi custom node'larını tanımlar.
/// </summary>
public class CustomNodeDefinitionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tenant kimliği</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Oluşturan kullanıcı</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>Benzersiz node tipi (tenant içinde unique, örn: "custom.math.add")</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Görünen ad</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Lucide icon adı</summary>
    public string Icon { get; set; } = "puzzle";

    /// <summary>Kategori</summary>
    public string Category { get; set; } = "Özel";

    /// <summary>Açıklama</summary>
    public string? Description { get; set; }

    /// <summary>Node rengi (hex)</summary>
    public string Color { get; set; } = "#89b4fa";

    /// <summary>Input port tanımları (JSONB)</summary>
    public JsonDocument? InputsJson { get; set; }

    /// <summary>Output port tanımları (JSONB)</summary>
    public JsonDocument? OutputsJson { get; set; }

    /// <summary>Parametre tanımları (JSONB)</summary>
    public JsonDocument? ParametersJson { get; set; }

    /// <summary>Script dili</summary>
    public CustomNodeScriptType ScriptType { get; set; } = CustomNodeScriptType.JavaScript;

    /// <summary>Çalıştırılacak script kodu</summary>
    public string ScriptCode { get; set; } = "// inputs objesini kullanarak output döndürün\nreturn { output: inputs.input };";

    /// <summary>Versiyon numarası (her güncelleme artırır)</summary>
    public int Version { get; set; } = 1;

    /// <summary>Marketplace'e yayınlanmış mı</summary>
    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ─── Navigation ─────────────────────────────────────────────
    public TenantEntity? Tenant { get; set; }
    public UserEntity? CreatedByUser { get; set; }
}
