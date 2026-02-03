using System.Text.Json;
using Pgvector;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Shared.Entities;

/// <summary>
/// Workflow entity - veritaban�nda saklan�r
/// Nodes ve Connections JSONB olarak tutulur (over-engineering'den ka��nmak i�in)
/// </summary>
public class WorkflowEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public string Name { get; set; } = "�simsiz �� Ak���";
    
    public string? Description { get; set; }
    
    public TriggerType TriggerType { get; set; } = TriggerType.Manual;
    
    /// <summary>
    /// T�m node'lar ve connection'lar JSON olarak
    /// UI (Blazor.Diagrams) zaten JSON serile�tirir, DB'de graph ili�kisi kurmak over-engineering
    /// </summary>
    public JsonDocument? GraphData { get; set; }
    
    public bool IsActive { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public ICollection<ExecutionEntity> Executions { get; set; } = [];
}

/// <summary>
/// Workflow execution kayd�
/// </summary>
public class ExecutionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid WorkflowId { get; set; }
    
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Queued;
    
    /// <summary>
    /// �u an hangi node'da oldu�u
    /// </summary>
    public Guid? CurrentNodeId { get; set; }
    
    /// <summary>
    /// Ad�mlar aras� veri ta��yan JSON blob
    /// </summary>
    public JsonDocument? Memory { get; set; }
    
    /// <summary>
    /// Hata mesaj� (varsa)
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Hangfire Job ID
    /// </summary>
    public string? HangfireJobId { get; set; }
    
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? CompletedAt { get; set; }
    
    // Navigation
    public WorkflowEntity Workflow { get; set; } = null!;
    
    public ICollection<ExecutionLogEntity> Logs { get; set; } = [];
}

/// <summary>
/// Execution log kayd�
/// </summary>
public class ExecutionLogEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid ExecutionId { get; set; }
    
    public Guid? NodeId { get; set; }
    
    public LogLevel Level { get; set; } = LogLevel.Info;
    
    public string Message { get; set; } = string.Empty;
    
    public JsonDocument? Data { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public ExecutionEntity Execution { get; set; } = null!;
}

/// <summary>
/// Vector store tablosu - Semantic Kernel embedding'leri i�in
/// </summary>
public class VectorStoreEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Kaynak koleksiyon ad� (�rn: "documents", "chat-history")
    /// </summary>
    public string Collection { get; set; } = "default";
    
    /// <summary>
    /// Orijinal metin i�eri�i
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Metadata (JSON)
    /// </summary>
    public JsonDocument? Metadata { get; set; }
    
    /// <summary>
    /// Embedding vekt�r� (pgvector)
    /// EF Core migration'da vector(1536) olarak tan�mlanacak
    /// </summary>
    public Vector? Embedding { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

