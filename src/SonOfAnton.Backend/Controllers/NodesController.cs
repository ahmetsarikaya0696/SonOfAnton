using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Models;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NodesController : ControllerBase
{
    /// <summary>
    /// Kullan�labilir t�m node tiplerini listele
    /// </summary>
    [HttpGet]
    public ActionResult<List<NodeDefinitionDto>> GetAll()
    {
        var nodes = NodeRegistry.GetAllNodes();
        
        var dtos = nodes.Select(n => new NodeDefinitionDto
        {
            Type = n.Type,
            Name = n.Name,
            Icon = n.Icon,
            Category = n.Category,
            Description = n.Description,
            Color = n.Color,
            Inputs = n.Inputs.Select(p => new NodePortDto
            {
                Name = p.Name,
                Type = p.Type,
                Multiple = p.Multiple
            }).ToList(),
            Outputs = n.Outputs.Select(p => new NodePortDto
            {
                Name = p.Name,
                Type = p.Type,
                Multiple = p.Multiple
            }).ToList(),
            Parameters = n.Parameters.Select(p => new NodeParameterDto
            {
                Name = p.Name,
                DisplayName = p.DisplayName,
                Type = p.Type,
                DefaultValue = p.DefaultValue,
                Required = p.Required,
                Placeholder = p.Placeholder,
                Options = p.Options?.Select(o => new SelectOptionDto
                {
                    Value = o.Value,
                    Label = o.Label
                }).ToList()
            }).ToList()
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Belirli bir node tipinin detay�n� getir
    /// </summary>
    [HttpGet("{type}")]
    public ActionResult<NodeDefinitionDto> Get(string type)
    {
        var node = NodeRegistry.GetNode(type);
        if (node == null)
            return NotFound();

        var dto = new NodeDefinitionDto
        {
            Type = node.Type,
            Name = node.Name,
            Icon = node.Icon,
            Category = node.Category,
            Description = node.Description,
            Color = node.Color,
            Inputs = node.Inputs.Select(p => new NodePortDto
            {
                Name = p.Name,
                Type = p.Type,
                Multiple = p.Multiple
            }).ToList(),
            Outputs = node.Outputs.Select(p => new NodePortDto
            {
                Name = p.Name,
                Type = p.Type,
                Multiple = p.Multiple
            }).ToList(),
            Parameters = node.Parameters.Select(p => new NodeParameterDto
            {
                Name = p.Name,
                DisplayName = p.DisplayName,
                Type = p.Type,
                DefaultValue = p.DefaultValue,
                Required = p.Required,
                Placeholder = p.Placeholder,
                Options = p.Options?.Select(o => new SelectOptionDto
                {
                    Value = o.Value,
                    Label = o.Label
                }).ToList()
            }).ToList()
        };

        return Ok(dto);
    }
}

