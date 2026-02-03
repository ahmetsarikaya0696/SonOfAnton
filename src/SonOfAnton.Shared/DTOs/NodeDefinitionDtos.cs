namespace SonOfAnton.Shared.DTOs;

/// <summary>
/// Node tan�m� - UI'da palette i�in
/// </summary>
public class NodeDefinitionDto
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#89b4fa";
    public List<NodePortDto> Inputs { get; set; } = [];
    public List<NodePortDto> Outputs { get; set; } = [];
    public List<NodeParameterDto> Parameters { get; set; } = [];
}

/// <summary>
/// Port tan�m�
/// </summary>
public class NodePortDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "any";
    public bool Multiple { get; set; }
}

/// <summary>
/// Parametre tan�m�
/// </summary>
public class NodeParameterDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Type { get; set; } = "string"; // string, number, boolean, select, code, json
    public object? DefaultValue { get; set; }
    public bool Required { get; set; }
    public string? Placeholder { get; set; }
    public List<SelectOptionDto>? Options { get; set; }
}

/// <summary>
/// Select se�enekleri
/// </summary>
public class SelectOptionDto
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

