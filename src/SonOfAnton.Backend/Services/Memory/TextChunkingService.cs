using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Backend.Services.Memory;

/// <summary>
/// Metin parçalama (chunking) sonucu
/// </summary>
public record TextChunk
{
    /// <summary>Chunk içeriği</summary>
    public required string Content { get; init; }

    /// <summary>Chunk sıra numarası (0-based)</summary>
    public int Index { get; init; }

    /// <summary>Orijinal metindeki başlangıç pozisyonu</summary>
    public int StartOffset { get; init; }

    /// <summary>Orijinal metindeki bitiş pozisyonu</summary>
    public int EndOffset { get; init; }

    /// <summary>Tahmini token sayısı (kelime sayısı × 1.3)</summary>
    public int EstimatedTokenCount { get; init; }
}

/// <summary>
/// Chunking seçenekleri
/// </summary>
public record ChunkingOptions
{
    /// <summary>Parçalama stratejisi</summary>
    public ChunkingStrategy Strategy { get; init; } = ChunkingStrategy.Paragraph;

    /// <summary>Maksimum chunk boyutu (karakter)</summary>
    public int MaxChunkSize { get; init; } = 512;

    /// <summary>Chunk'lar arası örtüşme boyutu (karakter)</summary>
    public int OverlapSize { get; init; } = 50;

    /// <summary>Minimum chunk boyutu (karakter) — bundan küçük chunk'lar bir sonrakiyle birleştirilir</summary>
    public int MinChunkSize { get; init; } = 50;
}

/// <summary>
/// Uzun dökümanların işlenmesi için metin parçalama servisi arayüzü
/// </summary>
public interface ITextChunkingService
{
    /// <summary>
    /// Metni belirtilen stratejiye göre parçalara ayırır
    /// </summary>
    IReadOnlyList<TextChunk> ChunkText(string text, ChunkingOptions? options = null);
}

/// <summary>
/// C# metin parçalama servisi — Paragraf, Cümle, Token Window, Sabit Boyut stratejileri
/// </summary>
public class TextChunkingService : ITextChunkingService
{
    private readonly ILogger<TextChunkingService> _logger;

