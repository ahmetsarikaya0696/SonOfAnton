namespace SonOfAnton.Backend.Infrastructure.VectorDatabase;

/// <summary>
/// HNSW (Hierarchical Navigable Small World) indeks konfigürasyonu
/// </summary>
public class HnswConfig
{
    /// <summary>
    /// Her node'un sahip olabileceği max bağlantı sayısı.
    /// Artırmak daha iyi recall verir ama daha fazla bellek kullanır.
    /// Varsayılan: 16
    /// </summary>
    public int M { get; set; } = 16;

    /// <summary>
    /// İndeks oluşturma sırasında kullanılan arama genişliği.
    /// Daha yüksek değer → daha kaliteli indeks, daha yavaş build.
    /// Varsayılan: 64
    /// </summary>
    public int EfConstruction { get; set; } = 64;

    /// <summary>
    /// Arama sırasında kullanılan arama genişliği.
    /// Daha yüksek değer → daha iyi recall, daha yavaş sorgu.
    /// Varsayılan: 40
    /// </summary>
    public int EfSearch { get; set; } = 40;

    /// <summary>
    /// appsettings.json'dan yükleme
    /// </summary>
    public static HnswConfig FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("VectorDatabase:Hnsw");
        return new HnswConfig
        {
            M = section.GetValue("M", 16),
            EfConstruction = section.GetValue("EfConstruction", 64),
            EfSearch = section.GetValue("EfSearch", 40)
        };
    }
}
