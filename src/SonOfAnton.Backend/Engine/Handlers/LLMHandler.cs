using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// LLM node'u - AI ile metin �retir
/// </summary>
public class LLMHandler : INodeHandler
{
    private readonly Kernel _kernel;

    public LLMHandler(Kernel kernel)
    {
        _kernel = kernel;
    }

    public string NodeType => "ai.llm";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var prompt = context.Node.Parameters.GetValueOrDefault("prompt")?.ToString();
        var systemPrompt = context.Node.Parameters.GetValueOrDefault("systemPrompt")?.ToString();
        
        if (string.IsNullOrEmpty(prompt))
        {
            return NodeExecutionResult.Fail("Prompt parametresi gerekli");
        }

        context.Log("LLM iste�i g�nderiliyor...", Shared.Enums.LogLevel.Info, null);

        try
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                history.AddSystemMessage(systemPrompt);
            }
            
            // Input data'y� prompt'a ekle
            var fullPrompt = prompt;
            if (context.InputData != null)
            {
                fullPrompt = $"{prompt}\n\nGirdi verisi:\n{System.Text.Json.JsonSerializer.Serialize(context.InputData)}";
            }
            
            history.AddUserMessage(fullPrompt);
            
            var response = await chatService.GetChatMessageContentAsync(
                history,
                cancellationToken: context.CancellationToken);
            
            var responseText = response.Content ?? string.Empty;
            
            context.Log("LLM yan�t� al�nd�", Shared.Enums.LogLevel.Info, null);
            
            return NodeExecutionResult.Ok(new
            {
                response = responseText,
                model = response.ModelId,
                inputData = context.InputData
            });
        }
        catch (Exception ex)
        {
            context.Log($"LLM hatas�: {ex.Message}", Shared.Enums.LogLevel.Error, null);
            return NodeExecutionResult.Fail($"LLM hatas�: {ex.Message}");
        }
    }
}

/// <summary>
/// OpenAI node'u - geriye uyumluluk i�in
/// </summary>
public class OpenAIHandler : LLMHandler
{
    public OpenAIHandler(Kernel kernel) : base(kernel)
    {
    }

    public new string NodeType => "ai.openai";
}

