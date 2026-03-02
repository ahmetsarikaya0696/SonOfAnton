using System.Net.Http.Json;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Frontend.Services;

/// <summary>
/// Planlama belgeleri ve Ralph döngü motoru için Frontend API client servisi.
/// Authenticated BackendApi HttpClient kullanır.
/// </summary>
public class PlanningApiService
{
    private readonly HttpClient _httpClient;

    public PlanningApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // ─── Planning Documents ──────────────────────────────────────

    /// <summary>Belirtilen ajanın tüm belgelerini listeler</summary>
    public async Task<List<PlanningDocumentDto>> GetAllDocumentsAsync(string agentId)
    {
        return await _httpClient.GetFromJsonAsync<List<PlanningDocumentDto>>(
            $"api/planning/documents/{agentId}") ?? [];
    }

    /// <summary>Belirtilen tipte belgeyi okur</summary>
    public async Task<PlanningDocumentDto?> GetDocumentAsync(string agentId, PlanFileType fileType)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<PlanningDocumentDto>(
                $"api/planning/documents/{agentId}/{fileType}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>Belge yazar (oluşturur veya günceller)</summary>
    public async Task<PlanningDocumentDto?> WriteDocumentAsync(
        string agentId, PlanFileType fileType, string content)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/planning/documents/{agentId}/{fileType}",
            new { Content = content });

        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<PlanningDocumentDto>();
        return null;
    }

    /// <summary>Belgenin sonuna içerik ekler</summary>
    public async Task<PlanningDocumentDto?> AppendToDocumentAsync(
        string agentId, PlanFileType fileType, string content)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/planning/documents/{agentId}/{fileType}/append",
            new { Content = content });

        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<PlanningDocumentDto>();
        return null;
    }

    /// <summary>Belge versiyonlarını listeler</summary>
    public async Task<List<PlanningDocumentDto>> GetDocumentHistoryAsync(
        string agentId, PlanFileType fileType)
    {
        return await _httpClient.GetFromJsonAsync<List<PlanningDocumentDto>>(
            $"api/planning/documents/{agentId}/{fileType}/history") ?? [];
    }

    /// <summary>Belgeyi siler</summary>
    public async Task<bool> DeleteDocumentAsync(string agentId, PlanFileType fileType)
    {
        var response = await _httpClient.DeleteAsync(
            $"api/planning/documents/{agentId}/{fileType}");
        return response.IsSuccessStatusCode;
    }

    // ─── Ralph Loop Engine ──────────────────────────────────────

    /// <summary>Tüm aktif döngüleri listeler</summary>
    public async Task<List<LoopExecutionStateDto>> GetActiveLoopsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<LoopExecutionStateDto>>(
            "api/loops/active") ?? [];
    }

    /// <summary>Belirli bir döngünün durumunu getirir</summary>
    public async Task<LoopExecutionStateDto?> GetLoopStateAsync(string loopId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<LoopExecutionStateDto>(
                $"api/loops/{loopId}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>Döngü durum özeti (dashboard için)</summary>
    public async Task<LoopStatusDto?> GetLoopStatusAsync(string loopId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<LoopStatusDto>(
                $"api/loops/{loopId}/status");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>Yeni döngü başlatır</summary>
    public async Task<LoopExecutionStateDto?> StartLoopAsync(LoopStartRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/loops/start", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<LoopExecutionStateDto>();
        return null;
    }

    /// <summary>Döngüyü durdurur</summary>
    public async Task<bool> StopLoopAsync(string loopId, string? reason = null)
    {
        var response = await _httpClient.PostAsJsonAsync("api/loops/stop",
            new LoopStopRequest { LoopId = loopId, Reason = reason });
        return response.IsSuccessStatusCode;
    }

    /// <summary>Döngüyü duraklatır</summary>
    public async Task<bool> PauseLoopAsync(string loopId)
    {
        var response = await _httpClient.PostAsync($"api/loops/{loopId}/pause", null);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Döngüyü devam ettirir</summary>
    public async Task<bool> ResumeLoopAsync(string loopId)
    {
        var response = await _httpClient.PostAsync($"api/loops/{loopId}/resume", null);
        return response.IsSuccessStatusCode;
    }
}
