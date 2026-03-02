using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Core;

namespace SonOfAnton.Backend.Services.AI;

/// <summary>
/// API anahtarlarının güvenli yönetimi.
/// Öncelik sırası:
///   1. appsettings.json / ortam değişkenleri (IConfiguration)
///   2. DB'deki CredentialEntity tablosu (EncryptionService ile şifreli)
/// Gelecekte Azure Key Vault'a geçiş için ISecretManager arayüzü korunur.
/// </summary>
public class SecretManagerService : ISecretManager
{
    private readonly IConfiguration _configuration;
    private readonly SonOfAntonDbContext _dbContext;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<SecretManagerService> _logger;

    /// <summary>
    /// Configuration'daki secret key prefix'i
    /// </summary>
    private const string ConfigPrefix = "Secrets:";

    public SecretManagerService(
        IConfiguration configuration,
        SonOfAntonDbContext dbContext,
        IEncryptionService encryptionService,
        ILogger<SecretManagerService> logger)
    {
        _configuration = configuration;
        _dbContext = dbContext;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        // 1. Önce configuration'dan dene (appsettings, env vars, user secrets)
        var configValue = _configuration[$"{ConfigPrefix}{key}"]
                       ?? _configuration[key]; // Kısa format da desteklenir

        if (!string.IsNullOrWhiteSpace(configValue))
        {
            _logger.LogDebug("Secret '{Key}' configuration'dan okundu.", key);
            return configValue;
        }

        // 2. DB'den dene (CredentialEntity)
        try
        {
            var credential = await _dbContext.Credentials
                .FirstOrDefaultAsync(c => c.SystemName == key, cancellationToken);

            if (credential != null)
            {
                var decrypted = _encryptionService.Decrypt(credential.EncryptedData);
                _logger.LogDebug("Secret '{Key}' veritabanından okundu.", key);
                return decrypted;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Secret '{Key}' DB'den okunurken hata oluştu.", key);
        }

        _logger.LogDebug("Secret '{Key}' bulunamadı.", key);
        return null;
    }

    /// <inheritdoc />
    public async Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var encrypted = _encryptionService.Encrypt(value);

        var existing = await _dbContext.Credentials
            .FirstOrDefaultAsync(c => c.SystemName == key, cancellationToken);

        if (existing != null)
        {
            existing.EncryptedData = encrypted;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _dbContext.Credentials.Add(new Shared.Entities.CredentialEntity
            {
                Name = key,
                SystemName = key,
                EncryptedData = encrypted,
                Description = $"Auto-created by SecretManager: {key}"
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Secret '{Key}' başarıyla kaydedildi.", key);
    }

    /// <inheritdoc />
    public async Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        var credential = await _dbContext.Credentials
            .FirstOrDefaultAsync(c => c.SystemName == key, cancellationToken);

        if (credential != null)
        {
            _dbContext.Credentials.Remove(credential);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Secret '{Key}' silindi.", key);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        // Configuration'da var mı?
        var configValue = _configuration[$"{ConfigPrefix}{key}"]
                       ?? _configuration[key];
        if (!string.IsNullOrWhiteSpace(configValue))
            return true;

        // DB'de var mı?
        return await _dbContext.Credentials
            .AnyAsync(c => c.SystemName == key, cancellationToken);
    }
}
