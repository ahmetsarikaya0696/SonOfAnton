using System.IO.Compression;
using System.Security.Cryptography;

namespace SonOfAnton.Backend.Services;

/// <summary>
/// ONNX model dosyalarını otomatik indirir ve yönetir
/// </summary>
public class ModelDownloadService : IHostedService
{
    private readonly ILogger<ModelDownloadService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _modelsDirectory;
    
    // Desteklenen modeller ve indirme URL'leri
    private static readonly Dictionary<string, ModelInfo> AvailableModels = new()
    {
        ["all-MiniLM-L6-v2"] = new ModelInfo
        {
            Name = "all-MiniLM-L6-v2",
            Description = "Sentence Transformers - Fast text embedding (384 dim)",
            ModelUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx",
            VocabUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt",
            Size = "86MB",
            Dimensions = 384
        },
        ["bge-small-en-v1.5"] = new ModelInfo
        {
            Name = "bge-small-en-v1.5",
            Description = "BGE Small - High quality text embedding (384 dim)",
            ModelUrl = "https://huggingface.co/BAAI/bge-small-en-v1.5/resolve/main/onnx/model.onnx",
            VocabUrl = "https://huggingface.co/BAAI/bge-small-en-v1.5/resolve/main/vocab.txt",
            Size = "133MB",
            Dimensions = 384
        }
    };
    
    public ModelDownloadService(
        ILogger<ModelDownloadService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SonOfAnton/1.0");
        
        // Model dizini
        _modelsDirectory = configuration.GetValue<string>("ModelDirectory") 
            ?? Path.Combine(AppContext.BaseDirectory, "models");
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Model Download Service başlatılıyor...");
        
        // Model dizinini oluştur
        Directory.CreateDirectory(_modelsDirectory);
        
        // Varsayılan modeli kontrol et ve indir
        var defaultModel = _configuration.GetValue<string>("DefaultEmbeddingModel") ?? "all-MiniLM-L6-v2";
        
        if (AvailableModels.TryGetValue(defaultModel, out var modelInfo))
        {
            if (!IsModelDownloaded(defaultModel))
            {
                _logger.LogInformation("Varsayılan model indiriliyor: {Model}", defaultModel);
                await DownloadModelAsync(defaultModel, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Model zaten mevcut: {Model}", defaultModel);
            }
        }
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _httpClient.Dispose();
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Modelin indirilip indirilmediğini kontrol eder
    /// </summary>
    public bool IsModelDownloaded(string modelName)
    {
        var modelPath = GetModelPath(modelName);
        var vocabPath = GetVocabPath(modelName);
        
        return File.Exists(modelPath) && File.Exists(vocabPath);
    }
    
    /// <summary>
    /// Model ONNX dosyasının yolunu döndürür
    /// </summary>
    public string GetModelPath(string modelName)
    {
        return Path.Combine(_modelsDirectory, modelName, "model.onnx");
    }
    
    /// <summary>
    /// Vocabulary dosyasının yolunu döndürür
    /// </summary>
    public string GetVocabPath(string modelName)
    {
        return Path.Combine(_modelsDirectory, modelName, "vocab.txt");
    }
    
    /// <summary>
    /// Modeli indirir
    /// </summary>
    public async Task DownloadModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        if (!AvailableModels.TryGetValue(modelName, out var modelInfo))
        {
            throw new ArgumentException($"Bilinmeyen model: {modelName}");
        }
        
        var modelDir = Path.Combine(_modelsDirectory, modelName);
        Directory.CreateDirectory(modelDir);
        
        var modelPath = Path.Combine(modelDir, "model.onnx");
        var vocabPath = Path.Combine(modelDir, "vocab.txt");
        
        // Model dosyasını indir
        if (!File.Exists(modelPath))
        {
            _logger.LogInformation("Model dosyası indiriliyor: {Url}", modelInfo.ModelUrl);
            await DownloadFileWithProgressAsync(modelInfo.ModelUrl, modelPath, cancellationToken);
        }
        
        // Vocabulary dosyasını indir
        if (!File.Exists(vocabPath))
        {
            _logger.LogInformation("Vocabulary dosyası indiriliyor: {Url}", modelInfo.VocabUrl);
            await DownloadFileWithProgressAsync(modelInfo.VocabUrl, vocabPath, cancellationToken);
        }
        
        _logger.LogInformation("Model indirme tamamlandı: {Model}", modelName);
    }
    
    /// <summary>
    /// Progress ile dosya indirir
    /// </summary>
    private async Task DownloadFileWithProgressAsync(string url, string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes > 0;
            
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            
            var buffer = new byte[81920];
            var totalRead = 0L;
            var lastProgressReport = 0;
            
            while (true)
            {
                var read = await contentStream.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;
                
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                totalRead += read;
                
                if (canReportProgress)
                {
                    var progress = (int)(totalRead * 100 / totalBytes);
                    if (progress >= lastProgressReport + 10)
                    {
                        _logger.LogInformation("İndirme: {Progress}% ({Downloaded}/{Total} MB)",
                            progress, totalRead / 1024 / 1024, totalBytes / 1024 / 1024);
                        lastProgressReport = progress;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dosya indirme hatası: {Url}", url);
            
            // Başarısız indirmeyi temizle
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            
            throw;
        }
    }
    
    /// <summary>
    /// Mevcut modelleri listeler
    /// </summary>
    public IEnumerable<ModelStatus> GetModelStatuses()
    {
        foreach (var (name, info) in AvailableModels)
        {
            var modelPath = GetModelPath(name);
            var isDownloaded = File.Exists(modelPath);
            long? fileSize = null;
            
            if (isDownloaded)
            {
                var fileInfo = new FileInfo(modelPath);
                fileSize = fileInfo.Length;
            }
            
            yield return new ModelStatus
            {
                Name = name,
                Description = info.Description,
                IsDownloaded = isDownloaded,
                ExpectedSize = info.Size,
                ActualSize = fileSize.HasValue ? $"{fileSize.Value / 1024 / 1024}MB" : null,
                Dimensions = info.Dimensions
            };
        }
    }
    
    /// <summary>
    /// Modeli siler
    /// </summary>
    public void DeleteModel(string modelName)
    {
        var modelDir = Path.Combine(_modelsDirectory, modelName);
        if (Directory.Exists(modelDir))
        {
            Directory.Delete(modelDir, true);
            _logger.LogInformation("Model silindi: {Model}", modelName);
        }
    }
}

/// <summary>
/// Model bilgisi
/// </summary>
public class ModelInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ModelUrl { get; set; } = string.Empty;
    public string VocabUrl { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public int Dimensions { get; set; }
}

/// <summary>
/// Model durumu
/// </summary>
public class ModelStatus
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDownloaded { get; set; }
    public string ExpectedSize { get; set; } = string.Empty;
    public string? ActualSize { get; set; }
    public int Dimensions { get; set; }
}
