using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SonOfAnton.Shared.Core;

namespace SonOfAnton.Backend.Services.AI.Censorship;

/// <summary>
/// RegexOptions.Compiled ve CultureInvariant ile hassas veri tespiti.
/// TC Kimlik, IBAN, Telefon, E-posta, Kredi Kartı formatlarını tanır.
/// </summary>
public class SensitiveDataDetector
{
    private readonly ILogger<SensitiveDataDetector> _logger;

    // ─── Compiled Regex Desenleri ─────────────────────────────────

    private static readonly Regex TcKimlikRegex = new(
        @"\b([1-9]\d{10})\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex IbanRegex = new(
        @"\bTR\d{2}\s?\d{4}\s?\d{4}\s?\d{4}\s?\d{4}\s?\d{4}\s?\d{2}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PhoneRegex = new(
        @"\b(?:\+90|0)\s?(?:\(?\d{3}\)?)\s?\d{3}\s?\d{2}\s?\d{2}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex EmailRegex = new(
        @"\b[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CreditCardRegex = new(
        @"\b(?:\d{4}[\s\-]?){3}\d{4}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Özel (kullanıcı tanımlı veya DB'den gelen) desenler
    /// </summary>
    private readonly List<(Regex Pattern, SensitiveDataType Type)> _customPatterns = new();

    public SensitiveDataDetector(ILogger<SensitiveDataDetector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Çalışma zamanında özel regex deseni ekler (DB kuralları için)
    /// </summary>
    public void AddCustomPattern(string pattern, SensitiveDataType type = SensitiveDataType.CustomPattern)
    {
        try
        {
            var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
            _customPatterns.Add((regex, type));
            _logger.LogDebug("Özel regex deseni eklendi: {Pattern}", pattern);
        }
        catch (RegexParseException ex)
        {
            _logger.LogWarning(ex, "Geçersiz özel regex deseni: {Pattern}", pattern);
        }
    }

    /// <summary>
    /// Verilen metindeki tüm hassas verileri tespit eder
    /// </summary>
    public Task<IReadOnlyList<SensitiveDataMatch>> DetectAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult<IReadOnlyList<SensitiveDataMatch>>(Array.Empty<SensitiveDataMatch>());

        var matches = new List<SensitiveDataMatch>();

        // TC Kimlik No (Luhn-benzeri doğrulama ile)
        foreach (Match match in TcKimlikRegex.Matches(text))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ValidateTcKimlik(match.Value))
            {
                matches.Add(new SensitiveDataMatch(
                    SensitiveDataType.TcKimlik,
                    match.Value,
                    match.Index,
                    match.Index + match.Length));
            }
        }

        // IBAN
        foreach (Match match in IbanRegex.Matches(text))
        {
            cancellationToken.ThrowIfCancellationRequested();
            matches.Add(new SensitiveDataMatch(
                SensitiveDataType.Iban,
                match.Value,
                match.Index,
                match.Index + match.Length));
        }

        // Telefon
        foreach (Match match in PhoneRegex.Matches(text))
        {
            cancellationToken.ThrowIfCancellationRequested();
            matches.Add(new SensitiveDataMatch(
                SensitiveDataType.PhoneNumber,
                match.Value,
                match.Index,
                match.Index + match.Length));
        }

        // E-posta
        foreach (Match match in EmailRegex.Matches(text))
        {
            cancellationToken.ThrowIfCancellationRequested();
            matches.Add(new SensitiveDataMatch(
                SensitiveDataType.Email,
                match.Value,
                match.Index,
                match.Index + match.Length));
        }

        // Kredi Kartı
        foreach (Match match in CreditCardRegex.Matches(text))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var digits = match.Value.Replace(" ", "").Replace("-", "");
            if (ValidateLuhn(digits))
            {
                matches.Add(new SensitiveDataMatch(
                    SensitiveDataType.CreditCard,
                    match.Value,
                    match.Index,
                    match.Index + match.Length));
            }
        }

        // Özel desenler
        foreach (var (pattern, type) in _customPatterns)
        {
            foreach (Match match in pattern.Matches(text))
            {
                cancellationToken.ThrowIfCancellationRequested();
                matches.Add(new SensitiveDataMatch(
                    type,
                    match.Value,
                    match.Index,
                    match.Index + match.Length));
            }
        }

        _logger.LogDebug("Hassas veri taraması tamamlandı. {Count} eşleşme bulundu.", matches.Count);

        return Task.FromResult<IReadOnlyList<SensitiveDataMatch>>(matches);
    }

    /// <summary>
    /// Metindeki hassas verileri maskeler
    /// </summary>
    public Task<string> RedactAsync(
        string text,
        IReadOnlyList<SensitiveDataMatch> matches,
        char maskChar = '*')
    {
        if (matches.Count == 0) return Task.FromResult(text);

        var sb = new StringBuilder(text);

        // Sondan başa doğru maskele (index kaymasını önlemek için)
        foreach (var match in matches.OrderByDescending(m => m.StartIndex))
        {
            var mask = new string(maskChar, match.EndIndex - match.StartIndex);
            sb.Remove(match.StartIndex, match.EndIndex - match.StartIndex);
            sb.Insert(match.StartIndex, mask);
        }

        return Task.FromResult(sb.ToString());
    }

    /// <summary>
    /// Metnin SHA256 hash'ini döndürür (audit loglama için)
    /// </summary>
    public static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexStringLower(bytes);
    }

    // ─── Doğrulama Algoritmaları ──────────────────────────────────

    /// <summary>
    /// TC Kimlik numarası doğrulama algoritması
    /// </summary>
    private static bool ValidateTcKimlik(string value)
    {
        if (value.Length != 11) return false;
        if (value[0] == '0') return false;

        var digits = value.Select(c => c - '0').ToArray();

        // 10. basamak kontrolü: ilk 9 basamağın toplamının mod 10'u
        var sumFirst9 = digits.Take(9).Sum();
        if (digits[9] != sumFirst9 % 10) return false;

        // 11. basamak kontrolü: tüm 10 basamağın toplamının mod 10'u
        var sumFirst10 = digits.Take(10).Sum();
        if (digits[10] != sumFirst10 % 10) return false;

        // Çift ve tek basamak kontrolü
        var oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        var evenSum = digits[1] + digits[3] + digits[5] + digits[7];
        var check10 = ((oddSum * 7) - evenSum) % 10;
        if (check10 < 0) check10 += 10;

        return digits[9] == check10;
    }

    /// <summary>
    /// Luhn algoritması ile kredi kartı numarası doğrulama
    /// </summary>
    private static bool ValidateLuhn(string number)
    {
        if (number.Length < 13 || number.Length > 19) return false;

        var sum = 0;
        var alternate = false;

        for (int i = number.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(number[i])) return false;

            var digit = number[i] - '0';

            if (alternate)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }

            sum += digit;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }
}
