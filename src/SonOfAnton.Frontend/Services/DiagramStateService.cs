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
/// Blazor.Diagrams state y�netimi
/// </summary>
public class DiagramStateService
{
    private BlazorDiagram? _diagram;
    private WorkflowDto? _currentWorkflow;
    private WorkflowNodeModel? _selectedNode;

    private Stack<string> _undoStack = new();
    private Stack<string> _redoStack = new();
    private bool _isTrackingState = true;

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

        return diagram;
    }


    public void CreateNewWorkflow()
    {
        _currentWorkflow = new WorkflowDto
        {
            Name = "Yeni İş Akışı",
            Graph = new WorkflowGraphDto()
        };
        
        Diagram.Nodes.Clear();
        Diagram.Links.Clear();
        
        _selectedNode = null;
        NotifyStateChanged();
    }

    public void LoadWorkflow(WorkflowDto workflow)
    {
        _currentWorkflow = workflow;
        Diagram.Nodes.Clear();
        Diagram.Links.Clear();

        if (workflow.Graph == null)
            return;

        // Node'ları ekle
        var nodeMap = new Dictionary<Guid, WorkflowNodeModel>();
        foreach (var nodeDto in workflow.Graph.Nodes)
        {
            var node = new WorkflowNodeModel(nodeDto, new Point(nodeDto.X, nodeDto.Y));
            Diagram.Nodes.Add(node);
            nodeMap[nodeDto.Id] = node;
        }

        // Bağlantıları ekle
        foreach (var conn in workflow.Graph.Connections)
        {
            if (nodeMap.TryGetValue(conn.SourceNodeId, out var sourceNode) &&
                nodeMap.TryGetValue(conn.TargetNodeId, out var targetNode))
            {
                var sourcePort = sourceNode.GetPort(PortAlignment.Right);
                var targetPort = targetNode.GetPort(PortAlignment.Left);
                
                if (sourcePort != null && targetPort != null)
                {
                    var link = new LinkModel(sourcePort, targetPort);
                    Diagram.Links.Add(link);
                }
            }
        }

        _selectedNode = null;
        if (_isTrackingState) NotifyStateChanged(); else OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Şablondan yeni workflow oluşturur
    /// </summary>
    public void LoadFromTemplate(SonOfAnton.Shared.Models.WorkflowTemplate template)
    {
        _currentWorkflow = new WorkflowDto
        {
            Id = Guid.Empty,
            Name = template.Name,
            Description = template.Description,
            Graph = template.Graph
        };

        Diagram.Nodes.Clear();
        Diagram.Links.Clear();

        if (template.Graph == null)
            return;

        // Node'ları ekle
        var nodeMap = new Dictionary<Guid, WorkflowNodeModel>();
        foreach (var nodeDto in template.Graph.Nodes)
        {
            // Yeni ID ata (Şablon ID'lerini kullanma)
            var newNodeDto = new WorkflowNodeDto
            {
                Id = Guid.NewGuid(),
                Type = nodeDto.Type,
                Name = nodeDto.Name,
                Icon = nodeDto.Icon,
                Color = nodeDto.Color,
                X = nodeDto.X,
                Y = nodeDto.Y,
                Parameters = new Dictionary<string, object?>(nodeDto.Parameters)
            };
            
            var node = new WorkflowNodeModel(newNodeDto, new Point(newNodeDto.X, newNodeDto.Y));
            Diagram.Nodes.Add(node);
            nodeMap[nodeDto.Id] = node; // Eski ID ile map'le (bağlantılar için)
        }

        // Bağlantıları ekle
        foreach (var conn in template.Graph.Connections)
        {
            if (nodeMap.TryGetValue(conn.SourceNodeId, out var sourceNode) &&
                nodeMap.TryGetValue(conn.TargetNodeId, out var targetNode))
            {
                var sourcePort = sourceNode.GetPort(PortAlignment.Right);
                var targetPort = targetNode.GetPort(PortAlignment.Left);
                
                if (sourcePort != null && targetPort != null)
                {
                    var link = new LinkModel(sourcePort, targetPort);
                    Diagram.Links.Add(link);
                }
            }
        }

        _selectedNode = null;
        if (_isTrackingState) NotifyStateChanged(); else OnStateChanged?.Invoke();
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
                    Id = Guid.NewGuid(),
                    SourceNodeId = sourceNode.NodeData.Id,
                    TargetNodeId = targetNode.NodeData.Id,
                    SourcePort = "output",
                    TargetPort = "input"
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
                LoadWorkflow(_currentWorkflow);
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
            _currentWorkflow.Graph.Variables = variables;
            OnStateChanged?.Invoke();
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
        }
        NotifyStateChanged();
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


