using Blazor.Diagrams;
using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Anchors;
using Blazor.Diagrams.Core.PathGenerators;
using Blazor.Diagrams.Core.Routers;
using Blazor.Diagrams.Options;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Frontend.Services;

/// <summary>
/// Blazor.Diagrams durum yönetimi
/// </summary>
public class DiagramStateService
{
    private BlazorDiagram? _diagram;
    private WorkflowDto? _currentWorkflow;
    private WorkflowNodeModel? _selectedNode;

    private Stack<string> _undoStack = new();
    private Stack<string> _redoStack = new();
    private bool _isTrackingState = true;
    private bool _isLoadingGraph;
    private readonly Dictionary<string, Guid> _connectionIds = new();
    private readonly Dictionary<string, (string Source, string Target)> _connectionPorts = new();

    public event Action? OnStateChanged;
    public event Action<WorkflowNodeModel?>? OnNodeSelected;

    public BlazorDiagram Diagram => _diagram ??= CreateDiagram();
    public WorkflowDto? CurrentWorkflow => _currentWorkflow;
    public WorkflowNodeModel? SelectedNode => _selectedNode;

    private BlazorDiagram CreateDiagram()
    {
        var options = new BlazorDiagramOptions
        {
            AllowMultiSelection = true,
            Zoom =
            {
                Enabled = true,
                Minimum = 0.25,
                Maximum = 4
            },
            Links =
            {
                DefaultRouter = new NormalRouter(),
                DefaultPathGenerator = new SmoothPathGenerator(),
                EnableSnapping = true,
                SnappingRadius = 50,
                RequireTarget = true
            }
        };

        var diagram = new BlazorDiagram(options);

        // Custom node modelini kaydet
        diagram.RegisterComponent<WorkflowNodeModel, Components.WorkflowNodeWidget>();
        diagram.SelectionChanged += _ =>
            SetSelectedNode(diagram.GetSelectedModels().OfType<WorkflowNodeModel>().LastOrDefault());
        // Commit a drag/link gesture once, rather than one undo entry per pointer move.
        diagram.PointerUp += (_, _) => { if (!_isLoadingGraph) NotifyStateChanged(); };
        diagram.Nodes.Removed += _ => { if (!_isLoadingGraph) NotifyStateChanged(); };
        diagram.Links.Removed += _ => { if (!_isLoadingGraph) NotifyStateChanged(); };

        return diagram;
    }


    public void CreateNewWorkflow() => LoadWorkflow(new WorkflowDto
    {
        Name = "Yeni İş Akışı",
        Graph = new WorkflowGraphDto()
    });

    public void LoadWorkflow(WorkflowDto workflow, bool resetHistory = true)
    {
        _isLoadingGraph = true;
        try
        {
            _currentWorkflow = workflow;
            workflow.Graph ??= new WorkflowGraphDto();
            _connectionIds.Clear();
            _connectionPorts.Clear();
            Diagram.Batch(() =>
            {
                Diagram.Links.Clear();
                Diagram.Nodes.Clear();
                var nodeMap = new Dictionary<Guid, WorkflowNodeModel>();
                foreach (var dto in workflow.Graph.Nodes)
                {
                    var node = new WorkflowNodeModel(dto, new Point(dto.X, dto.Y));
                    Diagram.Nodes.Add(node);
                    nodeMap[dto.Id] = node;
                }
                foreach (var connection in workflow.Graph.Connections)
                {
                    if (!nodeMap.TryGetValue(connection.SourceNodeId, out var source) ||
                        !nodeMap.TryGetValue(connection.TargetNodeId, out var target)) continue;
                    var sourcePort = source.GetPort(PortAlignment.Right);
                    var targetPort = target.GetPort(PortAlignment.Left);
                    if (sourcePort is null || targetPort is null) continue;
                    var link = new LinkModel(sourcePort, targetPort);
                    _connectionIds[link.Id] = connection.Id == Guid.Empty ? Guid.NewGuid() : connection.Id;
                    _connectionPorts[link.Id] = (connection.SourcePort, connection.TargetPort);
                    Diagram.Links.Add(link);
                }
            });
            SetSelectedNode(null);
            if (resetHistory)
            {
                _undoStack.Clear();
                _redoStack.Clear();
            }
        }
        finally { _isLoadingGraph = false; }
        if (_isTrackingState) NotifyStateChanged(); else OnStateChanged?.Invoke();
    }

    public void ReplaceGraph(WorkflowGraphDto graph)
    {
        if (_currentWorkflow is null) CreateNewWorkflow();
        _currentWorkflow!.Graph = graph;
        LoadWorkflow(_currentWorkflow, resetHistory: false);
    }

