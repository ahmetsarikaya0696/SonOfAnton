namespace SonOfAnton.Shared.Core.GraphEngine;

/// <summary>
/// DAG validation sonucu
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    
    public static ValidationResult Success() => new() { IsValid = true };
    
    public static ValidationResult Failure(params string[] errors) => new()
    {
        IsValid = false,
        Errors = [.. errors]
    };
    
    public static ValidationResult Failure(IEnumerable<string> errors) => new()
    {
        IsValid = false,
        Errors = errors.ToList()
    };
}

/// <summary>
/// Workflow graph'ını temsil eder
/// Node ve Edge'leri tutar, DAG validasyonu yapar
/// </summary>
public class Graph
{
    private readonly Dictionary<Guid, INode> _nodes = new();
    private readonly Dictionary<Guid, Edge> _edges = new();
    
    /// <summary>
    /// Graph ID
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// Graph adı
    /// </summary>
    public string Name { get; set; } = "Untitled Graph";
    
    /// <summary>
    /// Tüm node'lar
    /// </summary>
    public IReadOnlyDictionary<Guid, INode> Nodes => _nodes;
    
    /// <summary>
    /// Tüm edge'ler
    /// </summary>
    public IReadOnlyDictionary<Guid, Edge> Edges => _edges;
    
    /// <summary>
    /// Trigger node'ları (giriş bağlantısı olmayanlar)
    /// </summary>
    public IEnumerable<INode> TriggerNodes => 
        _nodes.Values.Where(n => !_edges.Values.Any(e => e.TargetNodeId == n.Id));
    
    /// <summary>
    /// Terminal node'ları (çıkış bağlantısı olmayanlar)
    /// </summary>
    public IEnumerable<INode> TerminalNodes =>
        _nodes.Values.Where(n => !_edges.Values.Any(e => e.SourceNodeId == n.Id));
    
    #region Node Operations
    
    /// <summary>
    /// Node ekler
    /// </summary>
    public void AddNode(INode node)
    {
        if (_nodes.ContainsKey(node.Id))
            throw new InvalidOperationException($"Node with ID {node.Id} already exists");
        
        _nodes[node.Id] = node;
    }
    
    /// <summary>
    /// Node kaldırır
    /// </summary>
    public bool RemoveNode(Guid nodeId)
    {
        if (!_nodes.Remove(nodeId))
            return false;
        
        // Bu node'a bağlı tüm edge'leri kaldır
        var edgesToRemove = _edges.Values
            .Where(e => e.SourceNodeId == nodeId || e.TargetNodeId == nodeId)
            .Select(e => e.Id)
            .ToList();
        
        foreach (var edgeId in edgesToRemove)
        {
            _edges.Remove(edgeId);
        }
        
        return true;
    }
    
    /// <summary>
    /// Node'u ID ile getirir
    /// </summary>
    public INode? GetNode(Guid nodeId) =>
        _nodes.TryGetValue(nodeId, out var node) ? node : null;
    
    /// <summary>
    /// Node'u günceller
    /// </summary>
    public bool UpdateNode(Guid nodeId, Action<INode> updateAction)
    {
        if (!_nodes.TryGetValue(nodeId, out var node))
            return false;
        
        updateAction(node);
        return true;
    }
    
    #endregion
    
    #region Edge Operations
    
    /// <summary>
    /// Edge ekler
    /// </summary>
    public ValidationResult AddEdge(Edge edge)
    {
        // Node'ların varlığını kontrol et
        if (!_nodes.TryGetValue(edge.SourceNodeId, out var sourceNode))
            return ValidationResult.Failure($"Source node {edge.SourceNodeId} not found");
        
        if (!_nodes.TryGetValue(edge.TargetNodeId, out var targetNode))
            return ValidationResult.Failure($"Target node {edge.TargetNodeId} not found");
        
        // Edge validasyonu
        if (!edge.IsValid(sourceNode, targetNode, out var error))
            return ValidationResult.Failure(error!);
        
        // Multiple bağlantı kontrolü
        var targetPin = targetNode.InputPins[edge.TargetPinName];
        if (!targetPin.Definition.AllowMultiple)
        {
            var existingConnection = _edges.Values.FirstOrDefault(e =>
                e.TargetNodeId == edge.TargetNodeId &&
                e.TargetPinName == edge.TargetPinName);
            
            if (existingConnection != null)
                return ValidationResult.Failure($"Pin '{edge.TargetPinName}' already has a connection");
        }
        
        // Cycle kontrolü (ekleme öncesi)
        _edges[edge.Id] = edge;
        if (HasCycle())
        {
            _edges.Remove(edge.Id);
            return ValidationResult.Failure("Adding this edge would create a cycle");
        }
        
        // Pin'lere edge referansı ekle
        sourceNode.OutputPins[edge.SourcePinName].ConnectedEdges.Add(edge.Id);
        targetNode.InputPins[edge.TargetPinName].ConnectedEdges.Add(edge.Id);

        // Edge bağlantı event'ini tetikle
        edge.NotifyConnected();
        
        return ValidationResult.Success();
    }
    
