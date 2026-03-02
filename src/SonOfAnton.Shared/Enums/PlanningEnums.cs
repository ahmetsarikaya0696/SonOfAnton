namespace SonOfAnton.Shared.Enums;

/// <summary>
/// Planlama belgesi dosya tipi
/// </summary>
public enum PlanFileType
{
    /// <summary>Görev planı belgesi (task_plan.md)</summary>
    TaskPlan,

    /// <summary>Bulgular belgesi (findings.md)</summary>
    Findings,

    /// <summary>İlerleme raporu belgesi (progress.md)</summary>
    Progress
}

/// <summary>
/// Ralph döngü motorunun durumu
/// </summary>
public enum LoopState
{
    /// <summary>Döngü henüz başlamamış</summary>
    Idle,

    /// <summary>Döngü aktif çalışıyor</summary>
    Running,

    /// <summary>Döngü duraklatıldı</summary>
    Paused,

    /// <summary>Döngü başarıyla tamamlandı</summary>
    Completed,

    /// <summary>Döngü hata ile sonlandı</summary>
    Failed,

    /// <summary>Döngü manuel olarak iptal edildi</summary>
    Cancelled
}

/// <summary>
/// Döngüden çıkış nedeni
/// </summary>
public enum LoopExitReason
{
    /// <summary>Maksimum iterasyon sayısına ulaşıldı</summary>
    MaxIterations,

    /// <summary>Tüm görevler tamamlandı</summary>
    TaskCompleted,

    /// <summary>Zaman aşımı</summary>
    Timeout,

    /// <summary>Özel koşul sağlandı</summary>
    CustomCondition,

    /// <summary>Manuel durdurma</summary>
    ManualStop,

    /// <summary>Hata nedeniyle çıkış</summary>
    Error
}
