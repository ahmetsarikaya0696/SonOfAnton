using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Controllers;

/// <summary>
/// Community Marketplace API.
/// Tenant-scoped: her tenant yalnızca kendi marketplace'ini görür.
/// Node tanımlarını ve workflow template'lerini paylaşma, kurma ve değerlendirme.
/// </summary>
[ApiController]
[Route("api/marketplace")]
[Produces("application/json")]
public class MarketplaceController : ControllerBase
{
    private readonly MarketplaceService _marketplaceService;
    private readonly ILogger<MarketplaceController> _logger;

    public MarketplaceController(MarketplaceService marketplaceService, ILogger<MarketplaceController> logger)
    {
        _marketplaceService = marketplaceService;
        _logger = logger;
    }

    /// <summary>
    /// Marketplace'de arama yap.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(MarketplaceSearchResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MarketplaceSearchResultDto>> Search([FromQuery] MarketplaceSearchDto search)
    {
        var result = await _marketplaceService.SearchAsync(search);
        return Ok(result);
    }

    /// <summary>
    /// Marketplace öğesi detayını getir.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MarketplaceItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MarketplaceItemDto>> GetItem(Guid id)
    {
        var item = await _marketplaceService.GetItemAsync(id);
        if (item == null)
            return NotFound();
        return Ok(item);
    }

    /// <summary>
    /// Marketplace'e öğe yayınla.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(MarketplaceItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MarketplaceItemDto>> Publish([FromBody] PublishMarketplaceItemDto dto)
    {
        try
        {
            var result = await _marketplaceService.PublishAsync(dto);
            return CreatedAtAction(nameof(GetItem), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Marketplace öğesini kaldır.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unpublish(Guid id)
    {
        try
        {
            await _marketplaceService.UnpublishAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Marketplace öğesini kur.
    /// </summary>
    [HttpPost("{id:guid}/install")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Install(Guid id)
    {
        try
        {
            await _marketplaceService.InstallAsync(id);
            return Ok(new { message = "Kurulum başarılı." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Marketplace öğesi kurulumunu kaldır.
    /// </summary>
    [HttpDelete("{id:guid}/install")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Uninstall(Guid id)
    {
        try
        {
            await _marketplaceService.UninstallAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Marketplace öğesinin değerlendirmelerini getir.
    /// </summary>
    [HttpGet("{id:guid}/reviews")]
    [ProducesResponseType(typeof(List<MarketplaceReviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MarketplaceReviewDto>>> GetReviews(Guid id)
    {
        var reviews = await _marketplaceService.GetReviewsAsync(id);
        return Ok(reviews);
    }

    /// <summary>
    /// Marketplace öğesine değerlendirme ekle.
    /// </summary>
    [HttpPost("{id:guid}/reviews")]
    [ProducesResponseType(typeof(MarketplaceReviewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MarketplaceReviewDto>> AddReview(Guid id, [FromBody] CreateReviewDto dto)
    {
        try
        {
            var review = await _marketplaceService.AddReviewAsync(id, dto);
            return CreatedAtAction(nameof(GetReviews), new { id }, review);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Marketplace kategorilerini getir.
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> GetCategories()
    {
        var categories = await _marketplaceService.GetCategoriesAsync();
        return Ok(categories);
    }
}
