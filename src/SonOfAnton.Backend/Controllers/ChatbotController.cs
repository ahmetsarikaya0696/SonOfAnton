using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/chatbots")]
public class ChatbotController : ControllerBase
{
    private readonly ChatbotService _chatbotService;

    public ChatbotController(ChatbotService chatbotService)
    {
        _chatbotService = chatbotService;
    }

    // TODO: Require [Authorize] and workspace validation matching SonOfAnton conventions in real project.
    // Assuming workspaceId is provided via header for now.

    private Guid GetWorkspaceId()
    {
        if (Request.Headers.TryGetValue("X-Workspace-Id", out var val) && Guid.TryParse(val, out var id))
        {
            return id;
        }
        return Guid.Empty; // Temporary default
    }

    [HttpGet]
    public async Task<ActionResult<List<ChatbotDto>>> GetChatbots()
    {
        var workspaceId = GetWorkspaceId();
        if (workspaceId == Guid.Empty) return BadRequest("WorkspaceId is required.");
        
        return await _chatbotService.GetChatbotsAsync(workspaceId, HttpContext.RequestAborted);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ChatbotDto>> GetChatbot(Guid id)
    {
        var workspaceId = GetWorkspaceId();
        var chatbot = await _chatbotService.GetChatbotAsync(id, workspaceId, HttpContext.RequestAborted);
        if (chatbot == null) return NotFound();
        return chatbot;
    }

    [HttpPost]
    public async Task<ActionResult<ChatbotDto>> CreateChatbot([FromBody] CreateChatbotRequest request)
    {
        var workspaceId = GetWorkspaceId();
        if (workspaceId == Guid.Empty) return BadRequest("WorkspaceId is required.");
        
        var chatbot = await _chatbotService.CreateChatbotAsync(workspaceId, request, HttpContext.RequestAborted);
        return CreatedAtAction(nameof(GetChatbot), new { id = chatbot.Id }, chatbot);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ChatbotDto>> UpdateChatbot(Guid id, [FromBody] UpdateChatbotRequest request)
    {
        var workspaceId = GetWorkspaceId();
        var chatbot = await _chatbotService.UpdateChatbotAsync(id, workspaceId, request, HttpContext.RequestAborted);
        if (chatbot == null) return NotFound();
        return chatbot;
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteChatbot(Guid id)
    {
        var workspaceId = GetWorkspaceId();
        var success = await _chatbotService.DeleteChatbotAsync(id, workspaceId, HttpContext.RequestAborted);
        if (!success) return NotFound();
        return NoContent();
    }
}

/// <summary>
/// Zapier Interfaces tarzı public chat endpoints
/// </summary>
[ApiController]
[Route("api/public/chatbots")]
public class PublicChatbotController : ControllerBase
{
    private readonly ChatbotService _chatbotService;

    public PublicChatbotController(ChatbotService chatbotService)
    {
        _chatbotService = chatbotService;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ChatbotDto>> GetPublicChatbot(Guid id)
    {
        var chatbot = await _chatbotService.GetPublicChatbotAsync(id, HttpContext.RequestAborted);
        if (chatbot == null) return NotFound("Chatbot not found or not public.");
        return chatbot;
    }

    [HttpPost("{id}/sessions")]
    public async Task<ActionResult<ChatbotSessionDto>> CreateSession(Guid id)
    {
        // Must be public before allowing anon sessions
        var chatbot = await _chatbotService.GetPublicChatbotAsync(id, HttpContext.RequestAborted);
        if (chatbot == null) return NotFound("Chatbot not found or not public.");
        
        // We can pass userId if we are using an auth mechanism, otherwise null
        var session = await _chatbotService.CreateSessionAsync(id, null, HttpContext.RequestAborted);
        return session;
    }

    [HttpPost("sessions/{sessionId}/messages")]
    public async Task<ActionResult<ChatResponseDto>> SendMessage(Guid sessionId, [FromBody] ChatbotMessageDto message)
    {
        try
        {
            var response = await _chatbotService.SendMessageAsync(sessionId, message.Content, HttpContext.RequestAborted);
            return response;
        }
        catch (InvalidOperationException)
        {
            return NotFound("Session not found");
        }
    }
}
