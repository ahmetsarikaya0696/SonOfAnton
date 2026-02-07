using System.Text.Json;

namespace SonOfAnton.Shared.Core.GraphEngine;

/// <summary>
/// Node'ların temel sınıfı
/// Tüm node tipleri bu sınıftan türetilmeli
/// </summary>
public abstract class NodeBase : INode
{
    private readonly Dictionary<string, Pin> _inputPins = new();
    private readonly Dictionary<string, Pin> _outputPins = new();
    private ExecutionState _state = ExecutionState.Idle;
    
    /// <inheritdoc />
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <inheritdoc />
    public abstract string Type { get; }
    
    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;
    
    /// <inheritdoc />
    public double X { get; set; }
    
    /// <inheritdoc />
    public double Y { get; set; }
    
    /// <inheritdoc />
    public IReadOnlyDictionary<string, Pin> InputPins => _inputPins;
    
    /// <inheritdoc />
    public IReadOnlyDictionary<string, Pin> OutputPins => _outputPins;
    
    /// <inheritdoc />
    public Dictionary<string, object?> Parameters { get; } = new();
    
    /// <inheritdoc />
    public ExecutionState State
    {
        get => _state;
        set
        {
            if (_state == value) return;
            if (!ExecutionStateTransitions.IsValidTransition(_state, value))
                throw ExecutionStateTransitions.InvalidTransition(_state, value);
            _state = value;
        }
    }
    
    /// <inheritdoc />
    public string? LastError { get; set; }

    /// <inheritdoc />
    public abstract NodeCategory Category { get; }

    /// <inheritdoc />
    public Dictionary<string, object?> Metadata { get; } = new();

    /// <inheritdoc />
    public ExecutionProgress Progress { get; } = new();
    
    protected NodeBase()
    {
        // Alt sınıflar constructor'da DefineInputPin ve DefineOutputPin çağırmalı
    }
    
    /// <summary>
    /// Input pin tanımlar
    /// </summary>
    protected void DefineInputPin(PinDefinition definition)
    {
        if (definition.Direction != PinDirection.Input)
            throw new ArgumentException("Pin direction must be Input", nameof(definition));
        
        _inputPins[definition.Name] = new Pin { Definition = definition };
    }
    
    /// <summary>
    /// Output pin tanımlar
    /// </summary>
    protected void DefineOutputPin(PinDefinition definition)
    {
        if (definition.Direction != PinDirection.Output)
            throw new ArgumentException("Pin direction must be Output", nameof(definition));
        
        _outputPins[definition.Name] = new Pin { Definition = definition };
    }
    
    /// <summary>
    /// Input pin'den değer alır
    /// </summary>
    protected T? GetInput<T>(string pinName)
    {
        if (!_inputPins.TryGetValue(pinName, out var pin))
            throw new KeyNotFoundException($"Input pin '{pinName}' not found");
        
        if (!pin.HasValue)
        {
            // Default value kullan
            if (pin.Definition.DefaultValue is T defaultVal)
                return defaultVal;
            return default;
        }
        
        if (pin.Value is T value)
            return value;
        
        // Tip dönüşümü dene
        try
        {
            return (T)Convert.ChangeType(pin.Value, typeof(T))!;
        }
        catch
        {
            return default;
        }
    }
    
    /// <summary>
    /// Output pin'e değer set eder
    /// </summary>
    protected void SetOutput(string pinName, object? value)
    {
        if (!_outputPins.TryGetValue(pinName, out var pin))
            throw new KeyNotFoundException($"Output pin '{pinName}' not found");
        
        pin.Value = value;
        pin.HasValue = true;
    }
    
    /// <summary>
    /// Parametre değeri alır
    /// </summary>
    protected T? GetParameter<T>(string name, T? defaultValue = default)
    {
        if (!Parameters.TryGetValue(name, out var value) || value is null)
            return defaultValue;
        
        if (value is T typedValue)
            return typedValue;
        
        // JSON element dönüşümü
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return ConvertJsonElement<T>(jsonElement, defaultValue);
        }
        
        try
        {
            return (T)Convert.ChangeType(value, typeof(T))!;
        }
        catch
        {
            return defaultValue;
        }
    }
    
    private static T? ConvertJsonElement<T>(System.Text.Json.JsonElement element, T? defaultValue)
    {
        try
        {
            return element.Deserialize<T>();
        }
        catch
        {
            return defaultValue;
        }
    }
    
    /// <summary>
    /// Tüm input değerlerini temizler
    /// </summary>
    public void ClearInputs()
    {
        foreach (var pin in _inputPins.Values)
        {
            pin.Value = null;
            pin.HasValue = false;
        }
    }
    
    /// <summary>
    /// Tüm output değerlerini temizler
    /// </summary>
    public void ClearOutputs()
    {
        foreach (var pin in _outputPins.Values)
        {
            pin.Value = null;
            pin.HasValue = false;
        }
    }
    
    /// <summary>
    /// Node durumunu sıfırlar
    /// </summary>
    public virtual void Reset()
    {
        State = ExecutionState.Idle;
        LastError = null;
        ClearInputs();
        ClearOutputs();
    }
}

/// <summary>
/// Trigger node'ları için base class (giriş bağlantısı olmayan)
/// </summary>
public abstract class TriggerNodeBase : NodeBase
{
    public override NodeCategory Category => NodeCategory.Trigger;

    protected TriggerNodeBase()
    {
        // Trigger node'larının default output'u
        DefineOutputPin(new PinDefinition
        {
            Name = "trigger",
            DisplayName = "Trigger",
            Direction = PinDirection.Output,
            DataType = PinType.Flow
        });
    }
}

/// <summary>
/// Standart işlem node'ları için base class
/// </summary>
public abstract class ProcessNodeBase : NodeBase
{
    public override NodeCategory Category => NodeCategory.Action;

    protected ProcessNodeBase()
    {
        // Default input ve output
        DefineInputPin(new PinDefinition
        {
            Name = "input",
            DisplayName = "Input",
            Direction = PinDirection.Input,
            DataType = PinType.Any
        });
        
        DefineOutputPin(new PinDefinition
        {
            Name = "output",
            DisplayName = "Output",
            Direction = PinDirection.Output,
            DataType = PinType.Any
        });
    }
}

/// <summary>
/// Branch/condition node'ları için base class
/// </summary>
public abstract class BranchNodeBase : NodeBase
{
    public override NodeCategory Category => NodeCategory.Logic;

    protected BranchNodeBase()
    {
        DefineInputPin(new PinDefinition
        {
            Name = "input",
            DisplayName = "Input",
            Direction = PinDirection.Input,
            DataType = PinType.Any
        });
        
        DefineOutputPin(new PinDefinition
        {
            Name = "true",
            DisplayName = "True",
            Direction = PinDirection.Output,
            DataType = PinType.Any,
        });
        
        DefineOutputPin(new PinDefinition
        {
            Name = "false",
            DisplayName = "False",
            Direction = PinDirection.Output,
            DataType = PinType.Any
        });
    }
}