    public TextChunkingService(ILogger<TextChunkingService> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<TextChunk> ChunkText(string text, ChunkingOptions? options = null)
    {
        options ??= new ChunkingOptions();

        if (string.IsNullOrWhiteSpace(text))
            return [];

        // Metin zaten max boyutun altındaysa tek chunk olarak döndür
        if (text.Length <= options.MaxChunkSize)
        {
            return [new TextChunk
            {
                Content = text,
                Index = 0,
                StartOffset = 0,
                EndOffset = text.Length,
                EstimatedTokenCount = EstimateTokenCount(text)
            }];
        }

        var rawChunks = options.Strategy switch
        {
            ChunkingStrategy.Paragraph => ChunkByParagraph(text, options),
            ChunkingStrategy.SentenceBased => ChunkBySentence(text, options),
            ChunkingStrategy.TokenWindow => ChunkByTokenWindow(text, options),
            ChunkingStrategy.FixedSize => ChunkByFixedSize(text, options),
            _ => ChunkByParagraph(text, options)
        };

        // Minimum boyut kontrolü — küçük chunk'ları birleştir
        var merged = MergeSmallChunks(rawChunks, options.MinChunkSize, options.MaxChunkSize);

        _logger.LogDebug(
            "Metin {Length} karakter → {ChunkCount} chunk ({Strategy})",
            text.Length, merged.Count, options.Strategy);

        return merged;
    }

    /// <summary>
    /// Paragraf bazlı parçalama (\n\n ile ayırma)
    /// </summary>
    private static List<TextChunk> ChunkByParagraph(string text, ChunkingOptions options)
    {
        var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<TextChunk>();
        var currentChunk = "";
        var currentOffset = 0;
        var chunkStart = 0;

        foreach (var paragraph in paragraphs)
        {
            var trimmed = paragraph.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (currentChunk.Length + trimmed.Length + 2 > options.MaxChunkSize && currentChunk.Length > 0)
            {
                chunks.Add(CreateChunk(currentChunk.Trim(), chunks.Count, chunkStart, currentOffset));
                chunkStart = currentOffset;
                currentChunk = "";
            }

            if (currentChunk.Length > 0)
                currentChunk += "\n\n";
            currentChunk += trimmed;
            currentOffset += trimmed.Length + 2; // +2 for \n\n
        }

        if (!string.IsNullOrWhiteSpace(currentChunk))
        {
            chunks.Add(CreateChunk(currentChunk.Trim(), chunks.Count, chunkStart, currentOffset));
        }

        return chunks;
    }

    /// <summary>
    /// Cümle bazlı parçalama (., !, ? ile ayırma)
    /// </summary>
    private static List<TextChunk> ChunkBySentence(string text, ChunkingOptions options)
    {
        var sentenceEnders = new[] { ". ", "! ", "? ", ".\n", "!\n", "?\n" };
        var chunks = new List<TextChunk>();
        var currentChunk = "";
        var currentOffset = 0;
        var chunkStart = 0;
        var remaining = text;

        while (remaining.Length > 0)
        {
            var minIdx = -1;
            var matchLen = 0;

            foreach (var ender in sentenceEnders)
            {
                var idx = remaining.IndexOf(ender, StringComparison.Ordinal);
                if (idx >= 0 && (minIdx < 0 || idx < minIdx))
                {
                    minIdx = idx;
                    matchLen = ender.Length;
                }
            }

            string sentence;
            if (minIdx >= 0)
            {
                sentence = remaining[..(minIdx + matchLen)];
                remaining = remaining[(minIdx + matchLen)..];
            }
            else
            {
                sentence = remaining;
                remaining = "";
            }

            if (currentChunk.Length + sentence.Length > options.MaxChunkSize && currentChunk.Length > 0)
            {
                chunks.Add(CreateChunk(currentChunk.Trim(), chunks.Count, chunkStart, currentOffset));
                chunkStart = currentOffset;
                currentChunk = "";
            }

            currentChunk += sentence;
            currentOffset += sentence.Length;
        }

        if (!string.IsNullOrWhiteSpace(currentChunk))
        {
            chunks.Add(CreateChunk(currentChunk.Trim(), chunks.Count, chunkStart, currentOffset));
        }

        return chunks;
    }

    /// <summary>
    /// Kayan pencere (sliding window) ile parçalama — overlap destekli
    /// </summary>
    private static List<TextChunk> ChunkByTokenWindow(string text, ChunkingOptions options)
    {
        var chunks = new List<TextChunk>();
        var step = options.MaxChunkSize - options.OverlapSize;
        if (step <= 0) step = options.MaxChunkSize / 2;

        for (int i = 0; i < text.Length; i += step)
        {
            var end = Math.Min(i + options.MaxChunkSize, text.Length);
            var content = text[i..end].Trim();

            if (!string.IsNullOrWhiteSpace(content))
            {
                chunks.Add(CreateChunk(content, chunks.Count, i, end));
            }

            if (end == text.Length) break;
        }

        return chunks;
    }

    /// <summary>
    /// Sabit karakter boyutu ile parçalama (kelime sınırına dikkat eder)
    /// </summary>
    private static List<TextChunk> ChunkByFixedSize(string text, ChunkingOptions options)
    {
        var chunks = new List<TextChunk>();
        var offset = 0;

        while (offset < text.Length)
        {
            var end = Math.Min(offset + options.MaxChunkSize, text.Length);

            // Kelime sınırını bul (tam kelimeyi kesmemek için)
            if (end < text.Length)
            {
                var lastSpace = text.LastIndexOf(' ', end, Math.Min(end - offset, 50));
                if (lastSpace > offset)
                    end = lastSpace;
            }

            var content = text[offset..end].Trim();
            if (!string.IsNullOrWhiteSpace(content))
            {
                chunks.Add(CreateChunk(content, chunks.Count, offset, end));
            }

            offset = end;
            // Boşluk atla
            while (offset < text.Length && text[offset] == ' ') offset++;
        }

        return chunks;
    }

    /// <summary>
    /// Küçük chunk'ları bir sonrakiyle birleştirir
    /// </summary>
    private static List<TextChunk> MergeSmallChunks(List<TextChunk> chunks, int minSize, int maxSize)
    {
        if (chunks.Count <= 1) return chunks;

        var merged = new List<TextChunk>();
        var buffer = chunks[0];

        for (int i = 1; i < chunks.Count; i++)
        {
            if (buffer.Content.Length < minSize && buffer.Content.Length + chunks[i].Content.Length + 2 <= maxSize)
            {
                // Birleştir
                buffer = new TextChunk
                {
                    Content = buffer.Content + "\n\n" + chunks[i].Content,
                    Index = buffer.Index,
                    StartOffset = buffer.StartOffset,
                    EndOffset = chunks[i].EndOffset,
                    EstimatedTokenCount = EstimateTokenCount(buffer.Content + "\n\n" + chunks[i].Content)
                };
            }
            else
            {
                merged.Add(buffer);
                buffer = chunks[i];
            }
        }

        merged.Add(buffer);

        // İndeksleri yeniden numarala
        return merged.Select((c, idx) => c with { Index = idx }).ToList();
    }

    private static TextChunk CreateChunk(string content, int index, int startOffset, int endOffset)
    {
        return new TextChunk
        {
            Content = content,
            Index = index,
            StartOffset = startOffset,
            EndOffset = endOffset,
            EstimatedTokenCount = EstimateTokenCount(content)
        };
    }

    /// <summary>
    /// Tahmini token sayısı — kelime sayısı × 1.3 (GPT tokenizer ortalaması)
    /// </summary>
    private static int EstimateTokenCount(string text)
    {
        var wordCount = text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
        return (int)(wordCount * 1.3);
    }
}
