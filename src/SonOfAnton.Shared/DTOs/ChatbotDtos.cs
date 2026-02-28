namespace SonOfAnton.Shared.DTOs;

public record ChatbotDto(
    Guid Id,
    string Name,
    string? Description,
    string SystemPrompt,
    string Model,
    Guid WorkspaceId,
    bool IsPublic,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateChatbotRequest(
    string Name,
    string? Description,
    string SystemPrompt,
    string Model,
    bool IsPublic
);

public record UpdateChatbotRequest(
    string Name,
    string? Description,
    string SystemPrompt,
    string Model,
    bool IsPublic
);

public record ChatbotMessageDto(
    string Role, // "user" or "assistant"
    string Content
);

public record ChatResponseDto(
    string Content
);

public record ChatbotSessionDto(
    Guid Id,
    Guid ChatbotId,
    string Title,
    string HistoryJson,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
