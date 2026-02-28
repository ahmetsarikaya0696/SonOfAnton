using System.Net.Http.Json;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Frontend.Services;

public class LinkedAccountService
{
    private readonly HttpClient _httpClient;

    public LinkedAccountService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<LinkedAccountDto>> GetLinkedAccountsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<LinkedAccountDto>>("api/linked-accounts") ?? new List<LinkedAccountDto>();
    }

    public async Task DeleteLinkedAccountAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/linked-accounts/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task RefreshTokenAsync(Guid linkedAccountId)
    {
        var response = await _httpClient.PostAsync($"api/linked-accounts/refresh/{linkedAccountId}", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<LinkedAccountDto?> ExchangeCodeAsync(string provider, OAuthExchangeRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/linked-accounts/exchange/{provider}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LinkedAccountDto>();
    }
}
