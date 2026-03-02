using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Core;

namespace SonOfAnton.Backend.Services.AI.Censorship;

/// <summary>
/// Sansür kuralları motoru orkestrasyon sınıfı.
/// Regex tabanlı SensitiveDataDetector ve ONNX tabanlı NerEntityDetector'ı birleştirir.
/// DB'den gelen özel kuralları da uygular.
/// </summary>
public class CensorshipEngine : ICensorshipEngine
{
    private readonly SensitiveDataDetector _regexDetector;
    private readonly NerEntityDetector _nerDetector;
    private readonly SonOfAntonDbContext _dbContext;
    private readonly ILogger<CensorshipEngine> _logger;

    public CensorshipEngine(
        SensitiveDataDetector regexDetector,
        NerEntityDetector nerDetector,
        SonOfAntonDbContext dbContext,
        ILogger<CensorshipEngine> logger)
    {
        _regexDetector = regexDetector;
        _nerDetector = nerDetector;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CensorshipResult> AnalyzeAsync(
        string text,
        CensorshipOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CensorshipOptions();
        var textHash = SensitiveDataDetector.ComputeHash(text);

        // DB'den aktif kuralları yükle ve regex detector'a ekle
        await LoadCustomRulesAsync(options, cancellationToken);

        // Paralel çalıştır: Regex + NER
        var regexTask = options.EnableRegex
            ? _regexDetector.DetectAsync(text, cancellationToken)
            : Task.FromResult<IReadOnlyList<SensitiveDataMatch>>(Array.Empty<SensitiveDataMatch>());

        var nerTask = options.EnableNer && _nerDetector.IsReady
            ? _nerDetector.DetectEntitiesAsync(text, cancellationToken)
            : Task.FromResult<IReadOnlyList<NerEntity>>(Array.Empty<NerEntity>());

        await Task.WhenAll(regexTask, nerTask);

        var regexMatches = regexTask.Result;
        var nerEntities = nerTask.Result;

        // Maskelenmiş metin oluştur
        var redactedText = text;
        if (regexMatches.Count > 0)
        {
            redactedText = await _regexDetector.RedactAsync(redactedText, regexMatches, options.MaskCharacter);
        }

        if (nerEntities.Count > 0)
        {
            redactedText = RedactNerEntities(redactedText, nerEntities, options.MaskCharacter);
        }

        var result = new CensorshipResult
        {
            SensitiveDataMatches = regexMatches,
            NerEntities = nerEntities,
            RedactedText = redactedText,
            OriginalTextHash = textHash
        };

        _logger.LogInformation(
            "Sansür analizi tamamlandı. Regex: {RegexCount}, NER: {NerCount}, İhlal: {HasViolation}",
            regexMatches.Count, nerEntities.Count, result.HasViolation);

        return result;
    }

    /// <inheritdoc />
    public async Task<string> RedactAsync(
        string text,
        CensorshipOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = await AnalyzeAsync(text, options, cancellationToken);
        return result.RedactedText;
    }

    /// <inheritdoc />
    public Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        // Regex her zaman hazır, NER opsiyonel
        return Task.FromResult(true);
    }

    // ─── Private Helpers ──────────────────────────────────────────

    /// <summary>
    /// DB'deki aktif sansür kurallarını yükler ve regex detector'a ekler
    /// </summary>
    private async Task LoadCustomRulesAsync(CensorshipOptions options, CancellationToken ct)
    {
        try
        {
            var query = _dbContext.CensorshipRules
                .Where(r => r.IsActive);

            if (options.RuleIds is { Count: > 0 })
            {
                query = query.Where(r => options.RuleIds.Contains(r.Id));
            }

            var rules = await query.ToListAsync(ct);

            foreach (var rule in rules.Where(r => r.RuleType == CensorshipRuleType.Regex))
            {
                _regexDetector.AddCustomPattern(rule.Pattern, SensitiveDataType.CustomPattern);
            }

            _logger.LogDebug("DB'den {Count} sansür kuralı yüklendi.", rules.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sansür kuralları DB'den yüklenemedi. Sadece varsayılan kurallar uygulanacak.");
        }
    }

    /// <summary>
    /// NER ile tespit edilen varlıkları maskeler
    /// </summary>
    private static string RedactNerEntities(string text, IReadOnlyList<NerEntity> entities, char maskChar)
    {
        if (entities.Count == 0) return text;

        var chars = text.ToCharArray();

        // Index çakışmalarını önlemek için sondan başa
        foreach (var entity in entities.OrderByDescending(e => e.StartIndex))
        {
            var startIdx = Math.Max(0, entity.StartIndex);
            var endIdx = Math.Min(chars.Length, entity.EndIndex);

            for (int i = startIdx; i < endIdx; i++)
            {
                if (!char.IsWhiteSpace(chars[i]))
                {
                    chars[i] = maskChar;
                }
            }
        }

        return new string(chars);
    }
}
