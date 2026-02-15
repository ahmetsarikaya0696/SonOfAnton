namespace SonOfAnton.Backend.Orchestrator;

/// <summary>
/// Orchestrator'�n �al��ma durumlar� (State Machine)
/// </summary>
public enum OrchestratorState
{
    /// <summary>Ba�lang�� durumu, kullan�c� iste�i bekleniyor</summary>
    Idle,
    
    /// <summary>Kullan�c� iste�i al�nd�, Analyst'e g�nderiliyor</summary>
    Init,
    
    /// <summary>Analyst �al���yor, plan olu�turuluyor</summary>
    Planning,
    
    /// <summary>Plan do�rulan�yor (JSON validation)</summary>
    Validating,
    
    /// <summary>Visualizer diyagram olu�turuyor</summary>
    Visualizing,
    
    /// <summary>Task'lar s�rayla i�leniyor</summary>
    Executing,
    
    /// <summary>Kod yaz�l�yor (Coder agent)</summary>
    Coding,
    
    /// <summary>Kod denetleniyor (Auditor agent)</summary>
    Auditing,
    
    /// <summary>Hata d�zeltiliyor (Fixer agent)</summary>
    Fixing,
    
    /// <summary>T�m g�revler tamamland�, kullan�c� onay� bekleniyor</summary>
    AwaitingApproval,
    
    /// <summary>VFS diske yaz�l�yor</summary>
    Flushing,
    
    /// <summary>��lem tamamland�</summary>
    Completed,
    
    /// <summary>Hata durumu</summary>
    Error,

    /// <summary>Workflow analiz ediliyor (Workflow Assist modu)</summary>
    WorkflowAnalyzing,

    /// <summary>Workflow düzenleme planı oluşturuluyor</summary>
    WorkflowPlanning,

    /// <summary>Workflow düzenleme uygulanıyor</summary>
    WorkflowApplying
}

