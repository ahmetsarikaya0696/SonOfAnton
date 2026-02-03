namespace SonOfAnton.Shared.Models;

/// <summary>
/// Tek bir geli�tirme g�revi
/// </summary>
public class DevTask
{
    public int Id { get; set; }
    
    public required string Description { get; set; }
    
    /// <summary>
    /// Bu g�revin etkiledi�i/olu�turaca�� dosya yolu
    /// </summary>
    public required string TargetFile { get; set; }
    
    /// <summary>
    /// G�revin ba��ml� oldu�u di�er g�rev ID'leri
    /// </summary>
    public List<int> DependsOn { get; set; } = [];
    
    public bool IsCompleted { get; set; }
    
    /// <summary>
    /// G�rev durumu (Pending, InProgress, Completed, Failed)
    /// </summary>
    public DevTaskStatus Status { get; set; } = DevTaskStatus.Pending;
}

/// <summary>
/// G�rev durumu - System.Threading.Tasks.TaskStatus ile �ak��may� �nlemek i�in DevTaskStatus
/// </summary>
public enum DevTaskStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

