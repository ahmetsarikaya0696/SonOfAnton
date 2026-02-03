namespace SonOfAnton.Shared.Core.GraphEngine;

/// <summary>
/// Node durumunu temsil eder
/// </summary>
public enum ExecutionState
{
    Idle,
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Skipped
}

/// <summary>
/// Pin yönünü belirler
/// </summary>
public enum PinDirection
{
    Input,
    Output
}

/// <summary>
/// Pin veri tiplerini tanımlar
/// </summary>
public enum PinType
{
    Any,           // Herhangi bir tip
    Flow,          // Execution flow (trigger)
    String,
    Number,
    Boolean,
    Object,        // JSON object
    Array,         // JSON array
    Binary,        // byte[] (image, file, etc.)
    Vector,        // float[] embedding vector
    Image,         // SKBitmap veya base64
    Document       // Document with metadata
}

/// <summary>
/// Node üzerindeki bir pin (giriş/çıkış portu) tanımı
/// </summary>
public class PinDefinition
{
    /// <summary>
    /// Pin'in unique adı (node içinde benzersiz)
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// UI'da gösterilecek etiket
    /// </summary>
    public required string DisplayName { get; init; }
    
    /// <summary>
    /// Input veya Output
    /// </summary>
    public PinDirection Direction { get; init; }
    
    /// <summary>
    /// Beklenen veri tipi
    /// </summary>
    public PinType DataType { get; init; } = PinType.Any;
    
    /// <summary>
    /// Birden fazla bağlantı kabul eder mi (sadece input için geçerli)
    /// </summary>
    public bool AllowMultiple { get; init; }
    
    /// <summary>
    /// Bu pin'e bağlantı zorunlu mu
    /// </summary>
    public bool Required { get; init; }
    
    /// <summary>
    /// Varsayılan değer (bağlantı yoksa kullanılır)
    /// </summary>
    public object? DefaultValue { get; init; }
    
    /// <summary>
    /// Pin rengi (UI gösterimi için)
    /// </summary>
    public string Color { get; init; } = "#89b4fa";
}

/// <summary>
/// Runtime'da bir pin'in durumu
/// </summary>
public class Pin
{
    public required PinDefinition Definition { get; init; }
    
    /// <summary>
    /// Bu pin'e bağlı olan connection ID'leri
    /// </summary>
    public List<Guid> ConnectedEdges { get; init; } = [];
    
    /// <summary>
    /// Pin'in mevcut değeri (execution sırasında)
    /// </summary>
    public object? Value { get; set; }
    
    /// <summary>
    /// Değer set edildi mi
    /// </summary>
    public bool HasValue { get; set; }
}

/// <summary>
/// Node'un temel interface'i
/// Her node tipi bu interface'i implement etmeli
/// </summary>
public interface INode
{
    /// <summary>
    /// Node instance ID
    /// </summary>
    Guid Id { get; }
    
    /// <summary>
    /// Node tipi (örn: "http_request", "vector_search")
    /// </summary>
    string Type { get; }
    
    /// <summary>
    /// Kullanıcının verdiği isim
    /// </summary>
    string Name { get; set; }
    
    /// <summary>
    /// Canvas üzerindeki X koordinatı
    /// </summary>
    double X { get; set; }
    
    /// <summary>
    /// Canvas üzerindeki Y koordinatı
    /// </summary>
    double Y { get; set; }
    
    /// <summary>
    /// Input pin'leri
    /// </summary>
    IReadOnlyDictionary<string, Pin> InputPins { get; }
    
    /// <summary>
    /// Output pin'leri
    /// </summary>
    IReadOnlyDictionary<string, Pin> OutputPins { get; }
    
    /// <summary>
    /// Node parametreleri (UI'dan düzenlenen)
    /// </summary>
    Dictionary<string, object?> Parameters { get; }
    
    /// <summary>
    /// Mevcut execution durumu
    /// </summary>
    ExecutionState State { get; set; }
    
    /// <summary>
    /// Son hata mesajı
    /// </summary>
    string? LastError { get; set; }
}
