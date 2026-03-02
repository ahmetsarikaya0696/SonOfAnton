namespace SonOfAnton.Shared.Core;

/// <summary>
/// Tespit edilen hassas veri türü
/// </summary>
public enum SensitiveDataType
{
    TcKimlik,
    Iban,
    PhoneNumber,
    Email,
    CreditCard,
    CustomPattern
}

/// <summary>
/// Regex/pattern tabanlı tespit sonucu
/// </summary>
public record SensitiveDataMatch(
    SensitiveDataType Type,
    string Value,
    int StartIndex,
    int EndIndex);

/// <summary>
/// NER modeli tarafından tespit edilen varlık etiketi
/// </summary>
public enum NerLabel
{
    Person,
    Organization,
    Location
}

/// <summary>
/// NER modeli tespit sonucu
/// </summary>
public record NerEntity(
    NerLabel Label,
    string Text,
    double Confidence,
    int StartIndex,
    int EndIndex);

/// <summary>
/// Sansür sonucunda uygulanan eylem
/// </summary>
public enum CensorshipAction
{
    Blocked,
    Redacted,
    Allowed
}

/// <summary>
/// Sansür kuralı türü
/// </summary>
public enum CensorshipRuleType
{
    Regex,
    Keyword,
    NerLabel
}

/// <summary>
/// Sansür analiz seçenekleri
/// </summary>
public record CensorshipOptions
{
    /// <summary>
    /// Regex tabanlı tespit etkin mi? Varsayılan: true
    /// </summary>
    public bool EnableRegex { get; init; } = true;

    /// <summary>
    /// NER modeli tabanlı tespit etkin mi? Varsayılan: true
    /// </summary>
    public bool EnableNer { get; init; } = true;

    /// <summary>
    /// Sadece belirli kuralları uygula. null = tüm aktif kurallar
    /// </summary>
    public IReadOnlyList<Guid>? RuleIds { get; init; }

    /// <summary>
    /// Maskeleme karakteri. Varsayılan: *
    /// </summary>
    public char MaskCharacter { get; init; } = '*';
}

/// <summary>
/// Sansür analiz sonucu
/// </summary>
public record CensorshipResult
{
    /// <summary>
    /// Regex ile tespit edilen hassas veriler
    /// </summary>
    public required IReadOnlyList<SensitiveDataMatch> SensitiveDataMatches { get; init; }

    /// <summary>
    /// NER ile tespit edilen varlıklar
    /// </summary>
    public required IReadOnlyList<NerEntity> NerEntities { get; init; }

    /// <summary>
    /// Herhangi bir ihlal tespit edildi mi?
    /// </summary>
    public bool HasViolation => SensitiveDataMatches.Count > 0 || NerEntities.Count > 0;

    /// <summary>
    /// Maskelenmiş metin
    /// </summary>
    public required string RedactedText { get; init; }

    /// <summary>
    /// Orijinal metin (hash'lenmiş)
    /// </summary>
    public required string OriginalTextHash { get; init; }
}

/// <summary>
/// Sansür kuralları motorunun orkestrasyon arayüzü.
/// Regex + NER tabanlı hassas veri tespiti ve maskeleme.
/// </summary>
public interface ICensorshipEngine
{
    /// <summary>
    /// Metni analiz eder ve hassas verileri tespit eder
    /// </summary>
    Task<CensorshipResult> AnalyzeAsync(
        string text,
        CensorshipOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Metindeki hassas verileri maskeleyerek döndürür
    /// </summary>
    Task<string> RedactAsync(
        string text,
        CensorshipOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sansür motorunun hazır olup olmadığını kontrol eder
    /// </summary>
    Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);
}
