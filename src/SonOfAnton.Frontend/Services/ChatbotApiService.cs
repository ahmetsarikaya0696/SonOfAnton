using System.Net.Http.Json;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Frontend.Services;

public class ChatbotApiService
{
    private readonly HttpClient _http;

    public ChatbotApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ChatbotDto>> GetChatbotsAsync()
    {
        return await _http.GetFromJsonAsync<List<ChatbotDto>>("api/chatbots") ?? new();
    }

    public async Task<ChatbotDto?> GetChatbotAsync(Guid id)
    {
        return await _http.GetFromJsonAsync<ChatbotDto>($"api/chatbots/{id}");
    }

    public async Task<ChatbotDto> CreateChatbotAsync(CreateChatbotRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/chatbots", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatbotDto>() ?? throw new Exception("Yanıt alınamadı.");
    }

    public async Task<ChatbotDto> UpdateChatbotAsync(Guid id, UpdateChatbotRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/chatbots/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatbotDto>() ?? throw new Exception("Yanıt alınamadı.");
    }

    public async Task DeleteChatbotAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"api/chatbots/{id}");
        response.EnsureSuccessStatusCode();
    }
    
    // Public endpoints
    
    public async Task<ChatbotDto?> GetPublicChatbotAsync(Guid id)
    {
        return await _http.GetFromJsonAsync<ChatbotDto>($"api/public/chatbots/{id}");
    }

    public async Task<ChatbotSessionDto> CreateSessionAsync(Guid chatbotId)
    {
        var response = await _http.PostAsync($"api/public/chatbots/{chatbotId}/sessions", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatbotSessionDto>() ?? throw new Exception("Yanıt alınamadı.");
    }

    public async Task<ChatResponseDto> SendMessageAsync(Guid sessionId, ChatbotMessageDto message)
    {
        var response = await _http.PostAsJsonAsync($"api/public/chatbots/sessions/{sessionId}/messages", message);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatResponseDto>() ?? throw new Exception("Yanıt alınamadı.");
    }
}
