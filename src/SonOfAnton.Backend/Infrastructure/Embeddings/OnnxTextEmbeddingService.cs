using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Text;
using System.Text.RegularExpressions;

namespace SonOfAnton.Backend.Infrastructure.Embeddings;

/// <summary>
/// ONNX Runtime ile local text embedding servisi
/// Model: all-MiniLM-L6-v2 (384 dimension)
/// </summary>
public class OnnxTextEmbeddingService : ITextEmbeddingService, IDisposable
{
    private readonly InferenceSession _session;
    private readonly ILogger<OnnxTextEmbeddingService> _logger;
    private readonly EmbeddingCacheService? _cache;
    private readonly Dictionary<string, int> _vocabulary;
    private readonly int _maxSequenceLength = 128;
    private bool _disposed;
    
    public int Dimension => 384;
    public EmbeddingModelType ModelType => EmbeddingModelType.TextSmall;
    
    public OnnxTextEmbeddingService(
        string modelPath,
        string vocabularyPath,
        ILogger<OnnxTextEmbeddingService> logger,
        EmbeddingCacheService? cache = null)
    {
        _logger = logger;
        _cache = cache;
        
        // ONNX oturumu oluştur
        var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            InterOpNumThreads = Environment.ProcessorCount,
            IntraOpNumThreads = Environment.ProcessorCount
        };
        
        _session = new InferenceSession(modelPath, sessionOptions);
        
        // Vocabulary yükle
        _vocabulary = LoadVocabulary(vocabularyPath);
        
        _logger.LogInformation(
            "ONNX Text Embedding servisi başlatıldı. Model: {ModelPath}, Vocab size: {VocabSize}",
            modelPath, _vocabulary.Count);
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
    
    private Task<EmbeddingResult> GenerateEmbeddingInternalAsync(string text, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Tokenize
            var (inputIds, attentionMask, tokenTypeIds) = Tokenize(text);
            
            // Tensor'lara dönüştür
            var inputIdsTensor = new DenseTensor<long>(inputIds, new[] { 1, inputIds.Length });
            var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, attentionMask.Length });
            var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, new[] { 1, tokenTypeIds.Length });
            
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
            
            return Task.FromResult(new EmbeddingResult
            {
                Vector = normalizedEmbedding,
                Model = ModelType,
                ProcessingTime = sw.Elapsed
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Embedding generation hatası: {Text}", text.Length > 50 ? text[..50] + "..." : text);
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
            #pragma warning disable SKEXP0001
            var embeddingService = _kernel.GetRequiredService<Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService>();
            var embeddings = await embeddingService.GenerateEmbeddingsAsync([text], _kernel, cancellationToken);
            #pragma warning restore SKEXP0001
            
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
