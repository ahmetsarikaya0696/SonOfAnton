namespace SonOfAnton.Shared.DTOs;

// ─── Custom Node DTOs ───────────────────────────────────────────

/// <summary>
/// Custom node tanımı oluşturma/güncelleme isteği
/// </summary>
public class CreateCustomNodeDto
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "puzzle";
    public string Category { get; set; } = "Özel";
    public string? Description { get; set; }
    public string Color { get; set; } = "#89b4fa";
    public List<NodePortDto> Inputs { get; set; } = [];
    public List<NodePortDto> Outputs { get; set; } = [];
    public List<NodeParameterDto> Parameters { get; set; } = [];
    public string ScriptType { get; set; } = "JavaScript";
    public string ScriptCode { get; set; } = "return { output: inputs.input };";
}

/// <summary>
/// Custom node tanımı detay yanıtı
/// </summary>
public class CustomNodeDto
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = string.Empty;
    public List<NodePortDto> Inputs { get; set; } = [];
    public List<NodePortDto> Outputs { get; set; } = [];
    public List<NodeParameterDto> Parameters { get; set; } = [];
    public string ScriptType { get; set; } = string.Empty;
    public string ScriptCode { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Custom node test çalıştırma isteği
/// </summary>
public class TestCustomNodeDto
{
    public Dictionary<string, object?> Inputs { get; set; } = [];
    public Dictionary<string, object?> Parameters { get; set; } = [];
}

/// <summary>
/// Custom node test sonucu
/// </summary>
public class TestCustomNodeResultDto
{
    public bool Success { get; set; }
    public Dictionary<string, object?>? Outputs { get; set; }
    public string? Error { get; set; }
    public double ExecutionTimeMs { get; set; }
}

// ─── Marketplace DTOs ───────────────────────────────────────────

/// <summary>
/// Marketplace öğesi yayınlama isteği
/// </summary>
public class PublishMarketplaceItemDto
{
    public string ItemType { get; set; } = "CustomNode"; // CustomNode, WorkflowTemplate
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Icon { get; set; } = "package";
    public string Category { get; set; } = "Genel";
    public List<string> Tags { get; set; } = [];
    /// <summary>
    /// CustomNode için: CustomNodeDefinition ID
    /// WorkflowTemplate için: Workflow ID
    /// </summary>
    public Guid? SourceId { get; set; }
    public string Version { get; set; } = "1.0.0";
}

/// <summary>
/// Marketplace öğesi detay yanıtı
/// </summary>
public class MarketplaceItemDto
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string PublishedByUserName { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string Version { get; set; } = string.Empty;
    public int DownloadCount { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public bool IsInstalled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Marketplace arama isteği
/// </summary>
public class MarketplaceSearchDto
{
    public string? Query { get; set; }
    public string? Category { get; set; }
    public string? ItemType { get; set; }
    public string SortBy { get; set; } = "newest"; // newest, popular, rating
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Sayfalanmış marketplace sonucu
/// </summary>
public class MarketplaceSearchResultDto
{
    public List<MarketplaceItemDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>
/// Marketplace değerlendirme ekleme isteği
/// </summary>
public class CreateReviewDto
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

/// <summary>
/// Marketplace değerlendirmesi yanıtı
/// </summary>
public class MarketplaceReviewDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
