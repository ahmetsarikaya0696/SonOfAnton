namespace SonOfAnton.Shared.Entities;

/// <summary>
/// Sistemde kullanıcının kendi yarattığı (Zapier Interface-like) Chatbot'lar
/// </summary>
public class ChatbotEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    
    public string? Description { get; set; }
    
    /// <summary>
    /// Chatbot'un sistem promptu (Kişiliği, ne yapması gerektiği)
    /// </summary>
    public string SystemPrompt { get; set; } = "Sen yardımcı bir asistansın.";
    
    /// <summary>
    /// Kullanılacak model (Örn: gpt-4o, gpt-4o-mini)
    /// </summary>
    public string Model { get; set; } = "gpt-4o";
    
    /// <summary>
    /// Bu chatbot hangi workspace'e ait?
    /// </summary>
    public Guid WorkspaceId { get; set; }
    public WorkspaceEntity Workspace { get; set; } = null!;
    
    /// <summary>
    /// Gizlilik durumu (Public/Private)
    /// </summary>
    public bool IsPublic { get; set; } = false;
    
    // İlişkiler
    public ICollection<ChatbotSessionEntity> Sessions { get; set; } = new List<ChatbotSessionEntity>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Belirli bir kullanıcı veya anonim kişinin chatbot ile olan sohbet oturumu
/// </summary>
public class ChatbotSessionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChatbotId { get; set; }
    public ChatbotEntity Chatbot { get; set; } = null!;
    
    /// <summary>
    /// Eğer login olmuş bir kullanıcı is, UserId. Yoksa null (anonim).
    /// </summary>
    public Guid? UserId { get; set; }
    public UserEntity? User { get; set; }
    
    public string Title { get; set; } = "Yeni Sohbet";
    
    /// <summary>
    /// Sohbetin Semantic Kernel ChatHistory formatında JSON ile saklanması
    /// </summary>
    public string HistoryJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
