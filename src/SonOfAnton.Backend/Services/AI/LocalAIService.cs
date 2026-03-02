using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SonOfAnton.Shared.Core;

namespace SonOfAnton.Backend.Services.AI;

/// <summary>
/// Semantic Kernel + Ollama tabanlı yerel LLM servisi.
/// IChatCompletionService üzerinden Ollama'ya bağlanır.
/// Streaming, çok turlu chat ve tek seferlik tamamlama destekler.
/// </summary>
public class LocalAIService : ILocalAIService
{
    private readonly Kernel _kernel;
    private readonly ILogger<LocalAIService> _logger;
    private readonly HardwareConfig _hardwareConfig;
    private readonly SemaphoreSlim _concurrencyLimiter;

    public LocalAIService(
        Kernel kernel,
        ILogger<LocalAIService> logger,
        IOptions<HardwareConfig> hardwareConfig)
    {
        _kernel = kernel;
        _logger = logger;
        _hardwareConfig = hardwareConfig.Value;
        _concurrencyLimiter = new SemaphoreSlim(
            _hardwareConfig.MaxConcurrentInferences,
            _hardwareConfig.MaxConcurrentInferences);
    }

    /// <inheritdoc />
    public async Task<string> CompleteAsync(
        string prompt,
        LocalAIOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<Shared.Core.ChatMessage>
        {
            new(ChatMessageRole.User, prompt)
        };

        return await ChatCompleteAsync(messages, options, cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamCompleteAsync(
        string prompt,
        LocalAIOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = new List<Shared.Core.ChatMessage>
        {
            new(ChatMessageRole.User, prompt)
        };

        await foreach (var chunk in StreamChatCompleteAsync(messages, options, cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <inheritdoc />
    public async Task<string> ChatCompleteAsync(
        IEnumerable<Shared.Core.ChatMessage> messages,
        LocalAIOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_hardwareConfig.InferenceTimeoutSeconds));

        await _concurrencyLimiter.WaitAsync(timeoutCts.Token);
        try
        {
            var chatHistory = BuildChatHistory(messages);
            var executionSettings = BuildExecutionSettings(options);

            var chatService = _kernel.GetRequiredService<IChatCompletionService>();

            _logger.LogDebug(
                "Yerel LLM isteği gönderiliyor. Mesaj sayısı: {MessageCount}, Model: {Model}",
                chatHistory.Count, options?.ModelId ?? "default");

            var response = await chatService.GetChatMessageContentAsync(
                chatHistory, executionSettings, _kernel, timeoutCts.Token);

            var result = response.Content ?? string.Empty;

            _logger.LogDebug(
                "Yerel LLM yanıtı alındı. Uzunluk: {Length}",
                result.Length);

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Yerel LLM isteği kullanıcı tarafından iptal edildi.");
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Yerel LLM isteği zaman aşımına uğradı ({Timeout}s).",
                _hardwareConfig.InferenceTimeoutSeconds);
            throw new TimeoutException(
                $"Yerel LLM yanıt zaman aşımı: {_hardwareConfig.InferenceTimeoutSeconds} saniye.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yerel LLM isteği başarısız oldu.");
            throw;
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamChatCompleteAsync(
        IEnumerable<Shared.Core.ChatMessage> messages,
        LocalAIOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_hardwareConfig.InferenceTimeoutSeconds));

        await _concurrencyLimiter.WaitAsync(timeoutCts.Token);
        try
        {
            var chatHistory = BuildChatHistory(messages);
            var executionSettings = BuildExecutionSettings(options);

            var chatService = _kernel.GetRequiredService<IChatCompletionService>();

            _logger.LogDebug("Yerel LLM streaming isteği başlatılıyor.");

            await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(
                chatHistory, executionSettings, _kernel, timeoutCts.Token))
            {
                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    yield return chunk.Content;
                }
            }

            _logger.LogDebug("Yerel LLM streaming tamamlandı.");
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            history.AddUserMessage("ping");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var response = await chatService.GetChatMessageContentAsync(
                history, null, _kernel, cts.Token);

            return response?.Content != null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Yerel LLM erişilebilirlik kontrolü başarısız.");
            return false;
        }
    }

    // ─── Private Helpers ──────────────────────────────────────────

    private static ChatHistory BuildChatHistory(IEnumerable<Shared.Core.ChatMessage> messages)
    {
        var history = new ChatHistory();

        foreach (var message in messages)
        {
            switch (message.Role)
            {
                case ChatMessageRole.System:
                    history.AddSystemMessage(message.Content);
                    break;
                case ChatMessageRole.User:
                    history.AddUserMessage(message.Content);
                    break;
                case ChatMessageRole.Assistant:
                    history.AddAssistantMessage(message.Content);
                    break;
            }
        }

        return history;
    }

    private static PromptExecutionSettings? BuildExecutionSettings(LocalAIOptions? options)
    {
        if (options is null) return null;

        return new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object>
            {
                ["temperature"] = options.Temperature,
                ["top_p"] = options.TopP,
                ["max_tokens"] = options.MaxTokens ?? 0,
                ["stop"] = options.StopSequences ?? (IReadOnlyList<string>)Array.Empty<string>()
            },
            ModelId = options.ModelId
        };
    }
}
