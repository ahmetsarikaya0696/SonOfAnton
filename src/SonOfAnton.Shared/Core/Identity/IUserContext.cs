namespace SonOfAnton.Shared.Core.Identity;

/// <summary>
/// Mevcut kullanıcının kimlik ve yetki bilgisini sağlayan soyutlama.
/// Domain/Application katmanı bu arayüz üzerinden kullanıcıya erişir.
/// HttpContext, ClaimsPrincipal veya harici kimlik sağlayıcılarına bağımlılık yoktur.
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// Sistemdeki dahili kullanıcı ID'si (UserEntity.Id)
    /// </summary>
    Guid CurrentUserId { get; }

    /// <summary>
    /// Dış kimlik sağlayıcısındaki benzersiz kimlik (OIDC sub/oid claim)
    /// </summary>
    string? ExternalIdentityId { get; }

    /// <summary>
    /// Kullanıcının ait olduğu kurum (tenant) kimliği
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Kullanıcının e-posta adresi
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Kullanıcının görünen adı
    /// </summary>
    string? DisplayName { get; }

    /// <summary>
    /// Kullanıcıya atanmış tüm izinlerin listesi
    /// </summary>
    IReadOnlyList<string> Permissions { get; }

    /// <summary>
    /// Kullanıcı kimlik doğrulaması yapılmış mı?
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Belirli bir izne sahip mi?
    /// </summary>
    bool HasPermission(string permission);
}
