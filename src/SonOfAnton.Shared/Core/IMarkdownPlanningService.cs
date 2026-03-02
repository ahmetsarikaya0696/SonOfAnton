using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Shared.Core;

/// <summary>
/// Ajanların markdown planlama belgelerini (task_plan.md, findings.md, progress.md)
/// yönetmek için kullanılan servis arayüzü.
/// Okuma/yazma işlemleri eşzamanlılık koruması altında gerçekleştirilir.
/// </summary>
public interface IMarkdownPlanningService
{
    /// <summary>
    /// Belirtilen tipte belgeyi okur.
    /// Önce dosya sisteminden, yoksa veritabanından okur.
    /// </summary>
    /// <param name="agentId">Ajan kimliği</param>
    /// <param name="fileType">Belge tipi</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    /// <returns>Belge DTO'su veya null</returns>
    Task<PlanningDocumentDto?> ReadDocumentAsync(
        string agentId,
        PlanFileType fileType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Belgeyi yazar (oluşturur veya günceller).
    /// Dosya sistemi ve veritabanına dual-write yapar.
    /// </summary>
    /// <param name="agentId">Ajan kimliği</param>
    /// <param name="fileType">Belge tipi</param>
    /// <param name="content">Markdown içeriği</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    /// <returns>Güncellenen belge DTO'su</returns>
    Task<PlanningDocumentDto> WriteDocumentAsync(
        string agentId,
        PlanFileType fileType,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Belgenin sonuna içerik ekler.
    /// Mevcut içeriği koruyarak yeni içeriği append eder.
    /// </summary>
    /// <param name="agentId">Ajan kimliği</param>
    /// <param name="fileType">Belge tipi</param>
    /// <param name="appendContent">Eklenecek markdown içeriği</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    /// <returns>Güncellenen belge DTO'su</returns>
    Task<PlanningDocumentDto> AppendToDocumentAsync(
        string agentId,
        PlanFileType fileType,
        string appendContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Belgenin geçmiş versiyonlarını listeler.
    /// </summary>
    /// <param name="agentId">Ajan kimliği</param>
    /// <param name="fileType">Belge tipi</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    /// <returns>Versiyon listesi (en yeniden eskiye)</returns>
    Task<IReadOnlyList<PlanningDocumentDto>> GetDocumentHistoryAsync(
        string agentId,
        PlanFileType fileType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Belirtilen ajanın tüm belgelerini listeler.
    /// </summary>
    /// <param name="agentId">Ajan kimliği</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    /// <returns>Belge listesi</returns>
    Task<IReadOnlyList<PlanningDocumentDto>> GetAllDocumentsAsync(
        string agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Belgeyi siler.
    /// Hem dosya sisteminden hem veritabanından kaldırır.
    /// </summary>
    /// <param name="agentId">Ajan kimliği</param>
    /// <param name="fileType">Belge tipi</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    Task DeleteDocumentAsync(
        string agentId,
        PlanFileType fileType,
        CancellationToken cancellationToken = default);
}
