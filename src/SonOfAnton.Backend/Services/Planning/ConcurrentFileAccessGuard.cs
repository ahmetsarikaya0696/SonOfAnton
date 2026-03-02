using System.Collections.Concurrent;

namespace SonOfAnton.Backend.Services.Planning;

/// <summary>
/// Eşzamanlı dosya erişimini kontrol eden guard.
/// Her dosya yolu için ayrı SemaphoreSlim kullanarak process-level locking sağlar.
/// IDisposable pattern ile using blokları içinde kullanılarak güvenli kilitleme garanti edilir.
/// </summary>
public sealed class ConcurrentFileAccessGuard : IDisposable
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<ConcurrentFileAccessGuard> _logger;
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    /// <summary>
    /// Son kullanım zamanlarını izler (idle lock cleanup için)
    /// </summary>
    private readonly ConcurrentDictionary<string, DateTime> _lastAccess = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Kullanılmayan lock'ların temizleneceği süre
    /// </summary>
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Bir lock'un idle kabul edileceği süre
    /// </summary>
    private static readonly TimeSpan IdleThreshold = TimeSpan.FromMinutes(30);

    public ConcurrentFileAccessGuard(ILogger<ConcurrentFileAccessGuard> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(CleanupIdleLocks, null, CleanupInterval, CleanupInterval);
    }

    /// <summary>
    /// Belirtilen dosya yolu için kilit alır.
    /// using pattern ile kullanılmalıdır: using var guard = await AcquireLockAsync("path");
    /// </summary>
    /// <param name="filePath">Kilitlenecek dosya yolu</param>
    /// <param name="timeout">Kilit bekleme süresi</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    /// <returns>Dispose edildiğinde kilidi serbest bırakan IDisposable</returns>
    public async Task<IDisposable> AcquireLockAsync(
        string filePath,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var normalizedPath = NormalizePath(filePath);
        var semaphore = _locks.GetOrAdd(normalizedPath, _ => new SemaphoreSlim(1, 1));
        _lastAccess[normalizedPath] = DateTime.UtcNow;

        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);

        _logger.LogDebug("Dosya kilidi bekleniyor: {FilePath}", normalizedPath);

        var acquired = await semaphore.WaitAsync(effectiveTimeout, cancellationToken);
        if (!acquired)
        {
            _logger.LogWarning("Dosya kilidi zaman aşımına uğradı: {FilePath} ({Timeout}ms)",
                normalizedPath, effectiveTimeout.TotalMilliseconds);
            throw new TimeoutException(
                $"'{normalizedPath}' dosyası için kilit {effectiveTimeout.TotalSeconds}s içinde alınamadı.");
        }

        _logger.LogDebug("Dosya kilidi alındı: {FilePath}", normalizedPath);
        return new LockReleaser(semaphore, normalizedPath, _logger);
    }

    /// <summary>
    /// Aktif kilit sayısını döndürür (monitoring için)
    /// </summary>
    public int ActiveLockCount => _locks.Count;

    /// <summary>
    /// Belirli bir dosyanın kilitli olup olmadığını kontrol eder (bloklamaz)
    /// </summary>
    public bool IsLocked(string filePath)
    {
        var normalizedPath = NormalizePath(filePath);
        if (_locks.TryGetValue(normalizedPath, out var semaphore))
        {
            return semaphore.CurrentCount == 0;
        }
        return false;
    }

    private static string NormalizePath(string filePath)
    {
        return Path.GetFullPath(filePath).Replace('\\', '/').ToLowerInvariant();
    }

    private void CleanupIdleLocks(object? state)
    {
        var cutoff = DateTime.UtcNow - IdleThreshold;
        var keysToRemove = _lastAccess
            .Where(kvp => kvp.Value < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            if (_locks.TryGetValue(key, out var semaphore) && semaphore.CurrentCount == 1)
            {
                if (_locks.TryRemove(key, out var removed))
                {
                    _lastAccess.TryRemove(key, out _);
                    removed.Dispose();
                    _logger.LogDebug("Idle dosya kilidi temizlendi: {FilePath}", key);
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cleanupTimer.Dispose();

        foreach (var kvp in _locks)
        {
            kvp.Value.Dispose();
        }
        _locks.Clear();
        _lastAccess.Clear();
    }

    /// <summary>
    /// Kilit serbest bırakıcı — using pattern ile otomatik release sağlar
    /// </summary>
    private sealed class LockReleaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly string _filePath;
        private readonly ILogger _logger;
        private bool _released;

        public LockReleaser(SemaphoreSlim semaphore, string filePath, ILogger logger)
        {
            _semaphore = semaphore;
            _filePath = filePath;
            _logger = logger;
        }

        public void Dispose()
        {
            if (_released) return;
            _released = true;
            _semaphore.Release();
            _logger.LogDebug("Dosya kilidi serbest bırakıldı: {FilePath}", _filePath);
        }
    }
}
