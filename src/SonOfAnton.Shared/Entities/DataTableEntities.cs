using System.Text.Json;

namespace SonOfAnton.Shared.Entities;

/// <summary>
/// Data Table Entity - Dinamik tabloların yapı şemasını tutar. Zapier Tables benzeri tablo tanımı.
/// </summary>
public class DataTableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public string Name { get; set; } = "İsimsiz Tablo";
    
    public string? Description { get; set; }

    /// <summary>
    /// Tablodaki sütun tanımlarını tutan JSON listesi.
    /// Örnek: [ { "id": "col_1", "name": "Ad", "type": "text", "isRequired": true } ]
    /// </summary>
    public JsonDocument? ColumnsJson { get; set; }

    // Multi-Tenant & Workspace
    public string? TenantId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public TenantEntity? Tenant { get; set; }
    public WorkspaceEntity? Workspace { get; set; }
    public UserEntity? CreatedByUser { get; set; }
    
    public ICollection<DataRowEntity> Rows { get; set; } = [];
    public ICollection<DataFormEntity> Forms { get; set; } = [];
}

/// <summary>
/// Data Row Entity - Data Table içindeki tek bir satırı tutar.
/// </summary>
public class DataRowEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid DataTableId { get; set; }
    
    /// <summary>
    /// Sütun ID'leri ile değerleri eşleştiren JSON verisi.
    /// Örnek: { "col_1": "Ahmet", "col_2": "ahmet@example.com" }
    /// </summary>
    public JsonDocument? ValuesJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public DataTableEntity DataTable { get; set; } = null!;
}

/// <summary>
/// Data Form Entity - Data Table'a dışarıdan (public) veri eklemek için oluşturulan form ayarları.
/// </summary>
public class DataFormEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid DataTableId { get; set; }
    
    public string Name { get; set; } = "İsimsiz Form";
    
    public string? Description { get; set; }
    
    /// <summary>
    /// Public erişim için benzersiz form slug/url kısmı.
    /// </summary>
    public string Slug { get; set; } = Guid.NewGuid().ToString("N")[..12];
    
    /// <summary>
    /// Formda hangi sütunların nasıl gösterileceğini belirten JSON ayarları.
    /// Örnek: [ { "columnId": "col_1", "label": "Lütfen Adınızı Girin", "isVisible": true, "isRequired": true } ]
    /// </summary>
    public JsonDocument? FieldsJson { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Tema ayarları (arka plan rengi, buton rengi vb. basit konfigürasyon)
    public JsonDocument? ThemeJson { get; set; }

    // Multi-Tenant & Workspace
    public string? TenantId { get; set; }
    public Guid? WorkspaceId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public DataTableEntity DataTable { get; set; } = null!;
    public TenantEntity? Tenant { get; set; }
    public WorkspaceEntity? Workspace { get; set; }
}
