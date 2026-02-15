using System.Collections.Concurrent;

namespace SonOfAnton.Backend.Engine.Expressions;

/// <summary>
/// Scoped variable store — execution sırasında değişken okuma/yazma
/// </summary>
public class VariableStore
{
    private readonly ConcurrentDictionary<string, object?> _variables = new();
    private readonly VariableStore? _parent;

    public VariableStore(VariableStore? parent = null)
    {
        _parent = parent;
    }

    /// <summary>
    /// Değişken ayarla
    /// </summary>
    public void Set(string name, object? value)
    {
        _variables[name] = value;
    }

    /// <summary>
    /// Değişken oku (önce local scope, sonra parent scope)
    /// </summary>
    public object? Get(string name)
    {
        if (_variables.TryGetValue(name, out var value))
            return value;

        return _parent?.Get(name);
    }

    /// <summary>
    /// Değişken var mı
    /// </summary>
    public bool Has(string name)
    {
        return _variables.ContainsKey(name) || (_parent?.Has(name) ?? false);
    }

    /// <summary>
    /// Child scope oluştur
    /// </summary>
    public VariableStore CreateChildScope()
    {
        return new VariableStore(this);
    }

    /// <summary>
    /// Tüm değişkenleri döndür (parent dahil)
    /// </summary>
    public Dictionary<string, object?> GetAll()
    {
        var result = _parent?.GetAll() ?? new Dictionary<string, object?>();
        foreach (var (key, value) in _variables)
        {
            result[key] = value;
        }
        return result;
    }
}
