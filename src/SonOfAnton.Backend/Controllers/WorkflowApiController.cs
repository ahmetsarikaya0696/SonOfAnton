using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SonOfAnton.Backend.Services;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Backend.Controllers;

[ApiController]
[Route("api/workflow-endpoints")]
[Authorize(Policy = Permissions.ApiEndpointsManage)]
public class WorkflowApiController : ControllerBase
{
    private readonly WorkflowApiService _apiService;

    public WorkflowApiController(WorkflowApiService apiService)
    {
        _apiService = apiService;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.ApiEndpointsRead)]
    public async Task<IActionResult> GetAll()
    {
        var endpoints = await _apiService.GetAllEndpointsAsync();
        
        // Populate FullUrl
        var baseUrl = $"{Request.Scheme}://{Request.Host}/api/published";
        foreach (var endpoint in endpoints)
        {
            endpoint.FullUrl = $"{baseUrl}/{endpoint.Route.TrimStart('/')}";
        }
        
        return Ok(endpoints);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.ApiEndpointsRead)]
    public async Task<IActionResult> Get(Guid id)
    {
        var endpoint = await _apiService.GetEndpointAsync(id);
        if (endpoint == null) return NotFound();

        endpoint.FullUrl = $"{Request.Scheme}://{Request.Host}/api/published/{endpoint.Route.TrimStart('/')}";
        return Ok(endpoint);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowApiEndpointRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Route))
                return BadRequest("Rota zorunludur.");
                
            request.Route = request.Route.TrimStart('/');

            var endpoint = await _apiService.CreateEndpointAsync(request);
            endpoint.FullUrl = $"{Request.Scheme}://{Request.Host}/api/published/{endpoint.Route}";
            
            return CreatedAtAction(nameof(Get), new { id = endpoint.Id }, endpoint);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkflowApiEndpointRequest request)
    {
        try
        {
            if (request.Route != null)
                request.Route = request.Route.TrimStart('/');

            var endpoint = await _apiService.UpdateEndpointAsync(id, request);
            if (endpoint == null) return NotFound();

            endpoint.FullUrl = $"{Request.Scheme}://{Request.Host}/api/published/{endpoint.Route}";
            return Ok(endpoint);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _apiService.DeleteEndpointAsync(id);
        if (!result) return NotFound();

        return NoContent();
    }
}
