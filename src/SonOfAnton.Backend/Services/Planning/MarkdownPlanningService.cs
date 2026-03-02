using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Core;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Backend.Services.Planning;

/// <summary>
/// Ajanların markdown planlama belgelerini yöneten servis.
/// Dosya sistemi + DB dual-write ile çalışır.
/// ConcurrentFileAccessGuard ile eşzamanlı erişim koruması sağlar.
/// </summary>
public class MarkdownPlanningService : IMarkdownPlanningService
{
    private readonly SonOfAntonDbContext _dbContext;
    private readonly ConcurrentFileAccessGuard _fileGuard;
    private readonly ILogger<MarkdownPlanningService> _logger;
    private readonly string _basePath;

    public MarkdownPlanningService(
        SonOfAntonDbContext dbContext,
        ConcurrentFileAccessGuard fileGuard,
        IConfiguration configuration,
        ILogger<MarkdownPlanningService> logger)
    {
        _dbContext = dbContext;
        _fileGuard = fileGuard;
        _logger = logger;
        _basePath = configuration.GetValue<string>("Planning:BasePath")
            ?? Path.Combine(AppContext.BaseDirectory, "planning_docs");
    }

    /// <inheritdoc />
    public async Task<PlanningDocumentDto?> ReadDocumentAsync(
        string agentId,
        PlanFileType fileType,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(agentId, fileType);

        // Önce dosya sisteminden oku
        using var _ = await _fileGuard.AcquireLockAsync(filePath, cancellationToken: cancellationToken);

        if (File.Exists(filePath))
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            var entity = await GetEntityAsync(agentId, fileType, cancellationToken);

            return new PlanningDocumentDto(
                entity?.Id ?? Guid.Empty,
                agentId,
                fileType,
                content,
                entity?.Version ?? 1,
                filePath,
                entity?.CreatedAt ?? DateTime.UtcNow,
                entity?.UpdatedAt ?? DateTime.UtcNow);
        }

        // Dosya yoksa DB'den dene
        var dbEntity = await GetEntityAsync(agentId, fileType, cancellationToken);
        if (dbEntity != null)
        {
            _logger.LogInformation(
                "Dosya sisteminde bulunamadı, DB'den okundu: {AgentId}/{FileType}",
                agentId, fileType);

            return MapToDto(dbEntity);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<PlanningDocumentDto> WriteDocumentAsync(
        string agentId,
        PlanFileType fileType,
        string content,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(agentId, fileType);

        using var _ = await _fileGuard.AcquireLockAsync(filePath, cancellationToken: cancellationToken);

        // Dosya sistemine yaz
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(filePath, content, cancellationToken);

        _logger.LogInformation("Belge dosya sistemine yazıldı: {FilePath}", filePath);

        // DB'ye yaz (dual-write)
        var entity = await GetEntityAsync(agentId, fileType, cancellationToken);
        if (entity != null)
        {
            entity.Content = content;
            entity.Version++;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.FilePath = filePath;
        }
        else
        {
            entity = new PlanningDocumentEntity
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                FileType = fileType,
                Content = content,
                Version = 1,
                FilePath = filePath,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.PlanningDocuments.Add(entity);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Belge DB'ye yazıldı: {AgentId}/{FileType} v{Version}",
            agentId, fileType, entity.Version);

        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<PlanningDocumentDto> AppendToDocumentAsync(
        string agentId,
        PlanFileType fileType,
        string appendContent,
        CancellationToken cancellationToken = default)
    {
        var existing = await ReadDocumentAsync(agentId, fileType, cancellationToken);
        var newContent = existing != null
            ? existing.Content + Environment.NewLine + appendContent
            : appendContent;

        return await WriteDocumentAsync(agentId, fileType, newContent, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlanningDocumentDto>> GetDocumentHistoryAsync(
        string agentId,
        PlanFileType fileType,
        CancellationToken cancellationToken = default)
    {
        // Mevcut yapıda versiyon geçmişi tek entity üzerinden takip edilir.
        // İleriki aşamada ayrı bir history tablosu eklenebilir.
        var entity = await GetEntityAsync(agentId, fileType, cancellationToken);
        if (entity == null) return [];

        return [MapToDto(entity)];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlanningDocumentDto>> GetAllDocumentsAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.PlanningDocuments
            .Where(d => d.AgentId == agentId)
            .OrderBy(d => d.FileType)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task DeleteDocumentAsync(
        string agentId,
        PlanFileType fileType,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(agentId, fileType);

        using var _ = await _fileGuard.AcquireLockAsync(filePath, cancellationToken: cancellationToken);

        // Dosya sisteminden sil
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("Belge dosya sisteminden silindi: {FilePath}", filePath);
        }

        // DB'den sil
        var entity = await GetEntityAsync(agentId, fileType, cancellationToken);
        if (entity != null)
        {
            _dbContext.PlanningDocuments.Remove(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Belge DB'den silindi: {AgentId}/{FileType}", agentId, fileType);
        }
    }

    // ─── Yardımcı Metotlar ──────────────────────────────────────────

    private string GetFilePath(string agentId, PlanFileType fileType)
    {
        var fileName = fileType switch
        {
            PlanFileType.TaskPlan => "task_plan.md",
            PlanFileType.Findings => "findings.md",
            PlanFileType.Progress => "progress.md",
            _ => throw new ArgumentOutOfRangeException(nameof(fileType))
        };

        return Path.Combine(_basePath, SanitizeAgentId(agentId), fileName);
    }

    private static string SanitizeAgentId(string agentId)
    {
        // Dosya sistemi güvenliği — geçersiz karakterleri kaldır
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(agentId.Select(c => invalid.Contains(c) ? '_' : c));
    }

    private async Task<PlanningDocumentEntity?> GetEntityAsync(
        string agentId,
        PlanFileType fileType,
        CancellationToken cancellationToken)
    {
        return await _dbContext.PlanningDocuments
            .FirstOrDefaultAsync(
                d => d.AgentId == agentId && d.FileType == fileType,
                cancellationToken);
    }

    private static PlanningDocumentDto MapToDto(PlanningDocumentEntity entity)
    {
        return new PlanningDocumentDto(
            entity.Id,
            entity.AgentId,
            entity.FileType,
            entity.Content,
            entity.Version,
            entity.FilePath,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
