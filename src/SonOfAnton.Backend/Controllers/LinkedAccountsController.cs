using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;
using System.Text.Json;
using System.Net.Http.Headers;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/linked-accounts")]
[Authorize]
public class LinkedAccountsController : ControllerBase
{
    private readonly SonOfAntonDbContext _dbContext;
    private readonly IUserContext _userContext;
    private readonly IEncryptionService _encryptionService;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public LinkedAccountsController(
        SonOfAntonDbContext dbContext,
        IUserContext userContext,
        IEncryptionService encryptionService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _userContext = userContext;
        _encryptionService = encryptionService;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var tenantId = _userContext.TenantId;
        var userId = _userContext.CurrentUserId;
        
        var accounts = await _dbContext.Set<LinkedAccountEntity>()
            .Where(a => a.TenantId == tenantId && a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new LinkedAccountDto
            {
                Id = a.Id,
                Provider = a.Provider,
                ProviderUserId = a.ProviderUserId,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        return Ok(accounts);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tenantId = _userContext.TenantId;
        var userId = _userContext.CurrentUserId;

        var account = await _dbContext.Set<LinkedAccountEntity>()
            .Include(a => a.Credential)
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && a.UserId == userId);

        if (account == null) return NotFound();

        if (account.Credential != null)
        {
            _dbContext.Remove(account.Credential);
        }

        _dbContext.Remove(account);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }
    
    [HttpPost("exchange/{provider}")]
    public async Task<IActionResult> ExchangeCode(string provider, [FromBody] OAuthExchangeRequest request)
    {
        var tenantId = _userContext.TenantId;
        var userId = _userContext.CurrentUserId;

        // In a real production scenario, these would come from config (e.g., KeyVault)
        var clientId = _configuration[$"OAuthProviders:{provider}:ClientId"] ?? "dummy_client_id";
        var clientSecret = _configuration[$"OAuthProviders:{provider}:ClientSecret"] ?? "dummy_client_secret";
        var tokenUrl = _configuration[$"OAuthProviders:{provider}:TokenUrl"] ?? "https://oauth2.googleapis.com/token";
        var userInfoUrl = _configuration[$"OAuthProviders:{provider}:UserInfoUrl"] ?? "https://www.googleapis.com/oauth2/v3/userinfo";
        var redirectUri = request.RedirectUri;

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(tokenUrl))
        {
            return BadRequest("Provider not configured.");
        }

        var client = _httpClientFactory.CreateClient();
        
        var tokenResponse = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code"] = request.Code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri
        }));

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var err = await tokenResponse.Content.ReadAsStringAsync();
            return BadRequest($"Failed to exchange code for tokens. Details: {err}");
        }

        var tokenDataStr = await tokenResponse.Content.ReadAsStringAsync();
        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenDataStr);
        
        var accessToken = tokenData.GetProperty("access_token").GetString();
        var refreshToken = tokenData.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

        // Fetch User Info to get ProviderUserId
        string providerUserId = Guid.NewGuid().ToString(); // Fallback
        if (!string.IsNullOrEmpty(userInfoUrl) && !string.IsNullOrEmpty(accessToken))
        {
            var userReq = new HttpRequestMessage(HttpMethod.Get, userInfoUrl);
            userReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var userRes = await client.SendAsync(userReq);
            if (userRes.IsSuccessStatusCode)
            {
                var userInfoStr = await userRes.Content.ReadAsStringAsync();
                var userInfo = JsonSerializer.Deserialize<JsonElement>(userInfoStr);
                providerUserId = userInfo.TryGetProperty("id", out var idProp) ? idProp.GetString()! : 
                                 userInfo.TryGetProperty("sub", out var subProp) ? subProp.GetString()! : providerUserId;
            }
        }

        // Store as Credential
        var rawData = new Dictionary<string, string?>
        {
            ["access_token"] = accessToken,
            ["refresh_token"] = refreshToken
        };

        var dbCred = new CredentialEntity
        {
            Name = $"{provider} Access",
            SystemName = provider,
            EncryptedData = _encryptionService.Encrypt(JsonSerializer.Serialize(rawData)),
            TenantId = tenantId,
            UserId = userId
        };
        _dbContext.Add(dbCred);

        var dbAccount = new LinkedAccountEntity
        {
            Provider = provider,
            ProviderUserId = providerUserId,
            UserId = userId,
            TenantId = tenantId,
            Credential = dbCred
        };
        _dbContext.Add(dbAccount);

        await _dbContext.SaveChangesAsync();

        return Ok(new LinkedAccountDto
        {
            Id = dbAccount.Id,
            Provider = dbAccount.Provider,
            ProviderUserId = dbAccount.ProviderUserId,
            CreatedAt = dbAccount.CreatedAt
        });
    }

    [HttpPost("refresh/{linkedAccountId:guid}")]
    public async Task<IActionResult> RefreshToken(Guid linkedAccountId)
    {
        var tenantId = _userContext.TenantId;
        var userId = _userContext.CurrentUserId;

        var account = await _dbContext.Set<LinkedAccountEntity>()
            .Include(a => a.Credential)
            .FirstOrDefaultAsync(a => a.Id == linkedAccountId && a.TenantId == tenantId && a.UserId == userId);

        if (account == null || account.Credential == null) return NotFound();

        var provider = account.Provider;
        var clientId = _configuration[$"OAuthProviders:{provider}:ClientId"] ?? "dummy_client_id";
        var clientSecret = _configuration[$"OAuthProviders:{provider}:ClientSecret"] ?? "dummy_client_secret";
        var tokenUrl = _configuration[$"OAuthProviders:{provider}:TokenUrl"] ?? "https://oauth2.googleapis.com/token";

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(tokenUrl))
        {
            return BadRequest("Provider not configured.");
        }

        var decDataStr = _encryptionService.Decrypt(account.Credential.EncryptedData);
        if (string.IsNullOrEmpty(decDataStr)) return BadRequest("Invalid credential data.");
        
        var credData = JsonSerializer.Deserialize<Dictionary<string, string>>(decDataStr);
        if (credData == null || !credData.TryGetValue("refresh_token", out var refreshToken) || string.IsNullOrEmpty(refreshToken))
        {
            return BadRequest("No refresh token available.");
        }

        var client = _httpClientFactory.CreateClient();
        var tokenResponse = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        }));

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var err = await tokenResponse.Content.ReadAsStringAsync();
            return BadRequest($"Failed to refresh token. Details: {err}");
        }

        var tokenDataStr = await tokenResponse.Content.ReadAsStringAsync();
        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenDataStr);
        
        var newAccessToken = tokenData.GetProperty("access_token").GetString();
        var newRefreshToken = tokenData.TryGetProperty("refresh_token", out var nrt) ? nrt.GetString() : refreshToken;

        credData["access_token"] = newAccessToken!;
        if (newRefreshToken != null) credData["refresh_token"] = newRefreshToken;

        account.Credential.EncryptedData = _encryptionService.Encrypt(JsonSerializer.Serialize(credData));
        account.Credential.UpdatedAt = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Token refreshed successfully." });
    }
}