    public void LoadFromTemplate(SonOfAnton.Shared.Models.WorkflowTemplate template)
    {
        var graph = System.Text.Json.JsonSerializer.Deserialize<WorkflowGraphDto>(
            System.Text.Json.JsonSerializer.Serialize(template.Graph)) ?? new();
        var ids = graph.Nodes.ToDictionary(n => n.Id, _ => Guid.NewGuid());
        foreach (var node in graph.Nodes) node.Id = ids[node.Id];
        foreach (var connection in graph.Connections)
        {
            connection.Id = Guid.NewGuid();
            connection.SourceNodeId = ids[connection.SourceNodeId];
            connection.TargetNodeId = ids[connection.TargetNodeId];
        }
        LoadWorkflow(new WorkflowDto { Name = template.Name, Description = template.Description, Graph = graph });
    }

    private Guid GetConnectionId(LinkModel link)
    {
        if (!_connectionIds.TryGetValue(link.Id, out var id))
            _connectionIds[link.Id] = id = Guid.NewGuid();
        return id;
    }

    public WorkflowGraphDto GetCurrentGraph()
    {
        var graph = new WorkflowGraphDto();

        // Mevcut workflow'un değişkenlerini koru
        if (_currentWorkflow?.Graph?.Variables != null)
        {
            graph.Variables = _currentWorkflow.Graph.Variables;
        }

        foreach (var node in Diagram.Nodes.OfType<WorkflowNodeModel>())
        {
            graph.Nodes.Add(new WorkflowNodeDto
            {
                Id = node.NodeData.Id,
                Type = node.NodeData.Type,
                Name = node.NodeData.Name,
                Icon = node.NodeData.Icon,
                Color = node.NodeData.Color,
                X = node.Position.X,
                Y = node.Position.Y,
                Parameters = node.NodeData.Parameters,
                ChildGraph = node.NodeData.ChildGraph,
                SubflowType = node.NodeData.SubflowType
            });
        }

        foreach (var link in Diagram.Links.OfType<LinkModel>())
        {
            // Link'in source ve target port'larından node'ları al
            var sourcePort = link.Source as SinglePortAnchor;
            var targetPort = link.Target as SinglePortAnchor;
            
            if (sourcePort?.Port?.Parent is WorkflowNodeModel sourceNode &&
                targetPort?.Port?.Parent is WorkflowNodeModel targetNode)
            {
                graph.Connections.Add(new NodeConnectionDto
                {
                    Id = GetConnectionId(link),
                    SourceNodeId = sourceNode.NodeData.Id,
                    TargetNodeId = targetNode.NodeData.Id,
                    SourcePort = _connectionPorts.TryGetValue(link.Id, out var ports) ? ports.Source : "output",
                    TargetPort = _connectionPorts.TryGetValue(link.Id, out ports) ? ports.Target : "input"
                });
            }
        }

        return graph;
    }

    public WorkflowNodeModel AddNode(NodeDefinitionDto definition, double x, double y)
    {
        var nodeDto = new WorkflowNodeDto
        {
            Id = Guid.NewGuid(),
            Type = definition.Type,
            Name = definition.Name,
            Icon = definition.Icon,
            Color = definition.Color,
            X = x,
            Y = y,
            Parameters = definition.Parameters
                .Where(p => p.DefaultValue != null)
                .ToDictionary(p => p.Name, p => p.DefaultValue)
        };

        var node = new WorkflowNodeModel(nodeDto, new Point(x, y));
        Diagram.Nodes.Add(node);
        
        NotifyStateChanged();
        return node;
    }

    public void RemoveSelectedNode()
    {
        if (_selectedNode != null)
        {
            Diagram.Nodes.Remove(_selectedNode);
            _selectedNode = null;
            OnNodeSelected?.Invoke(null);
            NotifyStateChanged();
        }
    }

    public void SelectNode(WorkflowNodeModel? node)
    {
        if (node is null) Diagram.UnselectAll();
        else Diagram.SelectModel(node, unselectOthers: true);
        SetSelectedNode(node);
    }

    private void SetSelectedNode(WorkflowNodeModel? node)
    {
        if (ReferenceEquals(_selectedNode, node)) return;
        _selectedNode = node;
        OnNodeSelected?.Invoke(node);
    }

    public void UpdateNodeParameter(Guid nodeId, string paramName, object? value)
    {
        var node = Diagram.Nodes.OfType<WorkflowNodeModel>()
            .FirstOrDefault(n => n.NodeData.Id == nodeId);
        
        if (node != null)
        {
            node.NodeData.Parameters[paramName] = value;
            NotifyStateChanged();
        }
    }

    public void NotifyStateChanged()
    {
        if (_isTrackingState && _currentWorkflow != null)
        {
            var graphJson = System.Text.Json.JsonSerializer.Serialize(GetCurrentGraph());
            
            if (_undoStack.Count == 0 || _undoStack.Peek() != graphJson)
            {
                _undoStack.Push(graphJson);
                _redoStack.Clear();
            }
        }
        OnStateChanged?.Invoke();
    }

