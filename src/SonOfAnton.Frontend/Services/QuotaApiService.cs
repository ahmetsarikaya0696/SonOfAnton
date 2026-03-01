using System.Net.Http.Json;
using SonOfAnton.Shared.Entities;

namespace SonOfAnton.Frontend.Services;

public class QuotaApiService
{
    private readonly HttpClient _httpClient;

    public QuotaApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UserQuotaEntity?> GetMyQuotaAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<UserQuotaEntity>("api/quotas");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching quota: {ex.Message}");
            return null;
        }
    }
}
