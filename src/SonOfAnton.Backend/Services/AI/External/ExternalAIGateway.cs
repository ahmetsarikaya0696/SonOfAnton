using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using SonOfAnton.Shared.Core;

namespace SonOfAnton.Backend.Services.AI.External;

/// <summary>
/// Harici YZ sağlayıcılarına sağlayıcı-bağımsız erişim ağ geçidi.
/// Gelen isteği doğru IProviderAdapter'a yönlendirir.
/// API anahtarlarını ISecretManager üzerinden güvenli şekilde alır.
/// </summary>
public class ExternalAIGateway : IExternalAIGateway
{
    private readonly Dictionary<AIProviderType, IProviderAdapter> _adapters;
    private readonly ISecretManager _secretManager;
    private readonly ILogger<ExternalAIGateway> _logger;

    /// <summary>
    /// Secret key formatı: "AI:ApiKeys:{ProviderName}"
    /// </summary>
    private static string GetSecretKey(AIProviderType provider) =>
        $"AI:ApiKeys:{provider}";

    public ExternalAIGateway(
        IEnumerable<IProviderAdapter> adapters,
        ISecretManager secretManager,
        ILogger<ExternalAIGateway> logger)
    {
        _adapters = adapters.ToDictionary(a => a.ProviderType);
        _secretManager = secretManager;
        _logger = logger;

        _logger.LogInformation(
            "ExternalAIGateway başlatıldı. Kayıtlı sağlayıcılar: {Providers}",
            string.Join(", ", _adapters.Keys));
    }

    /// <inheritdoc />
    public async Task<string> CompleteAsync(
        ExternalAIRequest request,
        CancellationToken cancellationToken = default)
    {
        var adapter = GetAdapter(request.Provider);
        var apiKey = await GetApiKeyAsync(request.Provider, cancellationToken);

        _logger.LogDebug(
            "Harici YZ isteği gönderiliyor. Sağlayıcı: {Provider}, Model: {Model}",
            request.Provider, request.ModelId ?? adapter.DefaultModel);

        try
        {
            var result = await adapter.CompleteAsync(request, apiKey, cancellationToken);

            _logger.LogDebug(
                "Harici YZ yanıtı alındı. Sağlayıcı: {Provider}, Uzunluk: {Length}",
                request.Provider, result.Length);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Harici YZ isteği başarısız. Sağlayıcı: {Provider}, Status: {Status}",
                request.Provider, ex.StatusCode);
            throw new InvalidOperationException(
                $"{request.Provider} API isteği başarısız oldu: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> StreamCompleteAsync(
        ExternalAIRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var adapter = GetAdapter(request.Provider);

        if (!adapter.SupportsStreaming)
        {
            // Streaming desteklenmiyorsa fallback: tek seferde al ve parça parça döndür
            _logger.LogWarning("{Provider} streaming desteklemiyor. Fallback kullanılıyor.", request.Provider);
            var result = await CompleteAsync(request, cancellationToken);
            yield return result;
            yield break;
        }

        var apiKey = await GetApiKeyAsync(request.Provider, cancellationToken);

        _logger.LogDebug("Harici YZ streaming başlatılıyor. Sağlayıcı: {Provider}", request.Provider);

        await foreach (var chunk in adapter.StreamCompleteAsync(request, apiKey, cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ExternalAIProvider>> GetAvailableProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        var providers = _adapters.Values.Select(a => new ExternalAIProvider(
            a.ProviderType,
            a.ProviderType.ToString(),
            a.AvailableModels,
            a.SupportsVision,
            a.SupportsStreaming,
            a.SupportsJsonMode
        )).ToList();

        return Task.FromResult<IReadOnlyList<ExternalAIProvider>>(providers);
    }

    // ─── Private Helpers ──────────────────────────────────────────

    private IProviderAdapter GetAdapter(AIProviderType provider)
    {
        if (!_adapters.TryGetValue(provider, out var adapter))
        {
            throw new InvalidOperationException(
                $"Sağlayıcı '{provider}' için kayıtlı adaptör bulunamadı. " +
                $"Mevcut sağlayıcılar: {string.Join(", ", _adapters.Keys)}");
        }

        return adapter;
    }

    private async Task<string> GetApiKeyAsync(
        AIProviderType provider,
        CancellationToken cancellationToken)
    {
        var secretKey = GetSecretKey(provider);
        var apiKey = await _secretManager.GetSecretAsync(secretKey, cancellationToken);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"'{provider}' için API anahtarı bulunamadı. " +
                $"Secret key: '{secretKey}' ile kayıt yapılmalıdır.");
        }

        return apiKey;
    }
}
