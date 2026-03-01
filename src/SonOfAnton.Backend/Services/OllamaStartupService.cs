using System.Diagnostics;
using System.Net.Http;

namespace SonOfAnton.Backend.Services;

/// <summary>
/// Uygulama ba�larken Ollama'n�n �al��t���ndan emin olur.
/// �al��m�yorsa otomatik ba�latmaya �al���r.
/// </summary>
public class OllamaStartupService : IHostedService
{
    private readonly ILogger<OllamaStartupService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private Process? _ollamaProcess;

    public OllamaStartupService(
        ILogger<OllamaStartupService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var endpoint = _configuration.GetValue<string>("Ollama:Endpoint") ?? "http://localhost:11434";
        
        _logger.LogInformation("Ollama ba�lant�s� kontrol ediliyor: {Endpoint}", endpoint);

        // �nce Ollama'n�n �al���p �al��mad���n� kontrol et
        if (await IsOllamaRunningAsync(endpoint, cancellationToken))
        {
            _logger.LogInformation("Ollama zaten �al���yor");
            return;
        }

        // Ollama �al��m�yorsa ba�latmay� dene
        _logger.LogWarning("Ollama �al��m�yor, ba�lat�lmaya �al���l�yor...");
        
        if (!TryStartOllama())
        {
            _logger.LogError(
                "Ollama ba�lat�lamad�! L�tfen manuel olarak ba�lat�n: 'ollama serve' veya Ollama uygulamas�n� a��n.");
            return;
        }

        // Ollama'n�n haz�r olmas�n� bekle
        var maxRetries = 30;
        var retryDelay = TimeSpan.FromSeconds(1);
        
        for (int i = 0; i < maxRetries; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            if (await IsOllamaRunningAsync(endpoint, cancellationToken))
            {
                _logger.LogInformation("Ollama ba�ar�yla ba�lat�ld�");
                await EnsureModelsExistAsync(cancellationToken);
                return;
            }

            _logger.LogDebug("Ollama hen�z haz�r de�il, bekleniyor... ({Retry}/{MaxRetries})", i + 1, maxRetries);
            await Task.Delay(retryDelay, cancellationToken);
        }

        _logger.LogError("Ollama {MaxRetries} saniye i�inde ba�lat�lamad�", maxRetries);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Uygulama taraf�ndan ba�lat�lan Ollama process'ini sonland�rma
        // Ollama genellikle sistem genelinde kullan�ld��� i�in bunu yapm�yoruz
        return Task.CompletedTask;
    }

    private async Task<bool> IsOllamaRunningAsync(string endpoint, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{endpoint}/api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private bool TryStartOllama()
    {
        try
        {
            // Windows'ta Ollama'y� ba�lat
            var startInfo = new ProcessStartInfo
            {
                FileName = "ollama",
                Arguments = "serve",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _ollamaProcess = Process.Start(startInfo);
            
            if (_ollamaProcess == null)
            {
                _logger.LogWarning("Ollama process ba�lat�lamad�");
                return false;
            }


            _logger.LogInformation("Ollama process ba�lat�ld� (PID: {ProcessId})", _ollamaProcess.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama ba�lat�l�rken hata olu�tu. Ollama y�kl� olmayabilir.");
            return false;
        }
    }

    private async Task EnsureModelsExistAsync(CancellationToken cancellationToken)
    {
        var endpoint = _configuration.GetValue<string>("Ollama:Endpoint") ?? "http://localhost:11434";
        var chatModel = _configuration.GetValue<string>("Ollama:Model") ?? "qwen2.5-coder:14b";
        var embeddingModel = _configuration.GetValue<string>("Ollama:EmbeddingModel") ?? "nomic-embed-text";

        try
        {
            var response = await _httpClient.GetAsync($"{endpoint}/api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return;

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Chat modeli kontrol�
            if (!content.Contains(chatModel.Split(':')[0]))
            {
                _logger.LogWarning(
                    "Chat modeli bulunamad�: {Model}. Otomatik indiriliyor...", 
                    chatModel);
                await PullModelAsync(endpoint, chatModel, cancellationToken);
            }

            // Embedding modeli kontrol�
            if (!content.Contains(embeddingModel.Split(':')[0]))
            {
                _logger.LogWarning(
                    "Embedding modeli bulunamad�: {Model}. Otomatik indiriliyor...", 
                    embeddingModel);
                await PullModelAsync(endpoint, embeddingModel, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Model kontrol� yap�l�rken hata olu�tu");
        }
    }

    private async Task PullModelAsync(string endpoint, string modelName, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Model indiriliyor: {Model} (bu biraz zaman alabilir...)", modelName);
            
            var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/api/pull")
            {
                Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(new { name = modelName }),
                    System.Text.Encoding.UTF8,
                    "application/json")
            };

            // Timeout'u uzun tut - model indirme uzun s�rebilir
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(30));

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            
            if (response.IsSuccessStatusCode)
            {
                // Stream'i oku - progress g�ster
                using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                using var reader = new StreamReader(stream);
                
                while (!cts.Token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cts.Token);
                    if (line == null) break;

                    if (!string.IsNullOrEmpty(line) && line.Contains("status"))
                    {
                        _logger.LogDebug("Model indirme durumu: {Status}", line);
                    }
                }
                
                _logger.LogInformation("Model ba�ar�yla indirildi: {Model}", modelName);
            }
            else
            {
                _logger.LogError("Model indirilemedi: {Model} - {StatusCode}", modelName, response.StatusCode);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Model indirme zaman a��m�na u�rad� veya iptal edildi: {Model}", modelName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Model indirilirken hata olu�tu: {Model}", modelName);
        }
    }
}

