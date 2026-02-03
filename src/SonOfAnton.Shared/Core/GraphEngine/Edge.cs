namespace SonOfAnton.Shared.Core.GraphEngine;

/// <summary>
/// Edge durumu
/// </summary>
public enum EdgeState
{
    /// <summary>
    /// Normal bekleme durumu
    /// </summary>
    Idle,
    
    /// <summary>
    /// Veri akışı aktif
    /// </summary>
    Active,
    
    /// <summary>
    /// Veri başarıyla iletildi
    /// </summary>
    Transferred,
    
    /// <summary>
    /// Hata oluştu
    /// </summary>
    Error
}

/// <summary>
/// İki node arasındaki bağlantıyı temsil eder
/// Source node'un output pin'inden target node'un input pin'ine veri akışı sağlar
/// </summary>
public class Edge
{
    /// <summary>
    /// Edge'in unique ID'si
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// Kaynak node ID'si
    /// </summary>
    public Guid SourceNodeId { get; init; }
    
    /// <summary>
    /// Hedef node ID'si
    /// </summary>
    public Guid TargetNodeId { get; init; }
    
    /// <summary>
    /// Kaynak pin adı
    /// </summary>
    public required string SourcePinName { get; init; }
    
    /// <summary>
    /// Hedef pin adı
    /// </summary>
    public required string TargetPinName { get; init; }
    
    /// <summary>
    /// Edge'in mevcut durumu
    /// </summary>
    public EdgeState State { get; set; } = EdgeState.Idle;
    
    /// <summary>
    /// Son aktarılan veri (debug amaçlı)
    /// </summary>
    public object? LastTransferredData { get; set; }
    
    /// <summary>
    /// Aktarım zamanı
    /// </summary>
    public DateTime? LastTransferTime { get; set; }
    
    /// <summary>
    /// Edge'in bağlantı yapmasının geçerli olup olmadığını kontrol eder
    /// </summary>
    public bool IsValid(INode sourceNode, INode targetNode, out string? error)
    {
        error = null;
        
        // Source pin kontrolü
        if (!sourceNode.OutputPins.TryGetValue(SourcePinName, out var sourcePin))
        {
            error = $"Source node '{sourceNode.Name}' does not have output pin '{SourcePinName}'";
            return false;
        }
        
        // Target pin kontrolü
        if (!targetNode.InputPins.TryGetValue(TargetPinName, out var targetPin))
        {
            error = $"Target node '{targetNode.Name}' does not have input pin '{TargetPinName}'";
            return false;
        }
        
        // Self-loop kontrolü
        if (SourceNodeId == TargetNodeId)
        {
            error = "Self-loops are not allowed";
            return false;
        }
        
        // Tip uyumluluğu kontrolü
        if (!IsTypeCompatible(sourcePin.Definition.DataType, targetPin.Definition.DataType))
        {
            error = $"Type mismatch: {sourcePin.Definition.DataType} -> {targetPin.Definition.DataType}";
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// İki pin tipi arasında uyumluluk kontrolü
    /// </summary>
    private static bool IsTypeCompatible(PinType source, PinType target)
    {
        // Any tipi her şeyle uyumlu
        if (source == PinType.Any || target == PinType.Any)
            return true;
        
        // Aynı tipler uyumlu
        if (source == target)
            return true;
        
        // Number, String'e dönüştürülebilir
        if (source == PinType.Number && target == PinType.String)
            return true;
        
        // Boolean, String'e dönüştürülebilir
        if (source == PinType.Boolean && target == PinType.String)
            return true;
        
        // Object ve Array birbirine dönüştürülebilir (JSON context'inde)
        if ((source == PinType.Object || source == PinType.Array) && 
            (target == PinType.Object || target == PinType.Any))
            return true;
        
        return false;
    }
}

/// <summary>
/// Edge oluşturmak için builder
/// </summary>
public class EdgeBuilder
{
    private Guid _sourceNodeId;
    private Guid _targetNodeId;
    private string _sourcePinName = "output";
    private string _targetPinName = "input";
    
    public EdgeBuilder From(Guid nodeId, string pinName = "output")
    {
        _sourceNodeId = nodeId;
        _sourcePinName = pinName;
        return this;
    }
    
    public EdgeBuilder To(Guid nodeId, string pinName = "input")
    {
        _targetNodeId = nodeId;
        _targetPinName = pinName;
        return this;
    }
    
    public Edge Build()
    {
        if (_sourceNodeId == Guid.Empty)
            throw new InvalidOperationException("Source node ID is required");
        if (_targetNodeId == Guid.Empty)
            throw new InvalidOperationException("Target node ID is required");
        
        return new Edge
        {
            SourceNodeId = _sourceNodeId,
            TargetNodeId = _targetNodeId,
            SourcePinName = _sourcePinName,
            TargetPinName = _targetPinName
        };
    }
}
