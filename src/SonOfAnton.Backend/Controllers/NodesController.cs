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
    private readonly CustomNodeService _customNodeService;
    private readonly IUserContext _userContext;
    private readonly PluginLoaderService _pluginLoader;

    public NodesController(
        CustomNodeService customNodeService,
        IUserContext userContext,
        PluginLoaderService pluginLoader)
    {
        _customNodeService = customNodeService;
        _userContext = userContext;
        _pluginLoader = pluginLoader;
    }

    /// <summary>
    /// Kullanılabilir tüm node tiplerini listele (built-in + custom + plugin).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<NodeDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NodeDefinitionDto>>> GetAll()
    {
        var builtInNodes = NodeRegistry.GetAllNodes();
        var tenantId = _userContext.TenantId ?? "default";
        var customNodes = await _customNodeService.GetAsNodeDefinitionsAsync(tenantId);
        var pluginNodes = _pluginLoader.GetPluginNodes();

        var allNodes = builtInNodes
            .Concat(customNodes)
            .Concat(pluginNodes)
            .ToList();

        return Ok(allNodes.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Belirli bir node tipinin detayını getir.
    /// </summary>
    [HttpGet("{type}")]
    [ProducesResponseType(typeof(NodeDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NodeDefinitionDto>> Get(string type)
    {
        var node = NodeRegistry.GetNode(type);

        if (node == null)
        {
            var tenantId = _userContext.TenantId ?? "default";
            var customNodes = await _customNodeService.GetAsNodeDefinitionsAsync(tenantId);
            node = customNodes.FirstOrDefault(n => n.Type == type);
        }

        if (node == null)
            node = _pluginLoader.GetPluginNodes().FirstOrDefault(n => n.Type == type);

        if (node == null)
            return NotFound();

        return Ok(MapToDto(node));
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
