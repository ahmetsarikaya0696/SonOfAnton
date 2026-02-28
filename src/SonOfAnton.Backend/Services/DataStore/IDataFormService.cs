using SonOfAnton.Shared.Models;

namespace SonOfAnton.Backend.Services.DataStore;

public interface IDataFormService
{
    // ─── Form Management ──────────────────────────────────────────────
    Task<List<DataFormDto>> GetFormsForTableAsync(Guid tableId, string tenantId);
    Task<DataFormDto?> GetFormAsync(Guid id, string tenantId);
    Task<DataFormDto> CreateFormAsync(DataFormCreateDto dto, string tenantId, Guid? workspaceId);
    Task<DataFormDto> UpdateFormAsync(Guid id, DataFormUpdateDto dto, string tenantId);
    Task<bool> DeleteFormAsync(Guid id, string tenantId);

    // ─── Public API ───────────────────────────────────────────────────
    /// <summary>
    /// Gets a form definition by its public slug (no authentication required)
    /// </summary>
    Task<DataFormDto?> GetFormBySlugAsync(string slug);

    /// <summary>
    /// Submits form data mapping to a Data Table row asynchronously
    /// </summary>
    Task<bool> SubmitFormAsync(string slug, DataFormSubmitDto dto);
}
