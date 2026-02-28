using System.Text.Json;

namespace SonOfAnton.Shared.Models;

// ─── Table ────────────────────────────────────────────────────────
public class DataTableDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JsonDocument? ColumnsJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    // Statistics
    public int RowCount { get; set; }
}

public class DataTableCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JsonDocument? ColumnsJson { get; set; }
}

public class DataTableUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JsonDocument? ColumnsJson { get; set; }
}

// Kolon tanımlarının yapısını Type-Safe kullanmak isteyen Blazor/Backend için:
public class DataTableColumnDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Tip: text, number, boolean, date, url vs.
    /// </summary>
    public string Type { get; set; } = "text"; 
    public bool IsRequired { get; set; }
}

// ─── Row ──────────────────────────────────────────────────────────
public class DataRowDto
{
    public Guid Id { get; set; }
    public Guid DataTableId { get; set; }
    public JsonDocument? ValuesJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DataRowCreateDto
{
    public JsonDocument? ValuesJson { get; set; }
}

public class DataRowUpdateDto
{
    public JsonDocument? ValuesJson { get; set; }
}

// ─── Form ─────────────────────────────────────────────────────────
public class DataFormDto
{
    public Guid Id { get; set; }
    public Guid DataTableId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Slug { get; set; } = string.Empty;
    public JsonDocument? FieldsJson { get; set; }
    public JsonDocument? ThemeJson { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DataFormCreateDto
{
    public Guid DataTableId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JsonDocument? FieldsJson { get; set; }
    public JsonDocument? ThemeJson { get; set; }
    public bool IsActive { get; set; } = true;
}

public class DataFormUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JsonDocument? FieldsJson { get; set; }
    public JsonDocument? ThemeJson { get; set; }
    public bool IsActive { get; set; }
}

public class DataFormFieldDefinition
{
    public string ColumnId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Public form üzerinden kullanıcının gönderdiği veri
/// </summary>
public class DataFormSubmitDto
{
    // Dictionary<ColumnId, Value> olarak alınır, JsonDocument'e çevrilip DataRow'a basılır
    public Dictionary<string, object?> Values { get; set; } = [];
}
