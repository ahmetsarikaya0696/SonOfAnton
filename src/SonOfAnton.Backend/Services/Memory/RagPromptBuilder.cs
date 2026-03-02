namespace SonOfAnton.Backend.Services.Memory;

/// <summary>
/// RAG bağlamı — arama sonuçlarından oluşturulan bağlam
/// </summary>
public record RagContext
{
    /// <summary>Elde edilen belge parçaları</summary>
    public IReadOnlyList<RagDocument> RetrievedDocuments { get; init; } = [];

    /// <summary>Sistem yönergeleri (opsiyonel)</summary>
    public string? SystemInstructions { get; init; }

    /// <summary>Maksimum token bütçesi</summary>
    public int MaxTokenBudget { get; init; } = 4000;
}

/// <summary>
/// RAG döküman parçası
/// </summary>
public record RagDocument
{
    /// <summary>İçerik</summary>
    public required string Content { get; init; }

    /// <summary>Benzerlik skoru</summary>
    public double Score { get; init; }

    /// <summary>Kaynak bilgisi (opsiyonel)</summary>
    public string? Source { get; init; }

    /// <summary>Sektör tipi</summary>
    public string? SectorType { get; init; }
}

/// <summary>
/// RAG prompt oluşturma seçenekleri
/// </summary>
public record RagPromptOptions
{
    /// <summary>Aranacak sonuç sayısı</summary>
    public int SearchTopK { get; init; } = 10;

    /// <summary>Minimum benzerlik</summary>
    public double MinSimilarity { get; init; } = 0.3;

    /// <summary>Belirli sektörlerde ara</summary>
    public List<Shared.Enums.MemorySectorType>? Sectors { get; init; }

    /// <summary>Koleksiyon Id</summary>
    public Guid? CollectionId { get; init; }

    /// <summary>Metadata bilgisini prompt'a dahil et</summary>
    public bool IncludeMetadata { get; init; } = true;

    /// <summary>Maksimum bağlam tokeni</summary>
    public int MaxContextTokens { get; init; } = 4000;
}

/// <summary>
/// RAG verilerinin LLM promptlarına dahil edilmesi arayüzü
/// </summary>
public interface IRagPromptBuilder
{
    /// <summary>
    /// Hazır RAG bağlamı ile prompt oluşturur
    /// </summary>
    string BuildPrompt(string userQuery, RagContext context);

    /// <summary>
    /// Otomatik arama yaparak prompt oluşturur (search + build)
    /// </summary>
    Task<string> BuildPromptWithSearchAsync(
        string userQuery,
        RagPromptOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sadece RAG bağlamını oluşturur (search only)
    /// </summary>
    Task<RagContext> BuildContextAsync(
        string query,
        RagPromptOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// RAG Prompt Builder — Semantic Kernel template formatı ile prompt oluşturur.
/// Arama sonuçlarını [CONTEXT] bloğu olarak prompt'a ekler.
/// </summary>
public class RagPromptBuilder : IRagPromptBuilder
{
    private readonly IMemorySearchService _searchService;
    private readonly ILogger<RagPromptBuilder> _logger;
    private readonly IConfiguration _configuration;

    // Varsayılan prompt şablonu
    private const string DefaultTemplate = """
        Aşağıda sana sağlanan bağlam bilgilerini kullanarak soruyu yanıtla.
        Bağlamda bulunmayan bilgileri uydurma. Emin değilsen "Bu bilgi mevcut bağlamda bulunmuyor" de.

        [CONTEXT]
        {0}
        [/CONTEXT]

        Kullanıcı Sorusu: {1}
        """;

    public RagPromptBuilder(
        IMemorySearchService searchService,
        ILogger<RagPromptBuilder> logger,
        IConfiguration configuration)
    {
        _searchService = searchService;
        _logger = logger;
        _configuration = configuration;
    }

    public string BuildPrompt(string userQuery, RagContext context)
    {
        // Bağlam dökümanlarını token bütçesine göre kırp
        var contextText = BuildContextText(context.RetrievedDocuments, context.MaxTokenBudget);

        // Şablonu doldur
        var template = _configuration["RAG:ContextTemplate"];
        if (!string.IsNullOrEmpty(template))
        {
            return template
                .Replace("{{$context}}", contextText)
                .Replace("{{$userQuery}}", userQuery);
        }

        var prompt = string.Format(DefaultTemplate, contextText, userQuery);

        // Sistem yönergelerini ekle
        if (!string.IsNullOrEmpty(context.SystemInstructions))
        {
            prompt = context.SystemInstructions + "\n\n" + prompt;
        }

        return prompt;
    }

    public async Task<string> BuildPromptWithSearchAsync(
        string userQuery,
        RagPromptOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var context = await BuildContextAsync(userQuery, options, cancellationToken);
        return BuildPrompt(userQuery, context);
    }

    public async Task<RagContext> BuildContextAsync(
        string query,
        RagPromptOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new RagPromptOptions();

        var searchOptions = new MemorySearchOptions
        {
            TopK = options.SearchTopK,
            MinSimilarity = options.MinSimilarity,
            CollectionId = options.CollectionId,
            SectorTypes = options.Sectors
        };

        var searchResults = await _searchService.SearchAsync(query, searchOptions, cancellationToken);

        var documents = searchResults.Select(r => new RagDocument
        {
            Content = r.Entry.Content,
            Score = r.Score,
            Source = r.Entry.SourceDocumentId,
            SectorType = r.Entry.SectorType.ToString()
        }).ToList();

        _logger.LogDebug(
            "RAG bağlamı oluşturuldu: {DocumentCount} döküman, TopK={TopK}",
            documents.Count, options.SearchTopK);

        var maxTokens = _configuration.GetValue("RAG:MaxContextTokens", options.MaxContextTokens);

        return new RagContext
        {
            RetrievedDocuments = documents,
            MaxTokenBudget = maxTokens
        };
    }

    /// <summary>
    /// Döküman listesini token bütçesine göre metin haline getirir
    /// </summary>
    private static string BuildContextText(IReadOnlyList<RagDocument> documents, int maxTokenBudget)
    {
        if (documents.Count == 0)
            return "(İlgili bağlam bulunamadı)";

        var parts = new List<string>();
        var currentTokens = 0;

        foreach (var doc in documents)
        {
            var estimatedTokens = doc.Content.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;

            if (currentTokens + estimatedTokens > maxTokenBudget)
            {
                // Kalan bütçeye sığabilecek kadar kırp
                var remainingBudget = maxTokenBudget - currentTokens;
                if (remainingBudget > 50) // En az 50 token kalmışsa ekle
                {
                    var words = doc.Content.Split(' ');
                    var truncated = string.Join(' ', words.Take(remainingBudget)) + "...";
                    parts.Add(FormatDocument(truncated, doc, parts.Count + 1));
                }
                break;
            }

            parts.Add(FormatDocument(doc.Content, doc, parts.Count + 1));
            currentTokens += estimatedTokens;
        }

        return string.Join("\n\n---\n\n", parts);
    }

    private static string FormatDocument(string content, RagDocument doc, int index)
    {
        var header = $"[Kaynak {index}] (Skor: {doc.Score:F2})";
        if (!string.IsNullOrEmpty(doc.Source))
            header += $" | Kaynak: {doc.Source}";
        if (!string.IsNullOrEmpty(doc.SectorType))
            header += $" | Sektör: {doc.SectorType}";

        return $"{header}\n{content}";
    }
}
