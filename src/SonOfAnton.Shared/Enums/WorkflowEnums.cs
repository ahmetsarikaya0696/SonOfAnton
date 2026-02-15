namespace SonOfAnton.Shared.Enums;

/// <summary>
/// Workflow tetiklenme tipi
/// </summary>
public enum TriggerType
{
    /// <summary>Manuel olarak tetiklenir</summary>
    Manual,
    
    /// <summary>Webhook ile tetiklenir</summary>
    Webhook,
    
    /// <summary>Zamanlay�c� ile tetiklenir</summary>
    Scheduled
}

/// <summary>
/// Workflow execution durumu
/// </summary>
public enum ExecutionStatus
{
    /// <summary>Sıraya alındı, henüz başlamadı</summary>
    Queued,

    /// <summary>Çalışıyor</summary>
    Running,

    /// <summary>Başarıyla tamamlandı</summary>
    Completed,

    /// <summary>Hata ile başarısız oldu</summary>
    Failed,

    /// <summary>İptal edildi</summary>
    Cancelled,

    /// <summary>Duraklatıldı</summary>
    Paused,

    /// <summary>Devam ettiriliyor</summary>
    Resuming
}

/// <summary>
/// Node durumu
/// </summary>
public enum NodeStatus
{
    /// <summary>Beklemede</summary>
    Idle,
    
    /// <summary>�al���yor</summary>
    Running,
    
    /// <summary>Ba�ar�l�</summary>
    Success,
    
    /// <summary>Hatal�</summary>
    Error,
    
    /// <summary>Atland�</summary>
    Skipped
}

/// <summary>
/// Log seviyesi
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// Subflow tipi
/// </summary>
public enum SubflowType
{
    Loop,
    Parallel,
    Conditional,
    ErrorHandler
}

/// <summary>
/// Orchestrator çalışma modu
/// </summary>
public enum OrchestratorMode
{
    CodeGeneration,
    WorkflowAssistant,
    Hybrid
}

/// <summary>
/// Workflow değişken tipi
/// </summary>
public enum VariableType
{
    String,
    Number,
    Boolean,
    Json,
    Secret
}

/// <summary>
/// Değişken kapsamı
/// </summary>
public enum VariableScope
{
    Global,
    Execution,
    Node
}

/// <summary>
/// A2A Task durumu
/// </summary>
public enum A2ATaskState
{
    Submitted,
    Working,
    InputRequired,
    Completed,
    Failed,
    Canceled
}

/// <summary>
/// Artifact tipi
/// </summary>
public enum ArtifactType
{
    Yaml,
    Mermaid,
    Json,
    Markdown
}

/// <summary>
/// Paralel join stratejisi
/// </summary>
public enum JoinStrategy
{
    WaitAll,
    WaitAny,
    WaitN
}

/// <summary>
/// Paralel hata yönetimi
/// </summary>
public enum ParallelErrorPolicy
{
    FailFast,
    Collect,
    Ignore
}

