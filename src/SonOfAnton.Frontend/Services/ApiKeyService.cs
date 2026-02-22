using System.Net.Http.Json;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Frontend.Services;

public class ApiKeyService
{
    private readonly HttpClient _httpClient;

    public ApiKeyService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ApiKeyDto>> GetApiKeysAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<ApiKeyDto>>("api/apikeys") ?? new List<ApiKeyDto>();
    }

    public async Task<CreateApiKeyResponse?> CreateApiKeyAsync(CreateApiKeyRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/apikeys", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
    }

    public async Task DeleteApiKeyAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/apikeys/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> ToggleApiKeyAsync(Guid id)
    {
        var response = await _httpClient.PatchAsync($"api/apikeys/{id}/toggle", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ToggleResponse>();
        return result?.IsActive ?? false;
    }

    public async Task<List<string>?> UpdateScopesAsync(Guid id, UpdateApiKeyScopesRequest request)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/apikeys/{id}/scopes", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UpdateScopesResponse>();
        return result?.Scopes;
    }

    private class ToggleResponse
    {
        public bool IsActive { get; set; }
    }

    private class UpdateScopesResponse
    {
        public List<string>? Scopes { get; set; }
    }
}
