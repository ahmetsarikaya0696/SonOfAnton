namespace SonOfAnton.Shared.Models;

/// <summary>
/// Analyst ajan�n�n �retti�i proje plan� (Strict JSON format�nda)
/// </summary>
public class ProjectPlan
{
    public required string ProjectName { get; set; }
    
    /// <summary>
    /// Mimari a��klamas� (Console, WebAPI, Blazor, etc.)
    /// </summary>
    public required string Architecture { get; set; }
    
    /// <summary>
    /// Projenin a��klamas�
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Yap�lacak g�revler listesi (s�ral�)
    /// </summary>
    public List<DevTask> Tasks { get; set; } = [];
    
    /// <summary>
    /// Mimari kurallar ve k�s�tlamalar
    /// </summary>
    public List<string> ArchitectureRules { get; set; } = [];
}

