using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Enums;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SonOfAnton.Shared.Serialization;

/// <summary>
/// WorkflowGraphDto ↔ YAML dönüşümü
/// </summary>
public class WorkflowYamlSerializer
{
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

    public WorkflowYamlSerializer()
    {
        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public string Serialize(WorkflowGraphDto graph)
    {
        var yamlModel = new WorkflowYamlModel
        {
            Version = "1.0",
            Nodes = graph.Nodes.Select(n => new YamlNode
            {
                Id = n.Id.ToString(),
                Type = n.Type,
                Name = n.Name,
                Position = new YamlPosition { X = n.X, Y = n.Y },
                Parameters = n.Parameters.Count > 0 ? n.Parameters : null,
                SubflowType = n.SubflowType?.ToString(),
                ChildGraph = n.ChildGraph != null ? Serialize(n.ChildGraph) : null
            }).ToList(),
            Connections = graph.Connections.Select(c => new YamlConnection
            {
                From = c.SourceNodeId.ToString(),
                To = c.TargetNodeId.ToString(),
                FromPort = c.SourcePort,
                ToPort = c.TargetPort
            }).ToList(),
            Variables = graph.Variables.Select(v => new YamlVariable
            {
                Name = v.Name,
                Type = v.Type.ToString().ToLowerInvariant(),
                Default = v.DefaultValue,
                Scope = v.Scope.ToString().ToLowerInvariant(),
                Description = v.Description
            }).ToList()
        };

        return _serializer.Serialize(yamlModel);
    }

    public WorkflowGraphDto? Deserialize(string yaml)
    {
        var model = _deserializer.Deserialize<WorkflowYamlModel>(yaml);
        if (model == null) return null;

        return new WorkflowGraphDto
        {
            Nodes = model.Nodes.Select(n => new WorkflowNodeDto
            {
                Id = Guid.TryParse(n.Id, out var id) ? id : Guid.NewGuid(),
                Type = n.Type,
                Name = n.Name,
                X = n.Position?.X ?? 0,
                Y = n.Position?.Y ?? 0,
                Parameters = n.Parameters ?? [],
                SubflowType = Enum.TryParse<SubflowType>(n.SubflowType, true, out var st) ? st : null
            }).ToList(),
            Connections = model.Connections.Select(c => new NodeConnectionDto
            {
                Id = Guid.NewGuid(),
                SourceNodeId = Guid.TryParse(c.From, out var from) ? from : Guid.Empty,
                TargetNodeId = Guid.TryParse(c.To, out var to) ? to : Guid.Empty,
                SourcePort = c.FromPort ?? "output",
                TargetPort = c.ToPort ?? "input"
            }).ToList(),
            Variables = model.Variables.Select(v => new WorkflowVariableDto
            {
                Name = v.Name,
                Type = Enum.TryParse<VariableType>(v.Type, true, out var vt) ? vt : VariableType.String,
                DefaultValue = v.Default,
                Scope = Enum.TryParse<VariableScope>(v.Scope, true, out var vs) ? vs : VariableScope.Execution,
                Description = v.Description
            }).ToList()
        };
    }

    public WorkflowYamlValidationResult Validate(string yaml)
    {
        try
        {
            var graph = Deserialize(yaml);
            if (graph == null)
                return new WorkflowYamlValidationResult { IsValid = false, Errors = ["YAML ayrıştırılamadı"] };

            var errors = new List<string>();

            if (graph.Nodes.Count == 0)
                errors.Add("En az bir node gerekli");

            var nodeIds = graph.Nodes.Select(n => n.Id).ToHashSet();
            foreach (var conn in graph.Connections)
            {
                if (!nodeIds.Contains(conn.SourceNodeId))
                    errors.Add($"Bağlantı kaynağı bulunamadı: {conn.SourceNodeId}");
                if (!nodeIds.Contains(conn.TargetNodeId))
                    errors.Add($"Bağlantı hedefi bulunamadı: {conn.TargetNodeId}");
            }

            return new WorkflowYamlValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                ParsedGraph = graph
            };
        }
        catch (Exception ex)
        {
            return new WorkflowYamlValidationResult
            {
                IsValid = false,
                Errors = [$"YAML parse hatası: {ex.Message}"]
            };
        }
    }
}

public class WorkflowYamlValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = [];
    public WorkflowGraphDto? ParsedGraph { get; set; }
}

// YAML intermediate models
internal class WorkflowYamlModel
{
    public string Version { get; set; } = "1.0";
    public List<YamlNode> Nodes { get; set; } = [];
    public List<YamlConnection> Connections { get; set; } = [];
    public List<YamlVariable> Variables { get; set; } = [];
}

internal class YamlNode
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public YamlPosition? Position { get; set; }
    public Dictionary<string, object?>? Parameters { get; set; }
    public string? SubflowType { get; set; }
    public string? ChildGraph { get; set; }
}

internal class YamlPosition
{
    public double X { get; set; }
    public double Y { get; set; }
}

internal class YamlConnection
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string? FromPort { get; set; }
    public string? ToPort { get; set; }
}

internal class YamlVariable
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public string? Default { get; set; }
    public string Scope { get; set; } = "execution";
    public string? Description { get; set; }
}
