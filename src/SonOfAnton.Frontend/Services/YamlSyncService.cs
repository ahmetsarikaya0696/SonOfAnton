using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Serialization;

namespace SonOfAnton.Frontend.Services;

/// <summary>
/// YAML ↔ Graph canlı senkronizasyonu
/// </summary>
public class YamlSyncService
{
    private readonly WorkflowYamlSerializer _yamlSerializer;
    private System.Timers.Timer? _debounceTimer;
    private string _lastYaml = string.Empty;

    public event Action<WorkflowGraphDto>? OnGraphChanged;
    public event Action<string>? OnYamlChanged;
    public event Action<List<string>>? OnValidationErrors;

    public YamlSyncService()
    {
        _yamlSerializer = new WorkflowYamlSerializer();
    }

    /// <summary>
    /// Graph değiştiğinde YAML güncelle
    /// </summary>
    public void UpdateFromGraph(WorkflowGraphDto graph)
    {
        try
        {
            var yaml = _yamlSerializer.Serialize(graph);
            if (yaml != _lastYaml)
            {
                _lastYaml = yaml;
                OnYamlChanged?.Invoke(yaml);
            }
        }
        catch
        {
            // Serialize hatası — sessizce geç
        }
    }

    /// <summary>
    /// YAML değiştiğinde graph güncelle (debounce ile)
    /// </summary>
    public void UpdateFromYaml(string yaml)
    {
        _debounceTimer?.Stop();
        _debounceTimer?.Dispose();

        _debounceTimer = new System.Timers.Timer(500);
        _debounceTimer.Elapsed += (_, _) =>
        {
            _debounceTimer?.Stop();
            ParseAndNotify(yaml);
        };
        _debounceTimer.AutoReset = false;
        _debounceTimer.Start();
    }

    private void ParseAndNotify(string yaml)
    {
        var validation = _yamlSerializer.Validate(yaml);
        if (validation.IsValid && validation.ParsedGraph != null)
        {
            _lastYaml = yaml;
            OnGraphChanged?.Invoke(validation.ParsedGraph);
            OnValidationErrors?.Invoke([]);
        }
        else
        {
            OnValidationErrors?.Invoke(validation.Errors);
        }
    }

    public string SerializeGraph(WorkflowGraphDto graph)
    {
        return _yamlSerializer.Serialize(graph);
    }

    public WorkflowGraphDto? DeserializeYaml(string yaml)
    {
        return _yamlSerializer.Deserialize(yaml);
    }
}
