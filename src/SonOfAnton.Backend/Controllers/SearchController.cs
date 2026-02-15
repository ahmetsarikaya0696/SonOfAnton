using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.Services.Search;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly FullTextSearchService _searchService;

    public SearchController(FullTextSearchService searchService)
    {
        _searchService = searchService;
    }

    /// <summary>
    /// Global arama — workflow, log ve daha fazlasında metin arar
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SearchResultDto<object>>> Search(
        [FromQuery] string q,
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query parametresi gerekli" });

        var request = new SearchRequest
        {
            Query = q,
            Types = type?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? [],
            Page = page,
            PageSize = Math.Min(pageSize, 100)
        };

        var results = await _searchService.SearchAsync(request, ct);
        return Ok(results);
    }
}
