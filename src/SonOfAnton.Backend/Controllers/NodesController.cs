using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Models;

namespace SonOfAnton.Backend.Controllers;

/// <summary>
/// Node tanımları kataloğu.
/// 
/// Node'lar, workflow'ların yapı taşlarıdır. Her node belirli bir işlevi yerine getirir.
/// Bu controller, kullanılabilir node tiplerini ve özelliklerini sunar.
/// 
/// <para><b>Node Kategorileri:</b></para>
/// <list type="bullet">
///   <item><description><b>Tetikleyiciler</b> - Workflow'u başlatan node'lar (manual, webhook, schedule)</description></item>
///   <item><description><b>Dönüşüm</b> - Veriyi şekillendiren node'lar (set, filter, template, aggregate)</description></item>
///   <item><description><b>Mantık</b> - Akış kontrolü node'ları (if, switch, merge, delay, loop)</description></item>
///   <item><description><b>HTTP</b> - Dış API'lerle iletişim (request)</description></item>
///   <item><description><b>İletişim</b> - Bildirim gönderme (email, slack)</description></item>
///   <item><description><b>Veri</b> - Veritabanı işlemleri (database)</description></item>
///   <item><description><b>Yapay Zeka</b> - LLM ve AI işlemleri</description></item>
///   <item><description><b>Vector DB</b> - Embedding ve semantik arama</description></item>
/// </list>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class NodesController : ControllerBase
{
    /// <summary>
    /// Kullanılabilir tüm node tiplerini listele.
    /// </summary>
    /// <returns>Node tanımları listesi (tip, portlar, parametreler dahil).</returns>
    /// <response code="200">Başarılı - Node kataloğu döner.</response>
    /// <remarks>
    /// Frontend'deki node paleti bu endpoint'ten beslenir.
    /// Her node tanımı şunları içerir:
    /// - **Type**: Benzersiz tip adı (ör: "logic.if", "transform.set")
    /// - **Inputs/Outputs**: Bağlantı noktaları (portlar)
    /// - **Parameters**: Kullanıcının dolduracağı ayarlar
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(List<NodeDefinitionDto>), StatusCodes.Status200OK)]
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
    /// Belirli bir node tipinin detayını getir.
    /// </summary>
    /// <param name="type">Node tipi (ör: "logic.if", "transform.set", "trigger.manual").</param>
    /// <returns>Node tanımı detayları.</returns>
    /// <response code="200">Başarılı - Node tanımı bulundu.</response>
    /// <response code="404">Node tipi bulunamadı.</response>
    [HttpGet("{type}")]
    [ProducesResponseType(typeof(NodeDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

