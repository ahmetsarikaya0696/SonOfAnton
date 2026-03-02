namespace SonOfAnton.Shared.Core;

/// <summary>
/// Yerel LLM ile iletişim için mesaj rolü
/// </summary>
public enum ChatMessageRole
{
    System,
    User,
    Assistant
}

/// <summary>
/// Chat mesajı — Rol ve içerik çifti
/// </summary>
public record ChatMessage(ChatMessageRole Role, string Content);

/// <summary>
/// Yerel LLM servisine gönderilecek seçenekler
/// </summary>
public record LocalAIOptions
{
    /// <summary>
    /// Yanıt yaratıcılığı (0.0 – 2.0). Varsayılan: 0.7
    /// </summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>
    /// Üretilecek maksimum token sayısı. null = model varsayılanı
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Nucleus sampling üst sınırı (0.0 – 1.0). Varsayılan: 0.9
    /// </summary>
    public double TopP { get; init; } = 0.9;

    /// <summary>
    /// Yanıtı sonlandıracak dizeler
    /// </summary>
    public IReadOnlyList<string>? StopSequences { get; init; }

    /// <summary>
    /// Kullanılacak model adı. null = yapılandırmadaki varsayılan model
    /// </summary>
    public string? ModelId { get; init; }
}

/// <summary>
/// Sağlayıcı-bağımsız yerel LLM servis arayüzü.
/// Semantic Kernel üzerinden Ollama veya LM-Kit gibi yerel modellere bağlanır.
/// </summary>
public interface ILocalAIService
{
    /// <summary>
    /// Tek seferlik metin tamamlama
    /// </summary>
    Task<string> CompleteAsync(
        string prompt,
        LocalAIOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streaming metin tamamlama — token'lar tek tek döner
    /// </summary>
    IAsyncEnumerable<string> StreamCompleteAsync(
        string prompt,
        LocalAIOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Çok turlu chat tamamlama
    /// </summary>
    Task<string> ChatCompleteAsync(
        IEnumerable<ChatMessage> messages,
        LocalAIOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streaming çok turlu chat tamamlama
    /// </summary>
    IAsyncEnumerable<string> StreamChatCompleteAsync(
        IEnumerable<ChatMessage> messages,
        LocalAIOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Modelin erişilebilir olup olmadığını kontrol eder
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
