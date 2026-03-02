namespace SonOfAnton.Shared.Enums;

/// <summary>
/// OpenMemory 5 sektörlü bellek tipi
/// </summary>
public enum MemorySectorType
{
    /// <summary>Geçmiş olaylar ve deneyimler (ne oldu?)</summary>
    Episodic,

    /// <summary>Genel bilgi ve kavramlar (ne biliniyor?)</summary>
    Semantic,

    /// <summary>Prosedürel bilgi — nasıl yapılır? (adımlar, süreçler)</summary>
    Procedural,

    /// <summary>Bağlamsal bilgi — mevcut oturum/görev bağlamı</summary>
    Contextual,

    /// <summary>Stratejik bilgi — hedefler, planlar, kararlar</summary>
    Strategic
}

/// <summary>
/// Metin parçalama stratejisi
/// </summary>
public enum ChunkingStrategy
{
    /// <summary>Paragraf bazlı (\n\n ile ayırma)</summary>
    Paragraph,

    /// <summary>Cümle bazlı (., !, ? ile ayırma)</summary>
    SentenceBased,

    /// <summary>Kayan pencere (sliding window + overlap)</summary>
    TokenWindow,

    /// <summary>Sabit karakter sayısı ile parçalama</summary>
    FixedSize
}