    public bool CanUndo => _undoStack.Count > 1;
    public bool CanRedo => _redoStack.Count > 0;

    public void Undo()
    {
        if (CanUndo)
        {
            var currentJson = _undoStack.Pop();
            _redoStack.Push(currentJson);

            var previousJson = _undoStack.Peek();
            RestoreFromSnapshot(previousJson);
        }
    }

    public void Redo()
    {
        if (CanRedo)
        {
            var nextJson = _redoStack.Pop();
            _undoStack.Push(nextJson);
            RestoreFromSnapshot(nextJson);
        }
    }

    private void RestoreFromSnapshot(string json)
    {
        if (_currentWorkflow == null) return;
        
        _isTrackingState = false;
        try
        {
            var graph = System.Text.Json.JsonSerializer.Deserialize<WorkflowGraphDto>(json);
            if (graph != null)
            {
                _currentWorkflow.Graph = graph;
                LoadWorkflow(_currentWorkflow, resetHistory: false);
            }
        }
        finally
        {
            _isTrackingState = true;
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Workflow değişkenlerini günceller
    /// </summary>
    public void UpdateVariables(List<WorkflowVariableDto> variables)
    {
        if (_currentWorkflow != null)
        {
            _currentWorkflow.Graph ??= new WorkflowGraphDto();
            _currentWorkflow.Graph.Variables = [.. variables];
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Mevcut workflow'un değişkenlerini döndürür
    /// </summary>
    public List<WorkflowVariableDto> GetVariables()
    {
        return _currentWorkflow?.Graph?.Variables ?? [];
    }
    
    /// <summary>
    /// Node'un execution durumunu ayarlar
    /// </summary>
    public void SetNodeExecuting(Guid nodeId, bool isExecuting)
    {
        var node = Diagram.Nodes.OfType<WorkflowNodeModel>()
            .FirstOrDefault(n => n.NodeData.Id == nodeId);
        
        if (node != null)
        {
            node.IsExecuting = isExecuting;
            OnStateChanged?.Invoke();
        }
    }
    
    /// <summary>
    /// Node'un tamamlanma durumunu ayarlar
    /// </summary>
    public void SetNodeCompleted(Guid nodeId, bool success)
    {
        var node = Diagram.Nodes.OfType<WorkflowNodeModel>()
            .FirstOrDefault(n => n.NodeData.Id == nodeId);
        
        if (node != null)
        {
            node.IsCompleted = true;
            node.IsSuccess = success;
            OnStateChanged?.Invoke();
        }
    }
    
    /// <summary>
    /// Tüm node'ların execution durumunu sıfırlar
    /// </summary>
    public void ResetAllNodeStates()
    {
        foreach (var node in Diagram.Nodes.OfType<WorkflowNodeModel>())
        {
            node.IsExecuting = false;
            node.IsCompleted = false;
            node.IsSuccess = false;
            node.InlineOutput = null;
            node.ErrorMessage = null;
        }
        NotifyStateChanged();
    }
    
    public void SetNodeOutput(Guid nodeId, string output)
    {
        var node = Diagram.Nodes.OfType<WorkflowNodeModel>()
            .FirstOrDefault(n => n.NodeData.Id == nodeId);
        
        if (node != null)
        {
            node.InlineOutput = output;
            OnStateChanged?.Invoke();
        }
    }

    public void SetNodeError(Guid nodeId, string error)
    {
        var node = Diagram.Nodes.OfType<WorkflowNodeModel>()
            .FirstOrDefault(n => n.NodeData.Id == nodeId);
        
        if (node != null)
        {
            node.ErrorMessage = error;
            node.IsCompleted = true;
            node.IsSuccess = false;
            OnStateChanged?.Invoke();
        }
    }
}

/// <summary>
/// Custom node model - Blazor.Diagrams için
/// </summary>
public class WorkflowNodeModel : NodeModel
{
    public WorkflowNodeDto NodeData { get; }
    
    // Execution state
    public bool IsExecuting { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsSuccess { get; set; }
    public bool IsSuspended { get; set; }
    public bool IsDisabled { get; set; }
    public string? InlineOutput { get; set; }
    public string? ErrorMessage { get; set; }

    public WorkflowNodeModel(WorkflowNodeDto data, Point position) : base(position)
    {
        NodeData = data;
        
        // Input port (sol)
        AddPort(PortAlignment.Left);
        
        // Output port (sağ)
        AddPort(PortAlignment.Right);
    }
    
    /// <summary>
    /// Node'un görsel CSS sınıfını döndürür
    /// </summary>
    public string StateClass => IsExecuting ? "executing" : IsCompleted ? (IsSuccess ? "completed" : "failed") : "";
}


