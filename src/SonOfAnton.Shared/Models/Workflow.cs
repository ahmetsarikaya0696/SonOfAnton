namespace SonOfAnton.Shared.Models;

/// <summary>
/// Bir workflow'u temsil eder
/// </summary>
public class Workflow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Isimsiz Is Akisi";
    public string Description { get; set; } = string.Empty;
    public List<WorkflowNode> Nodes { get; set; } = [];
    public List<NodeConnection> Connections { get; set; } = [];
    public List<WorkflowGroup> Groups { get; set; } = [];
    public List<WorkflowNote> Notes { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; }
    public Guid? GlobalErrorWorkflowId { get; set; }
}

/// <summary>
/// Workflow içindeki bir node
/// </summary>
public class WorkflowNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Type { get; set; }
    public required string Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public Dictionary<string, object?> Parameters { get; set; } = [];
    public NodeStatus Status { get; set; } = NodeStatus.Idle;
    public string? LastError { get; set; }
    public DateTime? LastExecutedAt { get; set; }
    public bool IsBypassed { get; set; }
    public Guid? GroupId { get; set; }
}

/// <summary>
/// İki node arasındaki bağlantı
/// </summary>
public class NodeConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }
    public string SourcePort { get; set; } = "output";
    public string TargetPort { get; set; } = "input";
}

/// <summary>
/// Node grubu
/// </summary>
public class WorkflowGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Group";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 400;
    public double Height { get; set; } = 300;
    public string Color { get; set; } = "#313244"; // Surface0
}

/// <summary>
/// Canvas üzerindeki yapışkan not
/// </summary>
public class WorkflowNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = "Not...";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 200;
    public double Height { get; set; } = 150;
    public string Color { get; set; } = "#f9e2af"; // Yellow
}

/// <summary>
/// Node durumu
/// </summary>
public enum NodeStatus
{
    Idle,
    Running,
    Success,
    Error,
    Disabled
}

/// <summary>
/// Node tipi tanımı
/// </summary>
public class NodeDefinition
{
    public required string Type { get; set; }
    public required string Name { get; set; }
    public required string Icon { get; set; }
    public required string Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#89b4fa";
    public List<NodePort> Inputs { get; set; } = [];
    public List<NodePort> Outputs { get; set; } = [];
    public List<NodeParameter> Parameters { get; set; } = [];
}

/// <summary>
/// Node giriş/çıkış portu
/// </summary>
public class NodePort
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public bool Multiple { get; set; }
}

/// <summary>
/// Node parametresi
/// </summary>
public class NodeParameter
{
    public required string Name { get; set; }
    public required string DisplayName { get; set; }
    public required string Type { get; set; } // string, number, boolean, select, code, json
    public object? DefaultValue { get; set; }
    public bool Required { get; set; }
    public string? Placeholder { get; set; }
    public List<SelectOption>? Options { get; set; }
}

public class SelectOption
{
    public required string Value { get; set; }
    public required string Label { get; set; }
}
