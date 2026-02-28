using System.Net.Http.Json;
using SonOfAnton.Shared.Models;

namespace SonOfAnton.Frontend.Services;

public class DataTableClientService
{
    private readonly HttpClient _httpClient;

    public DataTableClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<DataTableDto>> GetTablesAsync(Guid? workspaceId = null)
    {
        var url = workspaceId.HasValue ? $"api/data-tables?workspaceId={workspaceId}" : "api/data-tables";
        return await _httpClient.GetFromJsonAsync<List<DataTableDto>>(url) ?? [];
    }

    public async Task<DataTableDto?> GetTableAsync(Guid id)
    {
        return await _httpClient.GetFromJsonAsync<DataTableDto>($"api/data-tables/{id}");
    }

    public async Task<DataTableDto?> CreateTableAsync(DataTableCreateDto dto, Guid? workspaceId = null)
    {
        var url = workspaceId.HasValue ? $"api/data-tables?workspaceId={workspaceId}" : "api/data-tables";
        var response = await _httpClient.PostAsJsonAsync(url, dto);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<DataTableDto>();
        return null;
    }

    public async Task<bool> DeleteTableAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/data-tables/{id}");
        return response.IsSuccessStatusCode;
    }

    // -- Rows --
    public async Task<List<DataRowDto>> GetRowsAsync(Guid tableId)
    {
        return await _httpClient.GetFromJsonAsync<List<DataRowDto>>($"api/data-tables/{tableId}/rows") ?? [];
    }

    public async Task<DataRowDto?> AddRowAsync(Guid tableId, DataRowCreateDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/data-tables/{tableId}/rows", dto);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<DataRowDto>();
        return null;
    }

    public async Task<DataRowDto?> UpdateRowAsync(Guid tableId, Guid rowId, DataRowUpdateDto dto)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/data-tables/{tableId}/rows/{rowId}", dto);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<DataRowDto>();
        return null;
    }

    public async Task<bool> DeleteRowAsync(Guid tableId, Guid rowId)
    {
        var response = await _httpClient.DeleteAsync($"api/data-tables/{tableId}/rows/{rowId}");
        return response.IsSuccessStatusCode;
    }
}
