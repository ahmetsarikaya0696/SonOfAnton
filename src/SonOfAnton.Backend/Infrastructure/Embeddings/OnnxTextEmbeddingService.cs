using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace SonOfAnton.Backend.Infrastructure.Embeddings;

/// <summary>
/// ONNX inference session health durumu
/// </summary>
public class InferenceSessionHealth
{
    public bool IsHealthy { get; init; }
    public long TotalInferences { get; init; }
    public TimeSpan AverageLatency { get; init; }
    public long PeakMemoryUsage { get; init; }
    public DateTime LastInferenceAt { get; init; }
    public string ModelName { get; init; } = string.Empty;
}

/// <summary>
/// ONNX Runtime ile local text embedding servisi
/// Model: all-MiniLM-L6-v2 (384 dimension)
/// Batch inference, memory pooling, session yönetimi
/// </summary>
public class OnnxTextEmbeddingService : ITextEmbeddingService, IDisposable
{
    private readonly InferenceSession _session;
    private readonly ILogger<OnnxTextEmbeddingService> _logger;
    private readonly EmbeddingCacheService? _cache;
    private readonly Dictionary<string, int> _vocabulary;
    private readonly int _maxSequenceLength = 128;
    private readonly SemaphoreSlim _inferenceLock;
    private readonly string _modelName;
    private bool _disposed;
    
    // Performance tracking
    private long _totalInferences;
    private long _totalLatencyTicks;
    private long _peakMemoryUsage;
    private DateTime _lastInferenceAt;
    private bool _isWarmedUp;
    
    // Buffer pooling
    private readonly ArrayPool<long> _longPool = ArrayPool<long>.Shared;
    private readonly ArrayPool<float> _floatPool = ArrayPool<float>.Shared;
    
    public int Dimension => 384;
    public EmbeddingModelType ModelType => EmbeddingModelType.TextSmall;
    
