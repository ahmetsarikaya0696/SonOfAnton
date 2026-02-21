using Microsoft.JSInterop;
using SonOfAnton.Shared.Core.GraphEngine;

namespace SonOfAnton.Frontend.Services;

/// <summary>
/// Fabric.js Canvas ile JS Interop servisi
/// </summary>
public class NodeEditorInteropService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private DotNetObjectReference<NodeEditorInteropService>? _dotNetRef;
    private string? _currentCanvasId;
    
    // Events
    public event Action<string>? OnNodeSelected;
    public event Action<List<string>>? OnSelectionChanged;
    public event Action? OnSelectionCleared;
    public event Action<string, double, double>? OnNodeMoved;
    public event Action<double>? OnCanvasZoom;
    public event Action<string, string, string, string>? OnConnectionCreated;
    public event Action<string>? OnConnectionDeleted;
    public event Action<string, double, double>? OnContextMenu;
    public event Action<string, string>? OnNoteContentChanged;
    
    public NodeEditorInteropService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }
    
    /// <summary>
    /// JS modülünü yükler
    /// </summary>
    private async Task EnsureModuleLoadedAsync()
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/fabric-interop.js");
        _dotNetRef ??= DotNetObjectReference.Create(this);
    }
    
    /// <summary>
    /// Canvas'ı başlatır
    /// </summary>
    /// <param name="canvasId">Canvas element ID</param>
    public async Task<bool> InitializeCanvasAsync(string canvasId)
    {
        await EnsureModuleLoadedAsync();
        _currentCanvasId = canvasId;
        
        return await _module!.InvokeAsync<bool>("initializeCanvas", canvasId, _dotNetRef);
    }
    
    /// <summary>
    /// Canvas'a node ekler
    /// </summary>
    public async Task<bool> AddNodeAsync(
        string nodeId,
        string nodeType,
        string nodeName,
        double x,
        double y,
        NodeConfig config)
    {
        if (_module == null || _currentCanvasId == null)
            return false;
        
        var jsConfig = new
        {
            color = config.Color,
            icon = config.Icon,
            inputs = config.Inputs.Select(p => new { name = p.Name, type = p.Type.ToString().ToLower() }).ToArray(),
            outputs = config.Outputs.Select(p => new { name = p.Name, type = p.Type.ToString().ToLower() }).ToArray()
        };
        
        return await _module.InvokeAsync<bool>("addNode", 
            _currentCanvasId, nodeId, nodeType, nodeName, x, y, jsConfig);
    }
    
    /// <summary>
    /// Node pozisyonunu günceller
    /// </summary>
    public async Task<bool> SetNodePositionAsync(string nodeId, double x, double y)
    {
        if (_module == null || _currentCanvasId == null)
            return false;
        
        return await _module.InvokeAsync<bool>("setNodePosition", _currentCanvasId, nodeId, x, y);
    }
    
    /// <summary>
    /// Node'un çalışma durumunu günceller
    /// </summary>
    public async Task<bool> SetExecutingStateAsync(string nodeId, bool isExecuting)
    {
        if (_module == null || _currentCanvasId == null)
            return false;
        
        return await _module.InvokeAsync<bool>("setExecutingState", _currentCanvasId, nodeId, isExecuting);
    }
    
    /// <summary>
    /// Node'u hata durumuna geçirir
    /// </summary>
    public async Task<bool> SetErrorStateAsync(string nodeId, bool hasError)
    {
        if (_module == null || _currentCanvasId == null)
            return false;
        
        return await _module.InvokeAsync<bool>("setErrorState", _currentCanvasId, nodeId, hasError);
    }
    
    /// <summary>
    /// İki node arasına bağlantı çizer
    /// </summary>
    public async Task<bool> ConnectNodesAsync(
        string connectionId,
        string sourceNodeId,
        string targetNodeId,
        string sourcePort = "output",
        string targetPort = "input")
    {
        if (_module == null || _currentCanvasId == null)
            return false;
        
        return await _module.InvokeAsync<bool>("connectNodes", 
            _currentCanvasId, connectionId, sourceNodeId, targetNodeId, sourcePort, targetPort);
    }
    
    /// <summary>
    /// Bağlantıyı kaldırır
    /// </summary>
    public async Task<bool> RemoveConnectionAsync(string connectionId)
    {
        if (_module == null || _currentCanvasId == null)
            return false;
        
        return await _module.InvokeAsync<bool>("removeConnection", _currentCanvasId, connectionId);
    }
    
    /// <summary>
    /// Node'u kaldırır
    /// </summary>
    public async Task<bool> RemoveNodeAsync(string nodeId)
    {
        if (_module == null || _currentCanvasId == null)
            return false;
        
        return await _module.InvokeAsync<bool>("removeNode", _currentCanvasId, nodeId);
    }
    
    /// <summary>
    /// Canvas'ı temizler
    /// </summary>
    public async Task<bool> ClearCanvasAsync()
    {
        if (_module == null || _currentCanvasId == null)
            return false;
        
        return await _module.InvokeAsync<bool>("clearCanvas", _currentCanvasId);
    }
    
    public async Task DisposeCanvasAsync(string canvasId)
    {
        if (_module == null) return;
        await _module.InvokeVoidAsync("dispose", canvasId);
    }
    
    /// <summary>
    /// Zoom seviyesini ayarlar
    /// </summary>
    public async Task<bool> SetZoomAsync(double zoom)
    {
        if (_module == null || _currentCanvasId == null)
            return false;
        
        return await _module.InvokeAsync<bool>("setZoom", _currentCanvasId, zoom);
    }
    
    /// <summary>
    /// Tüm node'ları ekrana sığdırır
    /// </summary>
    public async Task<bool> FitToScreenAsync()
    {
        if (_module == null || _currentCanvasId == null)
            return false;
        
        return await _module.InvokeAsync<bool>("fitToScreen", _currentCanvasId);
    }

    /// <summary>
    /// Canvas'a sticky note ekler
    /// </summary>
    public async Task<bool> AddNoteAsync(string noteId, string content, double x, double y, string color)
    {
        if (_module == null || _currentCanvasId == null) return false;
        return await _module.InvokeAsync<bool>("addNote", _currentCanvasId, noteId, content, x, y, color);
    }

    /// <summary>
    /// Canvas'a grup ekler
    /// </summary>
    public async Task<bool> AddGroupAsync(string groupId, string name, double x, double y, double width, double height, string color)
    {
        if (_module == null || _currentCanvasId == null) return false;
        return await _module.InvokeAsync<bool>("addGroup", _currentCanvasId, groupId, name, x, y, width, height, color);
    }

    /// <summary>
    /// Node ismini günceller
    /// </summary>
    public async Task<bool> UpdateNodeNameAsync(string nodeId, string newName)
    {
        if (_module == null || _currentCanvasId == null) return false;
        return await _module.InvokeAsync<bool>("updateNodeName", _currentCanvasId, nodeId, newName);
    }

    /// <summary>
    /// Node bypass durumunu günceller
    /// </summary>
    public async Task<bool> SetNodeBypassAsync(string nodeId, bool isBypassed)
    {
        if (_module == null || _currentCanvasId == null) return false;
        return await _module.InvokeAsync<bool>("setNodeBypass", _currentCanvasId, nodeId, isBypassed);
    }
    
    /// <summary>
    /// Node üzerinde inline çıktı gösterir
    /// </summary>
    public async Task<bool> ShowNodeInlineOutputAsync(string nodeId, string outputText)
    {
        if (_module == null || _currentCanvasId == null) return false;
        return await _module.InvokeAsync<bool>("showNodeInlineOutput", _currentCanvasId, nodeId, outputText);
    }

    /// <summary>
    /// Node üzerindeki inline çıktıyı temizler
    /// </summary>
    public async Task<bool> ClearNodeInlineOutputAsync(string nodeId)
    {
        if (_module == null || _currentCanvasId == null) return false;
        return await _module.InvokeAsync<bool>("clearNodeInlineOutput", _currentCanvasId, nodeId);
    }
    
    /// <summary>
    /// Node hareket ettiğinde connection'ları günceller
    /// </summary>
    public async Task<bool> UpdateConnectionsAsync(string nodeId)
    {
        if (_module == null || _currentCanvasId == null)
            return false;
        
        return await _module.InvokeAsync<bool>("updateConnections", _currentCanvasId, nodeId);
    }
    
    #region JS Callbacks
    
    [JSInvokable]
    public void OnNodeSelected_Internal(string nodeId)
    {
        OnNodeSelected?.Invoke(nodeId);
    }
    
    [JSInvokable]
    public void OnSelectionChanged_Internal(List<string> nodeIds)
    {
        OnSelectionChanged?.Invoke(nodeIds);
    }
    
    [JSInvokable]
    public void OnSelectionCleared_Internal()
    {
        OnSelectionCleared?.Invoke();
    }
    
    [JSInvokable]
    public void OnNodeMoved_Internal(string nodeId, double x, double y)
    {
        OnNodeMoved?.Invoke(nodeId, x, y);
        _ = UpdateConnectionsAsync(nodeId);
    }
    
    [JSInvokable]
    public void OnCanvasZoom_Internal(double zoom)
    {
        OnCanvasZoom?.Invoke(zoom);
    }
    
    [JSInvokable]
    public void OnConnectionCreated_Internal(string fromNode, string fromPort, string toNode, string toPort)
    {
        OnConnectionCreated?.Invoke(fromNode, fromPort, toNode, toPort);
    }

    [JSInvokable]
    public void OnConnectionDeleted_Internal(string connectionId)
    {
        OnConnectionDeleted?.Invoke(connectionId);
    }

    [JSInvokable]
    public void OnContextMenu_Internal(string nodeId, double x, double y)
    {
        OnContextMenu?.Invoke(nodeId, x, y);
    }
    
    [JSInvokable]
    public void OnNoteContentChanged_Internal(string noteId, string newContent)
    {
        OnNoteContentChanged?.Invoke(noteId, newContent);
    }
    
    #endregion

    #region Batch Operations

    /// <summary>
    /// Birden fazla node'u tek JS çağrısıyla ekler (performans)
    /// </summary>
    public async Task<bool> AddNodesBatchAsync(IEnumerable<BatchNodeData> nodes)
    {
        if (_module == null || _currentCanvasId == null)
            return false;

        var nodesArray = nodes.Select(n => new
        {
            nodeId = n.NodeId,
            nodeType = n.NodeType,
            nodeName = n.NodeName,
            x = n.X,
            y = n.Y,
            config = new
            {
                color = n.Config.Color,
                icon = n.Config.Icon,
                inputs = n.Config.Inputs.Select(p => new { name = p.Name, type = p.Type.ToString().ToLower() }).ToArray(),
                outputs = n.Config.Outputs.Select(p => new { name = p.Name, type = p.Type.ToString().ToLower() }).ToArray()
            }
        }).ToArray();

        return await _module.InvokeAsync<bool>("addNodesBatch", _currentCanvasId, nodesArray);
    }

    /// <summary>
    /// Birden fazla connection'ı tek JS çağrısıyla ekler (performans)
    /// </summary>
    public async Task<bool> ConnectNodesBatchAsync(IEnumerable<BatchConnectionData> connections)
    {
        if (_module == null || _currentCanvasId == null)
            return false;

        var connsArray = connections.Select(c => new
        {
            connectionId = c.ConnectionId,
            sourceNodeId = c.SourceNodeId,
            targetNodeId = c.TargetNodeId,
            sourcePort = c.SourcePort,
            targetPort = c.TargetPort
        }).ToArray();

        return await _module.InvokeAsync<bool>("connectNodesBatch", _currentCanvasId, connsArray);
    }

    #endregion

    #region Viewport State

    /// <summary>
    /// Mevcut viewport durumunu döndürür
    /// </summary>
    public async Task<ViewportState?> GetViewportStateAsync()
    {
        if (_module == null || _currentCanvasId == null)
            return null;

        return await _module.InvokeAsync<ViewportState>("getViewportState", _currentCanvasId);
    }

    /// <summary>
    /// Viewport durumunu restore eder
    /// </summary>
    public async Task<bool> SetViewportStateAsync(ViewportState state)
    {
        if (_module == null || _currentCanvasId == null)
            return false;

        return await _module.InvokeAsync<bool>("setViewportState", _currentCanvasId, state);
    }

    /// <summary>
    /// Grid snapping'i etkinleştirir/devre dışı bırakır
    /// </summary>
    public async Task<bool> SetGridSnappingAsync(bool enabled, int gridSize = 20)
    {
        if (_module == null || _currentCanvasId == null)
            return false;

        return await _module.InvokeAsync<bool>("setGridSnapping", _currentCanvasId, enabled, gridSize);
    }

    #endregion
    
    public async ValueTask DisposeAsync()
    {
        if (_module != null && _currentCanvasId != null)
        {
            try
            {
                await _module.InvokeVoidAsync("dispose", _currentCanvasId);
            }
            catch
            {
                // Ignore disposal errors
            }
        }
        
        _dotNetRef?.Dispose();
        
        if (_module != null)
        {
            await _module.DisposeAsync();
        }
    }
}

