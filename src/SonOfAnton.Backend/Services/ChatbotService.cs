using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;
using System.Text.Json;

namespace SonOfAnton.Backend.Services;

public class ChatbotService
{
    private readonly SonOfAntonDbContext _dbContext;
    private readonly Kernel _kernel;
    
    public ChatbotService(SonOfAntonDbContext dbContext, Kernel kernel)
    {
        _dbContext = dbContext;
        _kernel = kernel;
    }

    public async Task<List<ChatbotDto>> GetChatbotsAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var chatbots = await _dbContext.Chatbots
            .Where(c => c.WorkspaceId == workspaceId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
            
        return chatbots.Select(ToDto).ToList();
    }

    public async Task<ChatbotDto?> GetChatbotAsync(Guid id, Guid? workspaceId = null, CancellationToken ct = default)
    {
        var query = _dbContext.Chatbots.AsQueryable();
        if (workspaceId.HasValue)
        {
            query = query.Where(c => c.WorkspaceId == workspaceId.Value);
        }
        
        var chatbot = await query.FirstOrDefaultAsync(c => c.Id == id, ct);
        return chatbot == null ? null : ToDto(chatbot);
    }
    
    public async Task<ChatbotDto?> GetPublicChatbotAsync(Guid id, CancellationToken ct = default)
    {
        var chatbot = await _dbContext.Chatbots.FirstOrDefaultAsync(c => c.Id == id && c.IsPublic, ct);
        return chatbot == null ? null : ToDto(chatbot);
    }

    public async Task<ChatbotDto> CreateChatbotAsync(Guid workspaceId, CreateChatbotRequest request, CancellationToken ct = default)
    {
        var entity = new ChatbotEntity
        {
            Name = request.Name,
            Description = request.Description,
            SystemPrompt = string.IsNullOrWhiteSpace(request.SystemPrompt) ? "Sen yardımcı bir asistansın." : request.SystemPrompt,
            Model = string.IsNullOrWhiteSpace(request.Model) ? "gpt-4o" : request.Model,
            WorkspaceId = workspaceId,
            IsPublic = request.IsPublic
        };
        
        _dbContext.Chatbots.Add(entity);
        await _dbContext.SaveChangesAsync(ct);
        
        return ToDto(entity);
    }

    public async Task<ChatbotDto?> UpdateChatbotAsync(Guid id, Guid workspaceId, UpdateChatbotRequest request, CancellationToken ct = default)
    {
        var entity = await _dbContext.Chatbots.FirstOrDefaultAsync(c => c.Id == id && c.WorkspaceId == workspaceId, ct);
        if (entity == null) return null;
        
        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.SystemPrompt = request.SystemPrompt;
        entity.Model = request.Model;
        entity.IsPublic = request.IsPublic;
        entity.UpdatedAt = DateTime.UtcNow;
        
        await _dbContext.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<bool> DeleteChatbotAsync(Guid id, Guid workspaceId, CancellationToken ct = default)
    {
        var entity = await _dbContext.Chatbots.FirstOrDefaultAsync(c => c.Id == id && c.WorkspaceId == workspaceId, ct);
        if (entity == null) return false;
        
        _dbContext.Chatbots.Remove(entity);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    // ─── Chat Logic ──────────────────────────────────────────────

    public async Task<ChatbotSessionDto> CreateSessionAsync(Guid chatbotId, Guid? userId = null, CancellationToken ct = default)
    {
        var session = new ChatbotSessionEntity
        {
            ChatbotId = chatbotId,
            UserId = userId,
            Title = "Yeni Sohbet"
        };
        
        _dbContext.ChatbotSessions.Add(session);
        await _dbContext.SaveChangesAsync(ct);
        
        return ToSessionDto(session);
    }
    
    public async Task<ChatbotSessionDto?> GetSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _dbContext.ChatbotSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        return session == null ? null : ToSessionDto(session);
    }

    public async Task<ChatResponseDto> SendMessageAsync(Guid sessionId, string message, CancellationToken ct = default)
    {
        // 1. Get Session & Chatbot
        var session = await _dbContext.ChatbotSessions
            .Include(s => s.Chatbot)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
            
        if (session == null) throw new InvalidOperationException("Session not found");
        
        // 2. Load History
        var history = new ChatHistory();
        history.AddSystemMessage(session.Chatbot.SystemPrompt);
        
        var priorMessages = JsonSerializer.Deserialize<List<ChatbotMessageDto>>(session.HistoryJson) ?? new List<ChatbotMessageDto>();
        foreach (var msg in priorMessages)
        {
            if (msg.Role == "user") history.AddUserMessage(msg.Content);
            else if (msg.Role == "assistant") history.AddAssistantMessage(msg.Content);
        }
        
        // 3. Add new message
        history.AddUserMessage(message);
        priorMessages.Add(new ChatbotMessageDto("user", message));
        
        // 4. Call Chat Service
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var response = await chatService.GetChatMessageContentAsync(history, null, _kernel, ct);
        var responseText = response.Content ?? "Bir hata oluştu.";
        
        // 5. Update History
        priorMessages.Add(new ChatbotMessageDto("assistant", responseText));
        session.HistoryJson = JsonSerializer.Serialize(priorMessages);
        session.UpdatedAt = DateTime.UtcNow;
        
        // Create title for newly created session (if first user message)
        if (priorMessages.Count == 2)
        {
            session.Title = message.Length > 20 ? message.Substring(0, 20) + "..." : message;
        }

        await _dbContext.SaveChangesAsync(ct);
        
        return new ChatResponseDto(responseText);
    }

    private static ChatbotDto ToDto(ChatbotEntity entity) => new(
        entity.Id,
        entity.Name,
        entity.Description,
        entity.SystemPrompt,
        entity.Model,
        entity.WorkspaceId,
        entity.IsPublic,
        entity.CreatedAt,
        entity.UpdatedAt
    );
    
    private static ChatbotSessionDto ToSessionDto(ChatbotSessionEntity entity) => new(
        entity.Id,
        entity.ChatbotId,
        entity.Title,
        entity.HistoryJson,
        entity.CreatedAt,
        entity.UpdatedAt
    );
}
