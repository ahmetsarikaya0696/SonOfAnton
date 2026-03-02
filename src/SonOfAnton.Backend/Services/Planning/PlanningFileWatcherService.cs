using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Enums;

namespace SonOfAnton.Backend.Services.Planning;

/// <summary>
/// .NET FileSystemWatcher kullanarak planlama belgesi dizinini izleyen BackgroundService.
/// Dosya değişiklikleri algılandığında DB'yi senkronize eder.
/// Debounce mekanizması ile art arda gelen olayları birleştirir.
/// </summary>
public class PlanningFileWatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PlanningFileWatcherService> _logger;
    private FileSystemWatcher? _watcher;

    /// <summary>
    /// Debounce için son değişiklik zamanlarını izler
    /// </summary>
    private readonly Dictionary<string, DateTime> _lastChangeTime = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Debounce süresi — aynı dosya için art arda gelen olaylar bu süre içinde birleştirilir
    /// </summary>
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Bekleyen değişiklikleri işleyecek kanal
    /// </summary>
    private readonly System.Threading.Channels.Channel<FileChangeEvent> _changeChannel =
        System.Threading.Channels.Channel.CreateUnbounded<FileChangeEvent>(
            new System.Threading.Channels.UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

    public PlanningFileWatcherService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<PlanningFileWatcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var watcherEnabled = _configuration.GetValue("Planning:WatcherEnabled", true);
        if (!watcherEnabled)
        {
            _logger.LogInformation("Planlama dosya izleyici devre dışı bırakıldı.");
            return;
        }

        var basePath = _configuration.GetValue<string>("Planning:BasePath")
            ?? Path.Combine(AppContext.BaseDirectory, "planning_docs");

        // Dizin yoksa oluştur
        Directory.CreateDirectory(basePath);

        _logger.LogInformation("Planlama dosya izleyici başlatılıyor: {BasePath}", basePath);

        // Değişiklik işleme döngüsünü başlat
        var processingTask = ProcessChangesAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                StartWatcher(basePath);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FileSystemWatcher hatası, 5 saniye sonra yeniden başlatılacak");
                StopWatcher();
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        StopWatcher();
        _changeChannel.Writer.Complete();
        await processingTask;

        _logger.LogInformation("Planlama dosya izleyici durduruldu.");
    }

    private void StartWatcher(string basePath)
    {
        StopWatcher();

        _watcher = new FileSystemWatcher(basePath, "*.md")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Deleted += OnFileDeleted;
        _watcher.Renamed += OnFileRenamed;
        _watcher.Error += OnWatcherError;

        _logger.LogInformation("FileSystemWatcher başlatıldı: {Path}", basePath);
    }

    private void StopWatcher()
    {
        if (_watcher == null) return;

        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnFileChanged;
        _watcher.Created -= OnFileChanged;
        _watcher.Deleted -= OnFileDeleted;
        _watcher.Renamed -= OnFileRenamed;
        _watcher.Error -= OnWatcherError;
        _watcher.Dispose();
        _watcher = null;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (!ShouldProcess(e.FullPath)) return;

        _changeChannel.Writer.TryWrite(new FileChangeEvent
        {
            FilePath = e.FullPath,
            ChangeType = e.ChangeType,
            Timestamp = DateTime.UtcNow
        });
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (!ShouldProcess(e.FullPath)) return;

        _changeChannel.Writer.TryWrite(new FileChangeEvent
        {
            FilePath = e.FullPath,
            ChangeType = WatcherChangeTypes.Deleted,
            Timestamp = DateTime.UtcNow
        });
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        _logger.LogInformation("Dosya yeniden adlandırıldı: {OldPath} → {NewPath}",
            e.OldFullPath, e.FullPath);

        // Eski dosyayı silindi olarak işle, yeni dosyayı oluşturuldu olarak işle
        if (ShouldProcess(e.OldFullPath))
        {
            _changeChannel.Writer.TryWrite(new FileChangeEvent
            {
                FilePath = e.OldFullPath,
                ChangeType = WatcherChangeTypes.Deleted,
                Timestamp = DateTime.UtcNow
            });
        }

        if (ShouldProcess(e.FullPath))
        {
            _changeChannel.Writer.TryWrite(new FileChangeEvent
            {
                FilePath = e.FullPath,
                ChangeType = WatcherChangeTypes.Created,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "FileSystemWatcher hatası oluştu");
    }

    /// <summary>
    /// Debounce uygulanmış değişiklikleri işler
    /// </summary>
    private async Task ProcessChangesAsync(CancellationToken stoppingToken)
    {
        await foreach (var change in _changeChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Debounce kontrolü
                if (_lastChangeTime.TryGetValue(change.FilePath, out var lastChange) &&
                    (change.Timestamp - lastChange) < DebounceInterval)
                {
                    continue;
                }

                _lastChangeTime[change.FilePath] = change.Timestamp;

                // Debounce gecikmesi — hızlı ardışık değişiklikleri sarma
                await Task.Delay(DebounceInterval, stoppingToken);

                await SyncFileToDbAsync(change, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dosya değişikliği işlenirken hata: {FilePath}", change.FilePath);
            }
        }
    }

    private async Task SyncFileToDbAsync(FileChangeEvent change, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SonOfAntonDbContext>();

        var (agentId, fileType) = ParseFilePath(change.FilePath);
        if (agentId == null || fileType == null)
        {
            _logger.LogDebug("Tanımlanamayan dosya değişikliği atlandı: {FilePath}", change.FilePath);
            return;
        }

        if (change.ChangeType == WatcherChangeTypes.Deleted)
        {
            var entity = await dbContext.PlanningDocuments
                .FirstOrDefaultAsync(d => d.AgentId == agentId && d.FileType == fileType.Value,
                    cancellationToken);

            if (entity != null)
            {
                dbContext.PlanningDocuments.Remove(entity);
                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Silinen dosya DB'den de kaldırıldı: {AgentId}/{FileType}",
                    agentId, fileType);
            }
        }
        else
        {
            // Created veya Changed
            if (!File.Exists(change.FilePath)) return;

            var content = await File.ReadAllTextAsync(change.FilePath, cancellationToken);

            var entity = await dbContext.PlanningDocuments
                .FirstOrDefaultAsync(d => d.AgentId == agentId && d.FileType == fileType.Value,
                    cancellationToken);

            if (entity != null)
            {
                // İçerik değişmemişse güncelleme yapma
                if (entity.Content == content) return;

                entity.Content = content;
                entity.Version++;
                entity.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                entity = new Shared.Entities.PlanningDocumentEntity
                {
                    Id = Guid.NewGuid(),
                    AgentId = agentId,
                    FileType = fileType.Value,
                    Content = content,
                    Version = 1,
                    FilePath = change.FilePath,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                dbContext.PlanningDocuments.Add(entity);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Dosya değişikliği DB ile senkronize edildi: {AgentId}/{FileType} v{Version}",
                agentId, fileType, entity.Version);
        }
    }

    /// <summary>
    /// Dosya yolundan agentId ve fileType çıkarır.
    /// Beklenen yapı: {basePath}/{agentId}/{filename}.md
    /// </summary>
    private (string? agentId, PlanFileType? fileType) ParseFilePath(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var fileType = fileName.ToLowerInvariant() switch
        {
            "task_plan.md" => PlanFileType.TaskPlan,
            "findings.md" => PlanFileType.Findings,
            "progress.md" => PlanFileType.Progress,
            _ => (PlanFileType?)null
        };

        if (fileType == null) return (null, null);

        var directory = Path.GetDirectoryName(filePath);
        var agentId = directory != null ? Path.GetFileName(directory) : null;

        return (agentId, fileType);
    }

    private static bool ShouldProcess(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".md", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FileChangeEvent
    {
        public required string FilePath { get; init; }
        public WatcherChangeTypes ChangeType { get; init; }
        public DateTime Timestamp { get; init; }
    }
}