    public OnnxTextEmbeddingService(
        string modelPath,
        string vocabularyPath,
        ILogger<OnnxTextEmbeddingService> logger,
        EmbeddingCacheService? cache = null,
        int maxConcurrency = 4)
    {
        _logger = logger;
        _cache = cache;
        _modelName = Path.GetFileName(Path.GetDirectoryName(modelPath)) ?? "unknown";
        _inferenceLock = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        
        // ONNX oturumu oluştur - optimized options
        var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            InterOpNumThreads = Math.Max(1, Environment.ProcessorCount / 2),
            IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount / 2),
            EnableMemoryPattern = true,
            EnableCpuMemArena = true,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
        };
        
        _session = new InferenceSession(modelPath, sessionOptions);
        
        // Vocabulary yükle
        _vocabulary = LoadVocabulary(vocabularyPath);
        
        _logger.LogInformation(
            "ONNX Text Embedding servisi başlatıldı. Model: {ModelPath}, Vocab size: {VocabSize}, MaxConcurrency: {MaxConcurrency}",
            modelPath, _vocabulary.Count, maxConcurrency);
    }
    
    /// <summary>
    /// Vocabulary dosyasını yükler
    /// </summary>
    private static Dictionary<string, int> LoadVocabulary(string path)
    {
        var vocab = new Dictionary<string, int>();
        var lines = File.ReadAllLines(path);
        
        for (int i = 0; i < lines.Length; i++)
        {
            vocab[lines[i]] = i;
        }
        
        return vocab;
    }
    
    /// <summary>
    /// Model warmup - ilk inference'ı hızlandırmak için dummy input ile çalıştır
    /// </summary>
    public async Task WarmupAsync()
    {
        if (_isWarmedUp) return;
        
        _logger.LogInformation("ONNX model warmup başlıyor: {Model}", _modelName);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            await GenerateEmbeddingInternalAsync("warmup test", CancellationToken.None);
            _isWarmedUp = true;
            sw.Stop();
            _logger.LogInformation("ONNX model warmup tamamlandı: {Model}, {Duration}ms", _modelName, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ONNX model warmup başarısız: {Model}", _modelName);
        }
    }
    
    /// <summary>
    /// Session health durumunu döndürür
    /// </summary>
    public InferenceSessionHealth GetHealth()
    {
        var totalInferences = Interlocked.Read(ref _totalInferences);
        var totalLatency = Interlocked.Read(ref _totalLatencyTicks);
        
        return new InferenceSessionHealth
        {
            IsHealthy = !_disposed && _session != null,
            TotalInferences = totalInferences,
            AverageLatency = totalInferences > 0 
                ? TimeSpan.FromTicks(totalLatency / totalInferences) 
                : TimeSpan.Zero,
            PeakMemoryUsage = Interlocked.Read(ref _peakMemoryUsage),
            LastInferenceAt = _lastInferenceAt,
            ModelName = _modelName
        };
    }
    
    public async Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        // Cache kontrolü
        if (_cache != null)
        {
            var cacheKey = EmbeddingCacheService.CreateTextKey(text, ModelType);
            return await _cache.GetOrCreateAsync(cacheKey, async () =>
            {
                return await GenerateEmbeddingInternalAsync(text, cancellationToken);
            });
        }
        
        return await GenerateEmbeddingInternalAsync(text, cancellationToken);
    }
    
    private async Task<EmbeddingResult> GenerateEmbeddingInternalAsync(string text, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        // Concurrency limiti
        await _inferenceLock.WaitAsync(cancellationToken);
        try
        {
            // Tokenize
            var (inputIds, attentionMask, tokenTypeIds) = Tokenize(text);
            
            // Tensor'lara dönüştür
            var inputIdsTensor = new DenseTensor<long>(inputIds, [1, inputIds.Length]);
            var attentionMaskTensor = new DenseTensor<long>(attentionMask, [1, attentionMask.Length]);
            var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, [1, tokenTypeIds.Length]);
            
            // Input'ları hazırla
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
                NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
            };
            
            // Inference
            using var results = _session.Run(inputs);
            var output = results.First().AsTensor<float>();
            
            // Mean pooling
            var embedding = MeanPooling(output, attentionMask);
            
            // L2 normalize
            var normalizedEmbedding = L2Normalize(embedding);
            
            sw.Stop();
            
            // Tracking
            Interlocked.Increment(ref _totalInferences);
            Interlocked.Add(ref _totalLatencyTicks, sw.Elapsed.Ticks);
            _lastInferenceAt = DateTime.UtcNow;
            
            var currentMemory = GC.GetTotalMemory(false);
            long currentPeak;
            do
            {
                currentPeak = Interlocked.Read(ref _peakMemoryUsage);
                if (currentMemory <= currentPeak) break;
            } while (Interlocked.CompareExchange(ref _peakMemoryUsage, currentMemory, currentPeak) != currentPeak);
            
            return new EmbeddingResult
            {
                Vector = normalizedEmbedding,
                Model = ModelType,
                ProcessingTime = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Embedding generation hatası: {Text}", text.Length > 50 ? text[..50] + "..." : text);
            throw;
        }
        finally
        {
            _inferenceLock.Release();
        }
    }
    
    /// <summary>
    /// Batch inference - birden fazla metni tek seferde işler (optimized)
    /// </summary>
    public async Task<IReadOnlyList<EmbeddingResult>> GenerateEmbeddingsAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        
        if (textList.Count == 0)
            return [];
        
        if (textList.Count == 1)
            return [await GenerateEmbeddingAsync(textList[0], cancellationToken)];
        
        // Batch inference: birden fazla metni aynı anda işle
        var results = new List<EmbeddingResult>(textList.Count);
        
        // Batch boyutunu belirle (bellek kullanımına göre)
        const int maxBatchSize = 16;
        
        for (int batchStart = 0; batchStart < textList.Count; batchStart += maxBatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var batchEnd = Math.Min(batchStart + maxBatchSize, textList.Count);
            var batch = textList.GetRange(batchStart, batchEnd - batchStart);
            
            var batchResults = await ProcessBatchAsync(batch, cancellationToken);
            results.AddRange(batchResults);
        }
        
        return results;
    }
    
    /// <summary>
    /// Bir batch'i işler - cache'li olanları atlar
    /// </summary>
    private async Task<List<EmbeddingResult>> ProcessBatchAsync(
        List<string> texts, 
        CancellationToken cancellationToken)
    {
        var results = new EmbeddingResult[texts.Count];
        var uncachedIndices = new List<int>();
        
        // Önce cache'e bak
        if (_cache != null)
        {
            for (int i = 0; i < texts.Count; i++)
            {
                var cacheKey = EmbeddingCacheService.CreateTextKey(texts[i], ModelType);
                var cached = await _cache.GetOrCreateAsync(cacheKey, () => Task.FromResult<EmbeddingResult>(null!));
                if (cached?.Vector != null)
                {
                    results[i] = cached;
                }
                else
                {
                    uncachedIndices.Add(i);
                }
            }
        }
        else
        {
            uncachedIndices.AddRange(Enumerable.Range(0, texts.Count));
        }
        
        // Uncached olanları compute et
        if (uncachedIndices.Count > 0)
        {
            await _inferenceLock.WaitAsync(cancellationToken);
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var batchSize = uncachedIndices.Count;
                
                // Her text için tokenize
                var allInputIds = new long[batchSize * _maxSequenceLength];
                var allAttentionMasks = new long[batchSize * _maxSequenceLength];
                var allTokenTypeIds = new long[batchSize * _maxSequenceLength];
                var attentionMasks = new long[batchSize][];
                
                for (int i = 0; i < batchSize; i++)
                {
                    var (inputIds, attentionMask, tokenTypeIds) = Tokenize(texts[uncachedIndices[i]]);
                    Array.Copy(inputIds, 0, allInputIds, i * _maxSequenceLength, _maxSequenceLength);
                    Array.Copy(attentionMask, 0, allAttentionMasks, i * _maxSequenceLength, _maxSequenceLength);
                    Array.Copy(tokenTypeIds, 0, allTokenTypeIds, i * _maxSequenceLength, _maxSequenceLength);
                    attentionMasks[i] = attentionMask;
                }
                
                // Batch tensors
                var inputIdsTensor = new DenseTensor<long>(allInputIds, [batchSize, _maxSequenceLength]);
                var attentionMaskTensor = new DenseTensor<long>(allAttentionMasks, [batchSize, _maxSequenceLength]);
                var tokenTypeIdsTensor = new DenseTensor<long>(allTokenTypeIds, [batchSize, _maxSequenceLength]);
                
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                    NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
                    NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
                };
                
                using var onnxResults = _session.Run(inputs);
                var output = onnxResults.First().AsTensor<float>();
                
                sw.Stop();
                var perItemTime = TimeSpan.FromTicks(sw.Elapsed.Ticks / batchSize);
                
                // Her item için mean pooling ve normalize
                for (int i = 0; i < batchSize; i++)
                {
                    var embedding = MeanPoolingBatch(output, attentionMasks[i], i);
                    var normalized = L2Normalize(embedding);
                    
                    results[uncachedIndices[i]] = new EmbeddingResult
                    {
                        Vector = normalized,
                        Model = ModelType,
                        ProcessingTime = perItemTime
                    };
                }
                
                Interlocked.Add(ref _totalInferences, batchSize);
                Interlocked.Add(ref _totalLatencyTicks, sw.Elapsed.Ticks);
                _lastInferenceAt = DateTime.UtcNow;
            }
            finally
            {
                _inferenceLock.Release();
            }
        }
        
        return results.ToList();
    }
    
    /// <summary>
    /// Basit WordPiece tokenizer
    /// </summary>
    private (long[] InputIds, long[] AttentionMask, long[] TokenTypeIds) Tokenize(string text)
    {
        // Normalize ve lowercase
        text = text.ToLowerInvariant();
        text = Regex.Replace(text, @"\s+", " ").Trim();
        
        var tokens = new List<int>();
        tokens.Add(_vocabulary.GetValueOrDefault("[CLS]", 101)); // CLS token
        
        // Basit word tokenization
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var word in words)
        {
            if (tokens.Count >= _maxSequenceLength - 1) break;
            
            // WordPiece: kelimeyi parçalara ayır
            var subTokens = WordPieceTokenize(word);
            foreach (var subToken in subTokens)
            {
                if (tokens.Count >= _maxSequenceLength - 1) break;
                tokens.Add(subToken);
            }
        }
        
        tokens.Add(_vocabulary.GetValueOrDefault("[SEP]", 102)); // SEP token
        
        // Padding
        var paddingLength = _maxSequenceLength - tokens.Count;
        var inputIds = tokens.Select(t => (long)t).Concat(Enumerable.Repeat(0L, paddingLength)).ToArray();
        var attentionMask = Enumerable.Repeat(1L, tokens.Count).Concat(Enumerable.Repeat(0L, paddingLength)).ToArray();
        var tokenTypeIds = Enumerable.Repeat(0L, _maxSequenceLength).ToArray();
        
        return (inputIds, attentionMask, tokenTypeIds);
    }
    
    /// <summary>
    /// WordPiece tokenization
    /// </summary>
    private List<int> WordPieceTokenize(string word)
    {
        var tokens = new List<int>();
        var start = 0;
        
        while (start < word.Length)
        {
            var end = word.Length;
            var found = false;
            
            while (start < end)
            {
                var subword = start == 0 ? word[start..end] : "##" + word[start..end];
                
                if (_vocabulary.TryGetValue(subword, out var tokenId))
                {
                    tokens.Add(tokenId);
                    found = true;
                    start = end;
                    break;
                }
                
                end--;
            }
            
            if (!found)
            {
                // Bilinmeyen token [UNK]
                tokens.Add(_vocabulary.GetValueOrDefault("[UNK]", 100));
                start++;
            }
        }
        
        return tokens;
    }
    
    /// <summary>
    /// Mean pooling - attention mask kullanarak ortalama al
    /// </summary>
    private static float[] MeanPooling(Tensor<float> output, long[] attentionMask)
    {
        var hiddenSize = output.Dimensions[2];
        var seqLength = output.Dimensions[1];
        var embedding = new float[hiddenSize];
        var maskSum = attentionMask.Sum();
        
        for (int h = 0; h < hiddenSize; h++)
        {
            float sum = 0;
            for (int s = 0; s < seqLength; s++)
            {
                sum += output[0, s, h] * attentionMask[s];
            }
            embedding[h] = sum / maskSum;
        }
        
        return embedding;
    }
    
    /// <summary>
    /// Mean pooling - batch version (belirli batch index için)
    /// </summary>
    private static float[] MeanPoolingBatch(Tensor<float> output, long[] attentionMask, int batchIndex)
    {
        var hiddenSize = output.Dimensions[2];
        var seqLength = output.Dimensions[1];
        var embedding = new float[hiddenSize];
        var maskSum = attentionMask.Sum();
        
        for (int h = 0; h < hiddenSize; h++)
        {
            float sum = 0;
            for (int s = 0; s < seqLength; s++)
            {
                sum += output[batchIndex, s, h] * attentionMask[s];
            }
            embedding[h] = sum / maskSum;
        }
        
        return embedding;
    }
    
    /// <summary>
    /// L2 normalization
    /// </summary>
    private static float[] L2Normalize(float[] vector)
    {
        var norm = MathF.Sqrt(vector.Sum(x => x * x));
        if (norm < 1e-12f) return vector;
        
        return vector.Select(x => x / norm).ToArray();
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _session?.Dispose();
            _inferenceLock?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Semantic Kernel Ollama tabanlı embedding servisi (fallback)
/// </summary>
public class OllamaTextEmbeddingService : ITextEmbeddingService
{
    private readonly Microsoft.SemanticKernel.Kernel _kernel;
    private readonly ILogger<OllamaTextEmbeddingService> _logger;
    private readonly EmbeddingCacheService? _cache;
    
    public int Dimension => 768; // nomic-embed-text
    public EmbeddingModelType ModelType => EmbeddingModelType.TextMedium;
    
    public OllamaTextEmbeddingService(
        Microsoft.SemanticKernel.Kernel kernel,
        ILogger<OllamaTextEmbeddingService> logger,
        EmbeddingCacheService? cache = null)
    {
        _kernel = kernel;
        _logger = logger;
        _cache = cache;
    }
    
    public async Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_cache != null)
        {
            var cacheKey = EmbeddingCacheService.CreateTextKey(text, ModelType);
            return await _cache.GetOrCreateAsync(cacheKey, async () =>
            {
                return await GenerateEmbeddingInternalAsync(text, cancellationToken);
            });
        }
        
        return await GenerateEmbeddingInternalAsync(text, cancellationToken);
    }
    
    private async Task<EmbeddingResult> GenerateEmbeddingInternalAsync(string text, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            #pragma warning disable SKEXP0001, CS0618
            var embeddingService = _kernel.GetRequiredService<Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService>();
            var embeddings = await embeddingService.GenerateEmbeddingsAsync([text], _kernel, cancellationToken);
            #pragma warning restore SKEXP0001, CS0618
            
            sw.Stop();
            
            return new EmbeddingResult
            {
                Vector = embeddings.First().ToArray(),
                Model = ModelType,
                ProcessingTime = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama embedding hatası: {Text}", text.Length > 50 ? text[..50] + "..." : text);
            throw;
        }
    }
    
    public async Task<IReadOnlyList<EmbeddingResult>> GenerateEmbeddingsAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var results = new List<EmbeddingResult>();
        
        foreach (var text in texts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await GenerateEmbeddingAsync(text, cancellationToken));
        }
        
        return results;
    }
}
