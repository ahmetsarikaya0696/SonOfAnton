using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Models;

namespace SonOfAnton.Backend.Controllers;

/// <summary>
/// Node tanımları kataloğu.
/// Built-in, custom ve plugin node tanımlarını birleştirir.
/// Custom node'lar tenant-scoped olarak DB'den çekilir.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class NodesController : ControllerBase
{
    private readonly HashSet<string> _supported;
    public NodesController(IEnumerable<SonOfAnton.Backend.Engine.INodeHandler> handlers)
        => _supported = handlers.Select(h => h.NodeType).ToHashSet(StringComparer.Ordinal);

    [HttpGet]
    public ActionResult<List<NodeDefinitionDto>> GetAll()
        => Ok(NodeRegistry.GetAllNodes().Where(n => _supported.Contains(n.Type)).Select(MapToDto).ToList());

    [HttpGet("{type}")]
    public ActionResult<NodeDefinitionDto> Get(string type)
    {
        var node = NodeRegistry.GetNode(type);
        return node is null || !_supported.Contains(type) ? NotFound() : Ok(MapToDto(node));
    }

    private static NodeDefinitionDto MapToDto(NodeDefinition n) => new()
    {
        Type = n.Type,
        Name = n.Name,
        Icon = n.Icon,
        Category = n.Category,
        Description = n.Description,
        Color = n.Color,
        Inputs = n.Inputs.Select(p => new NodePortDto { Name = p.Name, Type = p.Type, Multiple = p.Multiple }).ToList(),
        Outputs = n.Outputs.Select(p => new NodePortDto { Name = p.Name, Type = p.Type, Multiple = p.Multiple }).ToList(),
        Parameters = n.Parameters.Select(p => new NodeParameterDto
        {
            Name = p.Name, DisplayName = p.DisplayName, Type = p.Type,
            DefaultValue = p.DefaultValue, Required = p.Required, Placeholder = p.Placeholder,
            Options = p.Options?.Select(o => new SelectOptionDto { Value = o.Value, Label = o.Label }).ToList()
        }).ToList()
    };
}