    /// <summary>
    /// Edge kaldırır
    /// </summary>
    public bool RemoveEdge(Guid edgeId)
    {
        if (!_edges.TryGetValue(edgeId, out var edge))
            return false;
        
        // Pin'lerden referansı kaldır
        if (_nodes.TryGetValue(edge.SourceNodeId, out var sourceNode))
            sourceNode.OutputPins[edge.SourcePinName].ConnectedEdges.Remove(edgeId);
        
        if (_nodes.TryGetValue(edge.TargetNodeId, out var targetNode))
            targetNode.InputPins[edge.TargetPinName].ConnectedEdges.Remove(edgeId);

        // Edge bağlantı koparma event'ini tetikle
        edge.NotifyDisconnected();

        return _edges.Remove(edgeId);
    }
    
    /// <summary>
    /// Edge'i ID ile getirir
    /// </summary>
    public Edge? GetEdge(Guid edgeId) =>
        _edges.TryGetValue(edgeId, out var edge) ? edge : null;
    
    #endregion
    
    #region DAG Validation
    
    /// <summary>
    /// Graph'ta cycle var mı kontrol eder (Kahn's algorithm)
    /// </summary>
    public bool HasCycle()
    {
        // Her node için gelen edge sayısını hesapla
        var inDegree = _nodes.Keys.ToDictionary(id => id, _ => 0);
        foreach (var edge in _edges.Values)
        {
            inDegree[edge.TargetNodeId]++;
        }
        
        // Gelen edge'i olmayan node'ları kuyruğa ekle
        var queue = new Queue<Guid>();
        foreach (var (nodeId, degree) in inDegree)
        {
            if (degree == 0)
                queue.Enqueue(nodeId);
        }
        
        var processedCount = 0;
        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            processedCount++;
            
            // Bu node'dan çıkan edge'lerin hedeflerinin in-degree'sini azalt
            foreach (var edge in _edges.Values.Where(e => e.SourceNodeId == nodeId))
            {
                inDegree[edge.TargetNodeId]--;
                if (inDegree[edge.TargetNodeId] == 0)
                    queue.Enqueue(edge.TargetNodeId);
            }
        }
        
