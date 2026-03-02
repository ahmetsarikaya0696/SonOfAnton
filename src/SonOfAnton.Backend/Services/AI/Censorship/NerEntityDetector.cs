using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SonOfAnton.Shared.Core;

namespace SonOfAnton.Backend.Services.AI.Censorship;

/// <summary>
/// ONNX Runtime ile yerel NER (Named Entity Recognition) modeli.
/// Kişi adı (PER), organizasyon (ORG), konum (LOC) tespiti yapar.
/// Projede mevcut Microsoft.ML.OnnxRuntime bağımlılığını kullanır.
/// </summary>
public class NerEntityDetector : IDisposable
{
    private readonly ILogger<NerEntityDetector> _logger;
    private readonly Dictionary<string, int> _vocabulary;
    private readonly Dictionary<int, string> _labelMap;
    private readonly int _maxSequenceLength = 128;
    private readonly SemaphoreSlim _inferenceLock;
    private InferenceSession? _session;
    private bool _disposed;
    private bool _isModelLoaded;

    /// <summary>
    /// NER etiket eşleme tablosu (IOB2 formatı)
    /// </summary>
    private static readonly Dictionary<string, NerLabel> LabelMapping = new()
    {
        ["B-PER"] = NerLabel.Person,
        ["I-PER"] = NerLabel.Person,
        ["B-ORG"] = NerLabel.Organization,
        ["I-ORG"] = NerLabel.Organization,
        ["B-LOC"] = NerLabel.Location,
        ["I-LOC"] = NerLabel.Location,
    };

