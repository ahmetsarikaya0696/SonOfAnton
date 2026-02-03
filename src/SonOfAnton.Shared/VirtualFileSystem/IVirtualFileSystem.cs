namespace SonOfAnton.Shared.VirtualFileSystem;

/// <summary>
/// Sanal dosya sistemi aray�z�
/// Diske yazmadan �nce bellekte �al��may� sa�lar
/// </summary>
public interface IVirtualFileSystem
{
    /// <summary>Dosya olu�turur veya g�nceller</summary>
    void WriteFile(string path, string content);
    
    /// <summary>Dosya i�eri�ini okur</summary>
    string? ReadFile(string path);
    
    /// <summary>Dosya var m� kontrol eder</summary>
    bool FileExists(string path);
    
    /// <summary>Dosyay� siler</summary>
    bool DeleteFile(string path);
    
    /// <summary>T�m dosya yollar�n� d�nd�r�r</summary>
    IEnumerable<string> GetAllFiles();
    
    /// <summary>Belirli bir klas�rdeki dosyalar� d�nd�r�r</summary>
    IEnumerable<string> GetFilesInDirectory(string directory);
    
    /// <summary>VFS i�eri�ini ger�ek diske yazar</summary>
    Task FlushToDiskAsync(string basePath, CancellationToken cancellationToken = default);
    
    /// <summary>VFS'yi temizler</summary>
    void Clear();
}