/// <summary>
/// Node konfigürasyonu
/// </summary>
public record NodeConfig
{
    public string Color { get; init; } = "#89b4fa";
    public string? Icon { get; init; }
    public List<PinInfo> Inputs { get; init; } = [];
    public List<PinInfo> Outputs { get; init; } = [];
}

/// <summary>
/// Pin bilgisi
/// </summary>
public record PinInfo
{
    public required string Name { get; init; }
    public PinType Type { get; init; } = PinType.Any;
}

/// <summary>
/// Batch node ekleme verisi
/// </summary>
public record BatchNodeData
{
    public required string NodeId { get; init; }
    public required string NodeType { get; init; }
    public required string NodeName { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public required NodeConfig Config { get; init; }
}

/// <summary>
/// Batch connection ekleme verisi
/// </summary>
public record BatchConnectionData
{
    public required string ConnectionId { get; init; }
    public required string SourceNodeId { get; init; }
    public required string TargetNodeId { get; init; }
    public string SourcePort { get; init; } = "output";
    public string TargetPort { get; init; } = "input";
}

/// <summary>
/// Viewport durumu (zoom/pan state persistence)
/// </summary>
public record ViewportState
{
    public double Zoom { get; init; } = 1.0;
    public double PanX { get; init; }
    public double PanY { get; init; }
}
