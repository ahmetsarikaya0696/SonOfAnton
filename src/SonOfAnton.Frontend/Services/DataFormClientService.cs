using System.Net.Http.Json;
using SonOfAnton.Shared.Models;

namespace SonOfAnton.Frontend.Services;

public class DataFormClientService
{
    private readonly HttpClient _httpClient;

    public DataFormClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // ─── Management ──────────────────────────────
    public async Task<List<DataFormDto>> GetFormsForTableAsync(Guid tableId)
    {
        return await _httpClient.GetFromJsonAsync<List<DataFormDto>>($"api/data-forms/table/{tableId}") ?? [];
    }

    public async Task<DataFormDto?> GetFormAsync(Guid id)
    {
        return await _httpClient.GetFromJsonAsync<DataFormDto>($"api/data-forms/{id}");
    }

    public async Task<DataFormDto?> CreateFormAsync(DataFormCreateDto dto, Guid? workspaceId = null)
    {
        var url = workspaceId.HasValue ? $"api/data-forms?workspaceId={workspaceId}" : "api/data-forms";
        var response = await _httpClient.PostAsJsonAsync(url, dto);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<DataFormDto>();
        return null;
    }

    public async Task<bool> UpdateFormAsync(Guid id, DataFormUpdateDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/data-forms/{id}", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteFormAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/data-forms/{id}");
        return response.IsSuccessStatusCode;
    }

    // ─── Public ──────────────────────────────
    public async Task<DataFormDto?> GetPublicFormAsync(string slug)
    {
        return await _httpClient.GetFromJsonAsync<DataFormDto>($"api/data-forms/public/{slug}");
    }

    public async Task<bool> SubmitPublicFormAsync(string slug, DataFormSubmitDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/data-forms/public/{slug}/submit", dto);
        return response.IsSuccessStatusCode;
    }
}
