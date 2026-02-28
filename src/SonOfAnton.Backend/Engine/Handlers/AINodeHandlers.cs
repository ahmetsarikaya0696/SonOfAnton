using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;

namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// Duygu analizi yapar (Positive, Negative, Neutral)
/// </summary>
public class SentimentAnalysisHandler : INodeHandler
{
    private readonly Kernel _kernel;

    public SentimentAnalysisHandler(Kernel kernel)
    {
        _kernel = kernel;
    }

    public string NodeType => "ai.sentiment";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var text = context.Node.Parameters.GetValueOrDefault("text")?.ToString();
        
        if (string.IsNullOrEmpty(text))
        {
            return NodeExecutionResult.Fail("Text parametresi gerekli.");
        }

        context.Log("Duygu analizi başlıyor...", Shared.Enums.LogLevel.Info, null);

        try
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            
            history.AddSystemMessage("You are an expert sentiment analyzer. Respond with exactly one word: 'Positive', 'Negative', or 'Neutral'. Do not include punctuation or additional text.");
            history.AddUserMessage($"Text: {text}");
            
            var response = await chatService.GetChatMessageContentAsync(
                history,
                cancellationToken: context.CancellationToken);
            
            var responseText = response.Content?.Trim() ?? "Neutral";
            
            context.Log($"Sonuç: {responseText}", Shared.Enums.LogLevel.Info, null);
            
            return NodeExecutionResult.Ok(new
            {
                sentiment = responseText,
                inputData = context.InputData
            });
        }
        catch (Exception ex)
        {
            context.Log($"SentimentAnalysis hatası: {ex.Message}", Shared.Enums.LogLevel.Error, null);
            return NodeExecutionResult.Fail($"Duygu analizi hatası: {ex.Message}");
        }
    }
}

/// <summary>
/// Metni belirli kategorilere göre sınıflandırır
/// </summary>
public class TextClassificationHandler : INodeHandler
{
    private readonly Kernel _kernel;

    public TextClassificationHandler(Kernel kernel)
    {
        _kernel = kernel;
    }

    public string NodeType => "ai.classify";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var text = context.Node.Parameters.GetValueOrDefault("text")?.ToString();
        var categories = context.Node.Parameters.GetValueOrDefault("categories")?.ToString();
        
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(categories))
        {
            return NodeExecutionResult.Fail("Text ve Categories parametreleri gerekli.");
        }

        context.Log("Metin sınıflandırma başlıyor...", Shared.Enums.LogLevel.Info, null);

        try
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            
            history.AddSystemMessage($"You are an expert text classifier. Classify the user's text into ONE of the following categories: {categories}. Respond with ONLY the exact category name. If none fit perfectly, pick the closest one.");
            history.AddUserMessage($"Text: {text}");
            
            var response = await chatService.GetChatMessageContentAsync(
                history,
                cancellationToken: context.CancellationToken);
            
            var responseText = response.Content?.Trim() ?? "Unknown";
            
            context.Log($"Kategori: {responseText}", Shared.Enums.LogLevel.Info, null);
            
            return NodeExecutionResult.Ok(new
            {
                category = responseText,
                inputData = context.InputData
            });
        }
        catch (Exception ex)
        {
            context.Log($"TextClassification hatası: {ex.Message}", Shared.Enums.LogLevel.Error, null);
            return NodeExecutionResult.Fail($"Sınıflandırma hatası: {ex.Message}");
        }
    }
}

/// <summary>
/// Görüntü analizi yapar (Vision)
/// </summary>
public class ImageAnalysisHandler : INodeHandler
{
    private readonly Kernel _kernel;

    public ImageAnalysisHandler(Kernel kernel)
    {
        _kernel = kernel;
    }

    public string NodeType => "ai.vision";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var imageUrl = context.Node.Parameters.GetValueOrDefault("imageUrl")?.ToString();
        var prompt = context.Node.Parameters.GetValueOrDefault("prompt")?.ToString();
        
        if (string.IsNullOrEmpty(imageUrl) || string.IsNullOrEmpty(prompt))
        {
            return NodeExecutionResult.Fail("ImageUrl ve Prompt parametreleri gerekli.");
        }

        context.Log("Görüntü analizi başlıyor...", Shared.Enums.LogLevel.Info, null);

        try
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            
            var messageContent = new ChatMessageContentItemCollection();
            messageContent.Add(new TextContent(prompt));
            messageContent.Add(new ImageContent(new Uri(imageUrl)));

            var history = new ChatHistory();
            history.AddUserMessage(messageContent);
            
            var response = await chatService.GetChatMessageContentAsync(
                history,
                cancellationToken: context.CancellationToken);
            
            var responseText = response.Content ?? string.Empty;
            
            context.Log("Görüntü başarıyla analiz edildi.", Shared.Enums.LogLevel.Info, null);
            
            return NodeExecutionResult.Ok(new
            {
                response = responseText,
                inputData = context.InputData
            });
        }
        catch (Exception ex)
        {
            context.Log($"Vision hatası: {ex.Message}", Shared.Enums.LogLevel.Error, null);
            return NodeExecutionResult.Fail($"Görüntü analizi hatası: {ex.Message}");
        }
    }
}
