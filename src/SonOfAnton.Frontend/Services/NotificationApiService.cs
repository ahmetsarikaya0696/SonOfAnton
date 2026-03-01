using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Frontend.Services;

public class NotificationApiService
{
    private readonly HttpClient _httpClient;

    public NotificationApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync(int skip = 0, int take = 50)
    {
        return await _httpClient.GetFromJsonAsync<List<NotificationDto>>($"api/notifications?skip={skip}&take={take}") ?? [];
    }

    public async Task<int> GetUnreadCountAsync()
    {
        return await _httpClient.GetFromJsonAsync<int>("api/notifications/unread-count");
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        await _httpClient.PutAsync($"api/notifications/{id}/read", null);
    }

    public async Task MarkAllAsReadAsync()
    {
        await _httpClient.PutAsync("api/notifications/read-all", null);
    }

    public async IAsyncEnumerable<NotificationDto> StreamNotificationsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "api/notifications/stream");
        request.SetBrowserResponseStreamingEnabled(true);
        
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Unauthorized or forbidden — stop streaming silently
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var options = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            if (line.StartsWith("data: "))
            {
                var data = line.Substring(6);
                if (data == "done") break;
                
                NotificationDto? dto = null;
                try 
                {
                    dto = System.Text.Json.JsonSerializer.Deserialize<NotificationDto>(data, options);
                } 
                catch 
                {
                    // Bağlantı ID'si gibi ham metinleri yoksay
                }

                if (dto != null)
                {
                    yield return dto;
                }
            }
        }
    }
}
