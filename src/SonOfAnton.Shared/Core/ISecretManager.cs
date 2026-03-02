namespace SonOfAnton.Shared.Core;

/// <summary>
/// API anahtarları ve sırların güvenli yönetimi için arayüz.
/// İlk aşamada DB + EncryptionService tabanlı, gelecekte Azure Key Vault'a geçiş için hazır.
/// </summary>
public interface ISecretManager
{
    /// <summary>
    /// Belirtilen anahtar için saklanan sırrı getirir
    /// </summary>
    Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Yeni bir sır saklar veya mevcut olanı günceller
    /// </summary>
    Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Belirtilen anahtarın sırrını siler
    /// </summary>
    Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Belirtilen anahtarın mevcut olup olmadığını kontrol eder
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}
