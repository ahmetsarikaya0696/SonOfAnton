using System.Net.Http.Json;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Frontend.Services;

public class CredentialService
{
    private readonly HttpClient _httpClient;

    public CredentialService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<CredentialDto>> GetCredentialsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<CredentialDto>>("api/credentials") ?? new List<CredentialDto>();
    }

    public async Task<CredentialDto?> CreateCredentialAsync(CreateCredentialRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/credentials", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CredentialDto>();
    }

    public async Task<CredentialDto?> UpdateCredentialAsync(Guid id, UpdateCredentialRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/credentials/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CredentialDto>();
    }

    public async Task DeleteCredentialAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/credentials/{id}");
        response.EnsureSuccessStatusCode();
    }
}