        // Tüm node'lar işlenmediyse cycle var
        return processedCount != _nodes.Count;
    }

    /// <summary>
    /// Tarjan's strongly connected components algoritması ile cycle'ları bulur
    /// </summary>
    public List<List<INode>> FindStronglyConnectedComponents()
    {
        var index = 0;
        var stack = new Stack<Guid>();
        var onStack = new HashSet<Guid>();
        var nodeIndex = new Dictionary<Guid, int>();
        var lowLink = new Dictionary<Guid, int>();
        var result = new List<List<INode>>();

        void StrongConnect(Guid nodeId)
        {
            nodeIndex[nodeId] = index;
            lowLink[nodeId] = index;
            index++;
            stack.Push(nodeId);
            onStack.Add(nodeId);

            foreach (var edge in _edges.Values.Where(e => e.SourceNodeId == nodeId))
            {
                var targetId = edge.TargetNodeId;
                if (!nodeIndex.ContainsKey(targetId))
                {
                    StrongConnect(targetId);
                    lowLink[nodeId] = Math.Min(lowLink[nodeId], lowLink[targetId]);
                }
                else if (onStack.Contains(targetId))
                {
                    lowLink[nodeId] = Math.Min(lowLink[nodeId], nodeIndex[targetId]);
                }
            }

            if (lowLink[nodeId] == nodeIndex[nodeId])
            {
                var component = new List<INode>();
                Guid w;
                do
                {
                    w = stack.Pop();
                    onStack.Remove(w);
                    if (_nodes.TryGetValue(w, out var node))
                        component.Add(node);
                } while (w != nodeId);

                result.Add(component);
            }
        }

        foreach (var nodeId in _nodes.Keys)
        {
            if (!nodeIndex.ContainsKey(nodeId))
                StrongConnect(nodeId);
        }

        return result;
    }

    /// <summary>
    /// Bağlantısız (disconnected) bileşenleri bulur
    /// </summary>
    public List<List<INode>> FindDisconnectedComponents()
    {
        var visited = new HashSet<Guid>();
        var components = new List<List<INode>>();

        foreach (var nodeId in _nodes.Keys)
        {
            if (visited.Contains(nodeId)) continue;

            var component = new List<INode>();
            var queue = new Queue<Guid>();
            queue.Enqueue(nodeId);
            visited.Add(nodeId);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                if (_nodes.TryGetValue(currentId, out var node))
                    component.Add(node);

                // İki yönlü komşuları ekle (undirected graph gibi davran)
                foreach (var edge in _edges.Values)
                {
                    Guid? neighborId = null;
                    if (edge.SourceNodeId == currentId) neighborId = edge.TargetNodeId;
                    else if (edge.TargetNodeId == currentId) neighborId = edge.SourceNodeId;

                    if (neighborId.HasValue && visited.Add(neighborId.Value))
                        queue.Enqueue(neighborId.Value);
                }
            }

            components.Add(component);
        }

        return components;
    }

    /// <summary>
    /// Parallel execution seviyelerini hesaplar.
    /// Aynı seviyedeki node'lar paralel çalıştırılabilir.
    /// </summary>
    public List<List<INode>> GetParallelExecutionLevels()
    {
        var levels = new List<List<INode>>();
        var inDegree = _nodes.Keys.ToDictionary(id => id, _ => 0);

        foreach (var edge in _edges.Values)
        {
            inDegree[edge.TargetNodeId]++;
        }

        var currentLevel = _nodes.Keys.Where(id => inDegree[id] == 0).ToList();

        while (currentLevel.Count > 0)
        {
            var levelNodes = new List<INode>();
            var nextLevel = new List<Guid>();

            foreach (var nodeId in currentLevel)
            {
                if (_nodes.TryGetValue(nodeId, out var node))
                    levelNodes.Add(node);

                foreach (var edge in _edges.Values.Where(e => e.SourceNodeId == nodeId))
                {
                    inDegree[edge.TargetNodeId]--;
                    if (inDegree[edge.TargetNodeId] == 0)
                        nextLevel.Add(edge.TargetNodeId);
                }
            }

            if (levelNodes.Count > 0)
                levels.Add(levelNodes);

            currentLevel = nextLevel;
        }

        return levels;
    }
    
    
    /// <summary>
    /// Topological sort (execution order)
    /// </summary>
    public List<INode> GetTopologicalOrder()
    {
        var result = new List<INode>();
        var inDegree = _nodes.Keys.ToDictionary(id => id, _ => 0);
        
        foreach (var edge in _edges.Values)
        {
            inDegree[edge.TargetNodeId]++;
        }
        
        var queue = new Queue<Guid>();
        foreach (var (nodeId, degree) in inDegree)
        {
            if (degree == 0)
                queue.Enqueue(nodeId);
        }
        
        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            result.Add(_nodes[nodeId]);
            
            foreach (var edge in _edges.Values.Where(e => e.SourceNodeId == nodeId))
            {
                inDegree[edge.TargetNodeId]--;
                if (inDegree[edge.TargetNodeId] == 0)
                    queue.Enqueue(edge.TargetNodeId);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Graph'ı tam olarak validate eder
    /// </summary>
    public ValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        
        // 1. Boş graph kontrolü
        if (_nodes.Count == 0)
        {
            errors.Add("Graph has no nodes");
            return ValidationResult.Failure(errors);
        }
        
        // 2. Trigger node kontrolü
        if (!TriggerNodes.Any())
        {
            errors.Add("Graph has no trigger nodes (nodes without input connections)");
        }
        
        // 3. Cycle kontrolü (Tarjan's SCC ile detaylı)
        var sccs = FindStronglyConnectedComponents();
        var cyclicComponents = sccs.Where(c => c.Count > 1).ToList();
        if (cyclicComponents.Count > 0)
        {
            foreach (var component in cyclicComponents)
            {
                var nodeNames = string.Join(", ", component.Select(n => $"'{n.Name}' ({n.Type})"));
                errors.Add($"Cycle detected among nodes: {nodeNames}");
            }
        }
        
        // 4. Required pin kontrolü
        foreach (var node in _nodes.Values)
        {
            foreach (var (pinName, pin) in node.InputPins)
            {
                if (pin.Definition.Required && pin.ConnectedEdges.Count == 0 && pin.Definition.DefaultValue == null)
                {
                    errors.Add($"Node '{node.Name}' ({node.Type}): Required input pin '{pinName}' is not connected");
                }
            }
        }
        
        // 5. Orphan node kontrolü (warning)
        foreach (var node in _nodes.Values)
        {
            var hasInput = _edges.Values.Any(e => e.TargetNodeId == node.Id);
            var hasOutput = _edges.Values.Any(e => e.SourceNodeId == node.Id);
            
            if (!hasInput && !hasOutput && _nodes.Count > 1)
            {
                warnings.Add($"Node '{node.Name}' ({node.Type}) is not connected to the graph");
            }
        }

        // 6. Disconnected component analizi (warning)
        var components = FindDisconnectedComponents();
        if (components.Count > 1)
        {
            warnings.Add($"Graph has {components.Count} disconnected components. Consider connecting them or splitting into separate workflows.");
        }

        // 7. Pin uyumluluk matrisi validasyonu (tüm edge'ler)
        foreach (var edge in _edges.Values)
        {
            if (!_nodes.TryGetValue(edge.SourceNodeId, out var sourceNode) ||
                !_nodes.TryGetValue(edge.TargetNodeId, out var targetNode))
                continue;

            if (!edge.IsValid(sourceNode, targetNode, out var edgeError))
            {
                errors.Add($"Edge {edge.Id}: {edgeError}");
            }
        }
        
        return errors.Count == 0
            ? new ValidationResult { IsValid = true, Warnings = warnings }
            : new ValidationResult { IsValid = false, Errors = errors, Warnings = warnings };
    }
    
    #endregion
    
    #region Graph Traversal
    
    /// <summary>
    /// Bir node'un tüm predecessor'larını bulur (BFS)
    /// </summary>
    public IEnumerable<INode> GetPredecessors(Guid nodeId)
    {
        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        
        // Başlangıç node'una gelen edge'lerin source'larını ekle
        foreach (var edge in _edges.Values.Where(e => e.TargetNodeId == nodeId))
        {
            if (visited.Add(edge.SourceNodeId))
                queue.Enqueue(edge.SourceNodeId);
        }
        
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (_nodes.TryGetValue(currentId, out var node))
                yield return node;
            
            foreach (var edge in _edges.Values.Where(e => e.TargetNodeId == currentId))
            {
                if (visited.Add(edge.SourceNodeId))
                    queue.Enqueue(edge.SourceNodeId);
            }
        }
    }
    
    /// <summary>
    /// Bir node'un tüm successor'larını bulur (BFS)
    /// </summary>
    public IEnumerable<INode> GetSuccessors(Guid nodeId)
    {
        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        
        foreach (var edge in _edges.Values.Where(e => e.SourceNodeId == nodeId))
        {
            if (visited.Add(edge.TargetNodeId))
                queue.Enqueue(edge.TargetNodeId);
        }
        
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (_nodes.TryGetValue(currentId, out var node))
                yield return node;
            
            foreach (var edge in _edges.Values.Where(e => e.SourceNodeId == currentId))
            {
                if (visited.Add(edge.TargetNodeId))
                    queue.Enqueue(edge.TargetNodeId);
            }
        }
    }
    
    /// <summary>
    /// Bir node'un immediate child'larını bulur
    /// </summary>
    public IEnumerable<(INode Node, Edge Edge)> GetChildren(Guid nodeId)
    {
        foreach (var edge in _edges.Values.Where(e => e.SourceNodeId == nodeId))
        {
            if (_nodes.TryGetValue(edge.TargetNodeId, out var node))
                yield return (node, edge);
        }
    }
    
    /// <summary>
    /// Bir node'un immediate parent'larını bulur
    /// </summary>
    public IEnumerable<(INode Node, Edge Edge)> GetParents(Guid nodeId)
    {
        foreach (var edge in _edges.Values.Where(e => e.TargetNodeId == nodeId))
        {
            if (_nodes.TryGetValue(edge.SourceNodeId, out var node))
                yield return (node, edge);
        }
    }
    
    #endregion
    
    #region Serialization Helpers
    
    /// <summary>
    /// Graph'ı temizler
    /// </summary>
    public void Clear()
    {
        _nodes.Clear();
        _edges.Clear();
    }
    
    /// <summary>
    /// Tüm node'ların state'ini sıfırlar
    /// </summary>
    public void ResetAllNodes()
    {
        foreach (var node in _nodes.Values)
        {
            if (node is NodeBase nodeBase)
                nodeBase.Reset();
        }
        
        foreach (var edge in _edges.Values)
        {
            edge.State = EdgeState.Idle;
        }
    }
    
    #endregion
}
