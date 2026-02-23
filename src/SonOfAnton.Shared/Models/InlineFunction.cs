namespace SonOfAnton.Shared.Models;

/// <summary>
/// Inline formül fonksiyonu metadata modeli
/// Frontend autocomplete ve dokümentasyon için kullanılır
/// </summary>
public class InlineFunction
{
    /// <summary>
    /// Fonksiyon adı (ör: "formatDate", "if", "substring")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Fonksiyon açıklaması
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Kategori (String, Date, Number, Logic, Collection, Convert)
    /// </summary>
    public required string Category { get; set; }

    /// <summary>
    /// Parametre tanımları
    /// </summary>
    public List<FunctionParameter> Parameters { get; set; } = [];

    /// <summary>
    /// Dönüş tipi açıklaması
    /// </summary>
    public string ReturnType { get; set; } = "any";

    /// <summary>
    /// Kullanım örneği
    /// </summary>
    public string Example { get; set; } = string.Empty;
}

/// <summary>
/// Fonksiyon parametre tanımı
/// </summary>
public class FunctionParameter
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; } = true;
}
