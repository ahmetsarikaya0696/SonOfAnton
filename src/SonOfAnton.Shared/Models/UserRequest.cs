namespace SonOfAnton.Shared.Models;

/// <summary>
/// Kullan�c�n�n backend'e g�nderdi�i istek
/// </summary>
public class UserRequest
{
    /// <summary>
    /// Kullan�c�n�n do�al dilde iste�i
    /// </summary>
    public required string Prompt { get; set; }
    
    /// <summary>
    /// Session ID (opsiyonel, devam eden session i�in)
    /// </summary>
    public string? SessionId { get; set; }
}

