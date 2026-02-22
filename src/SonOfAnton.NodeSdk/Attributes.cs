namespace SonOfAnton.NodeSdk;

/// <summary>
/// Node tipini tanımlamak için kullanılır.
/// Bu attribute ile işaretlenmiş sınıflar, plugin scanner tarafından otomatik olarak algılanır.
/// </summary>
/// <example>
/// [NodeDefinition("math.add", "Toplama", Category = "Matematik", Icon = "plus", Color = "#94e2d5")]
/// public class AddNode : ICustomNodeExecutor { ... }
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class NodeDefinitionAttribute : Attribute
{
    /// <summary>Node tipi (benzersiz tanımlayıcı, örn: "math.add")</summary>
    public string NodeType { get; }

    /// <summary>Kullanıcıya gösterilecek ad</summary>
    public string DisplayName { get; }

    /// <summary>Kategori (varsayılan: "Plugin")</summary>
    public string Category { get; set; } = "Plugin";

    /// <summary>Lucide icon adı (varsayılan: "puzzle")</summary>
    public string Icon { get; set; } = "puzzle";

    /// <summary>Node rengi hex kodu</summary>
    public string Color { get; set; } = "#89dceb";

    /// <summary>Açıklama</summary>
    public string Description { get; set; } = "";

    public NodeDefinitionAttribute(string nodeType, string displayName)
    {
        NodeType = nodeType;
        DisplayName = displayName;
    }
}

/// <summary>
/// Node'a input pin tanımlar.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class NodeInputAttribute : Attribute
{
    public string Name { get; }
    public string DisplayName { get; }
    public string DataType { get; set; } = "any";
    public bool Multiple { get; set; }
    public bool Required { get; set; }

    public NodeInputAttribute(string name, string displayName)
    {
        Name = name;
        DisplayName = displayName;
    }
}

/// <summary>
/// Node'a output pin tanımlar.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class NodeOutputAttribute : Attribute
{
    public string Name { get; }
    public string DisplayName { get; }
    public string DataType { get; set; } = "any";

    public NodeOutputAttribute(string name, string displayName)
    {
        Name = name;
        DisplayName = displayName;
    }
}

/// <summary>
/// Node'a parametre tanımlar. UI'da düzenlenebilir ayarlar.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class NodeParameterAttribute : Attribute
{
    public string Name { get; }
    public string DisplayName { get; }
    public string ParamType { get; set; } = "string"; // string, number, boolean, select, code, json
    public object? DefaultValue { get; set; }
    public bool Required { get; set; }
    public string? Placeholder { get; set; }

    public NodeParameterAttribute(string name, string displayName)
    {
        Name = name;
        DisplayName = displayName;
    }
}
