using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Shared.Core;

/// <summary>
/// Ralph döngü motorundaki çıkış kurallarını değerlendiren doğrulayıcı arayüzü.
/// Her validator tek bir çıkış koşulunu denetler.
/// Döngü motoru tüm validator'ları zincir halinde çalıştırır.
/// </summary>
public interface ILoopExitValidator
{
    /// <summary>
    /// Doğrulayıcının adı (loglama ve raporlama için)
    /// </summary>
    string ValidatorName { get; }

    /// <summary>
    /// Doğrulayıcının önceliği (düşükten yükseğe sıralanır, küçük değer = yüksek öncelik)
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Çıkış koşulunu değerlendirir.
    /// </summary>
    /// <param name="context">Döngü yürütme bağlamı</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    /// <returns>Doğrulama sonucu</returns>
    Task<LoopExitResult> ValidateAsync(
        LoopExecutionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Döngü yürütme bağlamı — validator'lara aktarılan bilgiler.
/// </summary>
public class LoopExecutionContext
{
    /// <summary>Döngünün benzersiz kimliği</summary>
    public required string LoopId { get; init; }

    /// <summary>Mevcut iterasyon numarası</summary>
    public int CurrentIteration { get; init; }

    /// <summary>Maksimum izin verilen iterasyon</summary>
    public int MaxIterations { get; init; }

    /// <summary>Döngünün başlangıç zamanı</summary>
    public DateTime StartedAt { get; init; }

    /// <summary>Döngünün çalıştığı toplam süre</summary>
    public TimeSpan Elapsed => DateTime.UtcNow - StartedAt;

    /// <summary>Yapılandırılmış zaman aşımı süresi</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(60);

    /// <summary>Döngü bağlam verisi (JSON)</summary>
    public string? ContextJson { get; init; }

    /// <summary>Özel çıkış koşulu ifadesi</summary>
    public string? CustomExitCondition { get; init; }

    /// <summary>İlişkili progress.md içeriği (TaskCompletionValidator için)</summary>
    public string? ProgressContent { get; set; }
}

/// <summary>
/// Çıkış doğrulama sonucu
/// </summary>
public class LoopExitResult
{
    /// <summary>Döngüden çıkılmalı mı?</summary>
    public bool ShouldExit { get; init; }

    /// <summary>Çıkış nedeni</summary>
    public LoopExitReason? Reason { get; init; }

    /// <summary>Detay mesajı</summary>
    public string? Message { get; init; }

    /// <summary>Çıkış gerekmez</summary>
    public static LoopExitResult Continue() => new() { ShouldExit = false };

    /// <summary>Belirtilen nedenle çıkış</summary>
    public static LoopExitResult Exit(LoopExitReason reason, string? message = null) =>
        new() { ShouldExit = true, Reason = reason, Message = message };
}
