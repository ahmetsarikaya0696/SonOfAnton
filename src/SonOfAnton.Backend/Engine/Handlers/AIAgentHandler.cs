using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SonOfAnton.Backend.Engine.Handlers;

/// <summary>
/// Otonom AI Ajanı - Araç (Tool) kullanabilen node
/// </summary>
public class AIAgentHandler : INodeHandler
{
    private readonly Kernel _kernel;

    public AIAgentHandler(Kernel kernel)
    {
        // Klonlayarak ajana özel pluginler ekliyoruz
        _kernel = kernel.Clone();
        
        // Temel genel-amaçlı pluginleri ekleyelim (örnek)
        #pragma warning disable SKEXP0050 // Semantic Kernel deneysel özellikleri
        _kernel.ImportPluginFromType<Microsoft.SemanticKernel.Plugins.Core.TimePlugin>("Time");
        #pragma warning restore SKEXP0050
    }

    public string NodeType => "ai.agent";

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context)
    {
        var prompt = context.Node.Parameters.GetValueOrDefault("prompt")?.ToString();
        var systemPrompt = context.Node.Parameters.GetValueOrDefault("systemPrompt")?.ToString();
        
        if (string.IsNullOrEmpty(prompt))
        {
            return NodeExecutionResult.Fail("Prompt parametresi gerekli.");
        }

        context.Log("AI Agent düşünmeye başlıyor...", Shared.Enums.LogLevel.Info, null);

        try
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            
            // Auto-invoke tools özelliği
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            var history = new ChatHistory();
            
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                history.AddSystemMessage(systemPrompt);
            }
            
            history.AddUserMessage(prompt);

            var response = await chatService.GetChatMessageContentAsync(
                history,
                executionSettings: executionSettings,
                kernel: _kernel,
                cancellationToken: context.CancellationToken);
            
            var responseText = response.Content ?? string.Empty;
            
            context.Log("Agent görevini tamamladı.", Shared.Enums.LogLevel.Info, null);
            
            return NodeExecutionResult.Ok(new
            {
                response = responseText,
                inputData = context.InputData
            });
        }
        catch (Exception ex)
        {
            context.Log($"Agent hatası: {ex.Message}", Shared.Enums.LogLevel.Error, null);
            return NodeExecutionResult.Fail($"Ajan işlem hatası: {ex.Message}");
        }
    }
}
