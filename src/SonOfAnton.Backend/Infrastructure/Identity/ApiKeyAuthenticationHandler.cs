using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Core.Identity;
using System.Text.Json;

namespace SonOfAnton.Backend.Infrastructure.Identity;

/// <summary>
/// X-Api-Key header'ı üzerinden authentication sağlayan handler.
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public string HeaderName { get; set; } = "X-Api-Key";
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly SonOfAntonDbContext _dbContext;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        SonOfAntonDbContext dbContext)
        : base(options, logger, encoder)
    {
        _dbContext = dbContext;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var apiKeyHeaderValues))
        {
            return AuthenticateResult.NoResult();
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();
        if (string.IsNullOrEmpty(providedApiKey))
        {
            return AuthenticateResult.NoResult();
        }

        // Key hashing for DB lookup
        var hashedKey = HashApiKey(providedApiKey);

        var apiKeyEntity = await _dbContext.Set<SonOfAnton.Shared.Entities.ApiKeyEntity>()
            .FirstOrDefaultAsync(k => k.KeyHash == hashedKey && k.IsActive);

        if (apiKeyEntity == null)
        {
            return AuthenticateResult.Fail("Geçersiz API Anahtarı.");
        }

        // Expiry check
        if (apiKeyEntity.ExpiresAt.HasValue && apiKeyEntity.ExpiresAt.Value < DateTime.UtcNow)
        {
            return AuthenticateResult.Fail("API Anahtarının süresi dolmuş.");
        }

        // Update LastUsedAt (fire & forget to not block request)
        _ = UpdateLastUsedAsync(apiKeyEntity.Id);

        // Build claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, apiKeyEntity.Id.ToString()),
            new Claim(ClaimTypes.Name, apiKeyEntity.Name),
            new Claim("TenantId", apiKeyEntity.TenantId ?? string.Empty),
            new Claim("AuthMethod", "ApiKey"),
            new Claim("RateLimit", apiKeyEntity.RateLimitPerMinute.ToString())
        };

        // Add scope permissions
        if (apiKeyEntity.Scopes != null)
        {
            try
            {
                var scopes = JsonSerializer.Deserialize<List<string>>(apiKeyEntity.Scopes);
                if (scopes != null)
                {
                    foreach (var scope in scopes)
                    {
                        claims.Add(new Claim("Permission", scope));
                    }
                }
            }
            catch
            {
                // Json parse error ignore
            }
        }

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationOptions.DefaultScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationOptions.DefaultScheme);

        return AuthenticateResult.Success(ticket);
    }

    private async Task UpdateLastUsedAsync(Guid keyId)
    {
        try
        {
            // Create a new scope / dbContext wrapper or just use direct SQL to avoid scoped context issues in fire&forget
            await _dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE api_keys SET last_used_at = {0} WHERE id = {1}", 
                DateTime.UtcNow, keyId);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to update LastUsedAt for API Key {KeyId}", keyId);
        }
    }

    public static string HashApiKey(string plainKey)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(plainKey);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
