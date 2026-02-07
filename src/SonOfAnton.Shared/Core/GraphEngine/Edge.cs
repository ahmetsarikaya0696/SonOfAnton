namespace SonOfAnton.Shared.Core.GraphEngine;

/// <summary>
/// Edge lifecycle event tipleri
/// </summary>
public enum EdgeEventType
{
    Connected,
    Disconnected,
    DataTransferred,
    Error
}

/// <summary>
/// Edge event argümanları
/// </summary>
public class EdgeEventArgs : EventArgs
{
    public required Edge Edge { get; init; }
    public EdgeEventType EventType { get; init; }
    public object? Data { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

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
    /// Transfer sayısı (throughput ölçümü için)
    /// </summary>
    public long TransferCount { get; set; }

    /// <summary>
    /// Bağlantı kurulduğunda tetiklenen event
    /// </summary>
    public event EventHandler<EdgeEventArgs>? OnConnected;

    /// <summary>
    /// Bağlantı koparıldığında tetiklenen event
    /// </summary>
    public event EventHandler<EdgeEventArgs>? OnDisconnected;

    /// <summary>
    /// Veri aktarıldığında tetiklenen event
    /// </summary>
    public event EventHandler<EdgeEventArgs>? OnDataTransferred;

    /// <summary>
    /// Bağlantı kurulduğunu bildirir
    /// </summary>
    public void NotifyConnected()
    {
        OnConnected?.Invoke(this, new EdgeEventArgs
        {
            Edge = this,
            EventType = EdgeEventType.Connected
        });
    }

    /// <summary>
    /// Bağlantı koparıldığını bildirir
    /// </summary>
    public void NotifyDisconnected()
    {
        OnDisconnected?.Invoke(this, new EdgeEventArgs
        {
            Edge = this,
            EventType = EdgeEventType.Disconnected
        });
    }

    /// <summary>
    /// Veri aktarımını bildirir
    /// </summary>
    public void NotifyDataTransferred(object? data)
    {
        TransferCount++;
        LastTransferredData = data;
        LastTransferTime = DateTime.UtcNow;
        State = EdgeState.Transferred;

        OnDataTransferred?.Invoke(this, new EdgeEventArgs
        {
            Edge = this,
            EventType = EdgeEventType.DataTransferred,
            Data = data
        });
    }
    
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

        // Max connections kontrolü (source output pin)
        if (sourcePin.Definition.MaxConnections > 0 &&
            sourcePin.ConnectedEdges.Count >= sourcePin.Definition.MaxConnections)
        {
            error = $"Output pin '{SourcePinName}' has reached its maximum connection limit ({sourcePin.Definition.MaxConnections})";
            return false;
        }

        // Max connections kontrolü (target input pin)
        if (targetPin.Definition.MaxConnections > 0 &&
            targetPin.ConnectedEdges.Count >= targetPin.Definition.MaxConnections)
        {
            error = $"Input pin '{TargetPinName}' has reached its maximum connection limit ({targetPin.Definition.MaxConnections})";
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
        
        // Boolean, String'e ve Number'a dönüştürülebilir
        if (source == PinType.Boolean && (target == PinType.String || target == PinType.Number))
            return true;

        // String, Number'a dönüştürülebilir (parse edilebilir)
        if (source == PinType.String && target == PinType.Number)
            return true;

        // Object ve Array birbirine dönüştürülebilir (JSON context'inde)
        if ((source == PinType.Object || source == PinType.Array) && 
            (target == PinType.Object || target == PinType.Any))
            return true;

        // Image, Binary'ye dönüştürülebilir
        if (source == PinType.Image && target == PinType.Binary)
            return true;

        // Binary, Image'e dönüştürülebilir
        if (source == PinType.Binary && target == PinType.Image)
            return true;

        // Vector, Array'e dönüştürülebilir
        if (source == PinType.Vector && target == PinType.Array)
            return true;

        // Document, Object'e dönüştürülebilir
        if (source == PinType.Document && target == PinType.Object)
            return true;

        // Document, String'e dönüştürülebilir
        if (source == PinType.Document && target == PinType.String)
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
