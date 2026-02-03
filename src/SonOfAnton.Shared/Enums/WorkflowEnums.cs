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
    /// <summary>S�raya al�nd�, hen�z ba�lamad�</summary>
    Queued,
    
    /// <summary>�al���yor</summary>
    Running,
    
    /// <summary>Ba�ar�yla tamamland�</summary>
    Completed,
    
    /// <summary>Hata ile ba�ar�s�z oldu</summary>
    Failed,
    
    /// <summary>�ptal edildi</summary>
    Cancelled
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

