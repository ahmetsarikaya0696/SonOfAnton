using System.Text.Json;

namespace SonOfAnton.Shared.Entities;

/// <summary>
/// Sistemde saklanan, şifrelenmiş kimlik bilgisi (API Key, OAuth2 Token vb.)
/// </summary>
public class CredentialEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Kimlik bilgisinin adı (Örn: "Benim Google Hesabım")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Sağlayıcı veya sistem adı (Örn: "google", "github", "custom_api")
    /// </summary>
    public string SystemName { get; set; } = string.Empty;

    /// <summary>
    /// Açıklama (Opsiyonel)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Şifrelenmiş JSON verisi (İçerisinde access_token, refresh_token, api_key gibi değerler bulunur)
    /// </summary>
    public string EncryptedData { get; set; } = string.Empty;

    /// <summary>
    /// Ait olduğu tenant
    /// </summary>
    public string TenantId { get; set; } = string.Empty;
    
    /// <summary>
    /// Oluşturan kullanıcı
    /// </summary>
    public Guid? UserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public TenantEntity? Tenant { get; set; }
    public UserEntity? User { get; set; }
}

/// <summary>
/// Kullanıcının veya Tenant'ın dış bir sağlayıcıdaki bağlı hesabı
/// </summary>
public class LinkedAccountEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Dış sağlayıcı adı (Örn: "google", "github")
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Dış sağlayıcıdaki benzersiz kullanıcı kimliği (Sub/ID)
    /// </summary>
    public string ProviderUserId { get; set; } = string.Empty;

    /// <summary>
    /// Ait olduğu kullanıcı
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Ait olduğu tenant
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Bağlı hesabın yetki (token) bilgilerini tutan Credential referansı
    /// </summary>
    public Guid CredentialId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public UserEntity? User { get; set; }
    public TenantEntity? Tenant { get; set; }
    public CredentialEntity? Credential { get; set; }
}
