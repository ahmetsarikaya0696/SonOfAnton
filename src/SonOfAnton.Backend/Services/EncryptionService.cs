using System.Security.Cryptography;
using System.Text;

namespace SonOfAnton.Backend.Services;

public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

/// <summary>Versioned authenticated encryption. Legacy data requires an explicit migration key.</summary>
public sealed class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private readonly byte[]? _legacyKey;
    public EncryptionService(IConfiguration config)
    {
        var key = config["Encryption:Key"] ?? throw new InvalidOperationException("Encryption:Key must contain a base64-encoded 32-byte key.");
        _key = Convert.FromBase64String(key);
        if (_key.Length != 32) throw new InvalidOperationException("Encryption:Key must decode to exactly 32 bytes.");
        if (config["Encryption:LegacyKey"] is { Length: > 0 } legacy)
        {
            _legacyKey = Encoding.UTF8.GetBytes(legacy);
            if (_legacyKey.Length != 32) throw new InvalidOperationException("LegacyKey must be exactly 32 UTF-8 bytes.");
        }
    }
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(plainText);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        return "v1:" + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;
        if (!cipherText.StartsWith("v1:", StringComparison.Ordinal))
        {
            if (_legacyKey is null) throw new CryptographicException("Legacy ciphertext requires an explicit migration key.");
            var data = Convert.FromBase64String(cipherText);
            using var legacy = Aes.Create();
            legacy.Key = _legacyKey;
            return Encoding.UTF8.GetString(legacy.DecryptCbc(data.AsSpan(16), data.AsSpan(0,16)));
        }
        var bytes = Convert.FromBase64String(cipherText[3..]);
        if (bytes.Length < 28) throw new CryptographicException("Invalid ciphertext.");
        var plain = new byte[bytes.Length - 28];
        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(bytes.AsSpan(0,12), bytes.AsSpan(28), bytes.AsSpan(12,16), plain);
        return Encoding.UTF8.GetString(plain);
    }
}