    public NerEntityDetector(ILogger<NerEntityDetector> logger, int maxConcurrency = 2)
    {
        _logger = logger;
        _vocabulary = new Dictionary<string, int>();
        _labelMap = new Dictionary<int, string>();
        _inferenceLock = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    /// <summary>
    /// ONNX NER modelini ve sözlüğü yükler
    /// </summary>
    public void LoadModel(string modelPath, string vocabularyPath, string labelMapPath)
    {
        try
        {
            // Vocabulary yükle
            var lines = File.ReadAllLines(vocabularyPath);
            for (int i = 0; i < lines.Length; i++)
            {
                _vocabulary[lines[i]] = i;
            }

            // Label map yükle
            var labelLines = File.ReadAllLines(labelMapPath);
            for (int i = 0; i < labelLines.Length; i++)
            {
                _labelMap[i] = labelLines[i].Trim();
            }

            // ONNX oturumu oluştur
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
            _isModelLoaded = true;

            _logger.LogInformation(
                "NER modeli yüklendi. Model: {ModelPath}, Vocab: {VocabSize}, Labels: {LabelCount}",
                modelPath, _vocabulary.Count, _labelMap.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NER modeli yüklenemedi: {ModelPath}", modelPath);
            _isModelLoaded = false;
        }
    }

    /// <summary>
    /// Model yüklü ve hazır mı?
    /// </summary>
    public bool IsReady => _isModelLoaded && _session != null;

    /// <summary>
    /// Metindeki kişi, organizasyon ve konum varlıklarını tespit eder
    /// </summary>
    public async Task<IReadOnlyList<NerEntity>> DetectEntitiesAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (!IsReady)
        {
            _logger.LogWarning("NER modeli yüklü değil. Boş sonuç döndürülüyor.");
            return Array.Empty<NerEntity>();
        }

        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<NerEntity>();

        await _inferenceLock.WaitAsync(cancellationToken);
        try
        {
            return RunInference(text, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NER tespiti başarısız: {Text}", text.Length > 50 ? text[..50] + "..." : text);
            return Array.Empty<NerEntity>();
        }
        finally
        {
            _inferenceLock.Release();
        }
    }

    private IReadOnlyList<NerEntity> RunInference(string text, CancellationToken cancellationToken)
    {
        // Tokenize
        var (tokens, tokenToCharMap) = TokenizeWithMapping(text);

        // Tensör oluştur
        var inputIds = tokens.Select(t => (long)t).ToArray();
        var attentionMask = Enumerable.Repeat(1L, tokens.Count)
            .Concat(Enumerable.Repeat(0L, _maxSequenceLength - tokens.Count))
            .ToArray();

        // Padding
        var paddedInputIds = inputIds
            .Concat(Enumerable.Repeat(0L, _maxSequenceLength - inputIds.Length))
            .ToArray();

        var inputIdsTensor = new DenseTensor<long>(paddedInputIds, [1, _maxSequenceLength]);
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, [1, _maxSequenceLength]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
        };

        // Token type IDs yoksa ekleme, varsa ekle
        if (_session!.InputNames.Contains("token_type_ids"))
        {
            var tokenTypeIds = Enumerable.Repeat(0L, _maxSequenceLength).ToArray();
            var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, [1, _maxSequenceLength]);
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Inference
        using var results = _session.Run(inputs);
        var output = results.First().AsTensor<float>();

        // Tahminleri parse et
        var entities = ParsePredictions(text, output, tokens.Count, tokenToCharMap);

        _logger.LogDebug("NER tespiti tamamlandı. {Count} varlık bulundu.", entities.Count);
        return entities;
    }

    private List<NerEntity> ParsePredictions(
        string text,
        Tensor<float> output,
        int tokenCount,
        List<(int Start, int End)> tokenToCharMap)
    {
        var entities = new List<NerEntity>();
        var numLabels = output.Dimensions[2];

        NerEntity? currentEntity = null;
        var entityTokens = new List<string>();

        for (int i = 1; i < tokenCount - 1; i++) // CLS ve SEP atla
        {
            // Softmax ile en yüksek skoru bul
            var maxScore = float.MinValue;
            var maxLabel = 0;

            for (int j = 0; j < numLabels; j++)
            {
                var score = output[0, i, j];
                if (score > maxScore)
                {
                    maxScore = score;
                    maxLabel = j;
                }
            }

            var labelStr = _labelMap.GetValueOrDefault(maxLabel, "O");
            var confidence = Softmax(output, i, numLabels, maxLabel);

            if (labelStr.StartsWith("B-") && LabelMapping.TryGetValue(labelStr, out var nerLabel))
            {
                // Önceki entity'yi kaydet
                if (currentEntity != null)
                {
                    entities.Add(currentEntity);
                }

                // Yeni entity başlat
                var charStart = i < tokenToCharMap.Count ? tokenToCharMap[i].Start : 0;
                var charEnd = i < tokenToCharMap.Count ? tokenToCharMap[i].End : text.Length;

                currentEntity = new NerEntity(
                    nerLabel,
                    text[charStart..charEnd],
                    confidence,
                    charStart,
                    charEnd);
            }
            else if (labelStr.StartsWith("I-") && currentEntity != null &&
                     LabelMapping.TryGetValue(labelStr, out var contLabel) &&
                     contLabel == currentEntity.Label)
            {
                // Mevcut entity'yi genişlet
                var charEnd = i < tokenToCharMap.Count ? tokenToCharMap[i].End : text.Length;
                currentEntity = currentEntity with
                {
                    Text = text[currentEntity.StartIndex..charEnd],
                    EndIndex = charEnd,
                    Confidence = Math.Min(currentEntity.Confidence, confidence)
                };
            }
            else
            {
                // O etiketi — entity bitti
                if (currentEntity != null)
                {
                    entities.Add(currentEntity);
                    currentEntity = null;
                }
            }
        }

        // Son entity'yi kaydet
        if (currentEntity != null)
        {
            entities.Add(currentEntity);
        }

        return entities;
    }

    private (List<int> Tokens, List<(int Start, int End)> CharMap) TokenizeWithMapping(string text)
    {
        var tokens = new List<int>();
        var charMap = new List<(int Start, int End)>();

        // CLS token
        tokens.Add(_vocabulary.GetValueOrDefault("[CLS]", 101));
        charMap.Add((0, 0));

        var words = SplitWithPositions(text);

        foreach (var (word, start, end) in words)
        {
            if (tokens.Count >= _maxSequenceLength - 1) break;

            var wordLower = word.ToLowerInvariant();

            if (_vocabulary.TryGetValue(wordLower, out var tokenId))
            {
                tokens.Add(tokenId);
                charMap.Add((start, end));
            }
            else
            {
                // WordPiece
                var subStart = 0;
                while (subStart < wordLower.Length && tokens.Count < _maxSequenceLength - 1)
                {
                    var subEnd = wordLower.Length;
                    var found = false;

                    while (subStart < subEnd)
                    {
                        var sub = subStart == 0 ? wordLower[subStart..subEnd] : "##" + wordLower[subStart..subEnd];

                        if (_vocabulary.TryGetValue(sub, out var subTokenId))
                        {
                            tokens.Add(subTokenId);
                            charMap.Add((start + subStart, start + subEnd));
                            subStart = subEnd;
                            found = true;
                            break;
                        }
                        subEnd--;
                    }

                    if (!found)
                    {
                        tokens.Add(_vocabulary.GetValueOrDefault("[UNK]", 100));
                        charMap.Add((start + subStart, start + subStart + 1));
                        subStart++;
                    }
                }
            }
        }

        // SEP token
        tokens.Add(_vocabulary.GetValueOrDefault("[SEP]", 102));
        charMap.Add((text.Length, text.Length));

        return (tokens, charMap);
    }

    private static List<(string Word, int Start, int End)> SplitWithPositions(string text)
    {
        var result = new List<(string, int, int)>();
        var start = 0;
        var inWord = false;
        var wordStart = 0;

        for (int i = 0; i <= text.Length; i++)
        {
            var isSpace = i == text.Length || char.IsWhiteSpace(text[i]);

            if (!isSpace && !inWord)
            {
                wordStart = i;
                inWord = true;
            }
            else if (isSpace && inWord)
            {
                result.Add((text[wordStart..i], wordStart, i));
                inWord = false;
            }
        }

        return result;
    }

    private static double Softmax(Tensor<float> output, int tokenIndex, int numLabels, int targetLabel)
    {
        var maxVal = float.MinValue;
        for (int j = 0; j < numLabels; j++)
        {
            maxVal = Math.Max(maxVal, output[0, tokenIndex, j]);
        }

        var sumExp = 0.0;
        for (int j = 0; j < numLabels; j++)
        {
            sumExp += Math.Exp(output[0, tokenIndex, j] - maxVal);
        }

        return Math.Exp(output[0, tokenIndex, targetLabel] - maxVal) / sumExp;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _session?.Dispose();
            _inferenceLock.Dispose();
            _disposed = true;
        }
    }
}
