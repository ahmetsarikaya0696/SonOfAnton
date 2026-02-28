namespace SonOfAnton.Shared.DTOs;

public class CredentialDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SystemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateCredentialRequest
{
    public string Name { get; set; } = string.Empty;
    public string SystemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    /// <summary>Raw JSON verisi (Controller'da şifrelenecek)</summary>
    public string RawData { get; set; } = string.Empty;
}

public class UpdateCredentialRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RawData { get; set; }
}

public class LinkedAccountDto
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class OAuthExchangeRequest
{
    public string Code { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
}
