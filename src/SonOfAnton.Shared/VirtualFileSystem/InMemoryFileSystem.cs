using System.Collections.Concurrent;

namespace SonOfAnton.Shared.VirtualFileSystem;

/// <summary>
/// Bellek tabanl� sanal dosya sistemi implementasyonu
/// Thread-safe ve performansl�
/// </summary>
public class InMemoryFileSystem : IVirtualFileSystem
{
    private readonly ConcurrentDictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

    public void WriteFile(string path, string content)
    {
        var normalizedPath = NormalizePath(path);
        _files[normalizedPath] = content;
    }

    public string? ReadFile(string path)
    {
        var normalizedPath = NormalizePath(path);
        return _files.TryGetValue(normalizedPath, out var content) ? content : null;
    }

    public bool FileExists(string path)
    {
        var normalizedPath = NormalizePath(path);
        return _files.ContainsKey(normalizedPath);
    }

    public bool DeleteFile(string path)
    {
        var normalizedPath = NormalizePath(path);
        return _files.TryRemove(normalizedPath, out _);
    }

    public IEnumerable<string> GetAllFiles()
    {
        return _files.Keys.ToList();
    }

    public IEnumerable<string> GetFilesInDirectory(string directory)
    {
        var normalizedDir = NormalizePath(directory);
        if (!normalizedDir.EndsWith('/'))
            normalizedDir += '/';

        return _files.Keys
            .Where(f => f.StartsWith(normalizedDir, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task FlushToDiskAsync(string basePath, CancellationToken cancellationToken = default)
    {
        foreach (var (relativePath, content) in _files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var fullPath = Path.Combine(basePath, relativePath);
            var directory = Path.GetDirectoryName(fullPath);
            
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            await File.WriteAllTextAsync(fullPath, content, cancellationToken);
        }
    }

    public void Clear()
    {
        _files.Clear();
    }

    private static string NormalizePath(string path)
    {
        // Windows ve Unix path'lerini normalize et
        return path.Replace('\\', '/').TrimStart('/');
    }
}

