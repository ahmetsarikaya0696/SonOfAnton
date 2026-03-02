namespace SonOfAnton.Shared.Core;

/// <summary>
/// Desteklenen harici YZ sağlayıcıları
/// </summary>
public enum AIProviderType
{
    OpenAI,
    Claude,
    Gemini
}

/// <summary>
/// Yanıt format seçenekleri
/// </summary>
public enum AIResponseFormat
{
    Text,
    JsonObject
}

/// <summary>
/// Harici YZ sağlayıcısına gönderilecek istek
/// </summary>
public record ExternalAIRequest
{
    /// <summary>
    /// Hedef sağlayıcı
    /// </summary>
    public required AIProviderType Provider { get; init; }

    /// <summary>
    /// Kullanılacak model ID. null = sağlayıcı varsayılanı
    /// </summary>
    public string? ModelId { get; init; }

    /// <summary>
    /// Chat mesajları
    /// </summary>
    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    /// <summary>
    /// Sıcaklık (0.0 – 2.0). Varsayılan: 0.7
    /// </summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>
    /// Maksimum token sayısı
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Yanıt formatı: Text veya JSON
    /// </summary>
    public AIResponseFormat ResponseFormat { get; init; } = AIResponseFormat.Text;

    /// <summary>
    /// Vision desteği için görüntü verisi (base64 veya byte[])
    /// </summary>
    public byte[]? ImageData { get; init; }

    /// <summary>
    /// Görüntü MIME tipi (image/png, image/jpeg vb.)
    /// </summary>
    public string? ImageMimeType { get; init; }
}

/// <summary>
/// Harici YZ sağlayıcı bilgileri
/// </summary>
public record ExternalAIProvider(
    AIProviderType Type,
    string Name,
    IReadOnlyList<string> AvailableModels,
    bool SupportsVision,
    bool SupportsStreaming,
    bool SupportsJsonMode);

/// <summary>
/// Harici YZ sağlayıcılarına sağlayıcı-bağımsız erişim sağlayan ağ geçidi.
/// OpenAI, Claude ve Gemini destekler. Vision, Streaming, JSON Mode özellikleri.
/// </summary>
public interface IExternalAIGateway
{
    /// <summary>
    /// Tek seferlik tamamlama isteği gönderir
    /// </summary>
    Task<string> CompleteAsync(
        ExternalAIRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streaming tamamlama — token'lar IAsyncEnumerable ile döner
    /// </summary>
    IAsyncEnumerable<string> StreamCompleteAsync(
        ExternalAIRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanılabilir sağlayıcıların listesini döndürür
    /// </summary>
    Task<IReadOnlyList<ExternalAIProvider>> GetAvailableProvidersAsync(
        CancellationToken cancellationToken = default);
}
