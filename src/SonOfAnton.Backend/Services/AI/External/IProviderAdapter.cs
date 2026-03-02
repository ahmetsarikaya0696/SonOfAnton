using SonOfAnton.Shared.Core;

namespace SonOfAnton.Backend.Services.AI.External;

/// <summary>
/// Harici YZ sağlayıcı adaptör arayüzü.
/// Her sağlayıcı (OpenAI, Claude, Gemini) bu arayüzü uygular.
/// </summary>
public interface IProviderAdapter
{
    /// <summary>
    /// Bu adaptörün desteklediği sağlayıcı türü
    /// </summary>
    AIProviderType ProviderType { get; }

    /// <summary>
    /// Vision (görüntü) desteği var mı?
    /// </summary>
    bool SupportsVision { get; }

    /// <summary>
    /// JSON Mode desteği var mı?
    /// </summary>
    bool SupportsJsonMode { get; }

    /// <summary>
    /// Streaming desteği var mı?
    /// </summary>
    bool SupportsStreaming { get; }

    /// <summary>
    /// Kullanılabilir model listesi
    /// </summary>
    IReadOnlyList<string> AvailableModels { get; }

    /// <summary>
    /// Varsayılan model ID
    /// </summary>
    string DefaultModel { get; }

    /// <summary>
    /// Tek seferlik tamamlama
    /// </summary>
    Task<string> CompleteAsync(
        ExternalAIRequest request,
        string apiKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streaming tamamlama — token'lar IAsyncEnumerable ile döner
    /// </summary>
    IAsyncEnumerable<string> StreamCompleteAsync(
        ExternalAIRequest request,
        string apiKey,
        CancellationToken cancellationToken = default);
}
