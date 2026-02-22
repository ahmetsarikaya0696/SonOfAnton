using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;

namespace SonOfAnton.Backend.Services;

public class WorkflowApiService
{
    private readonly SonOfAntonDbContext _dbContext;
    private readonly IUserContext _userContext;

    public WorkflowApiService(SonOfAntonDbContext dbContext, IUserContext userContext)
    {
        _dbContext = dbContext;
        _userContext = userContext;
    }

    public async Task<List<WorkflowApiEndpointDto>> GetAllEndpointsAsync()
    {
        var tenantId = _userContext.TenantId;
        var endpoints = await _dbContext.WorkflowApiEndpoints
            .Include(e => e.Workflow)
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        return endpoints.Select(MapToDto).ToList();
    }

    public async Task<WorkflowApiEndpointDto?> GetEndpointAsync(Guid id)
    {
        var tenantId = _userContext.TenantId;
        var endpoint = await _dbContext.WorkflowApiEndpoints
            .Include(e => e.Workflow)
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);

        return endpoint != null ? MapToDto(endpoint) : null;
    }

    public async Task<WorkflowApiEndpointDto> CreateEndpointAsync(CreateWorkflowApiEndpointRequest request)
    {
        var tenantId = _userContext.TenantId;

        // Route unique check
        var existing = await _dbContext.WorkflowApiEndpoints
            .AnyAsync(e => e.Route == request.Route && e.HttpMethod == request.HttpMethod && e.TenantId == tenantId);

        if (existing)
        {
            throw new InvalidOperationException($"'{request.HttpMethod} {request.Route}' rotası zaten kullanımda.");
        }

        var entity = new WorkflowApiEndpointEntity
        {
            WorkflowId = request.WorkflowId,
            Route = request.Route,
            HttpMethod = request.HttpMethod.ToUpper(),
            Description = request.Description,
            InputSchema = request.InputSchema != null ? JsonSerializer.SerializeToDocument(request.InputSchema) : null,
            OutputTemplate = request.OutputTemplate,
            ResponseContentType = request.ResponseContentType,
            RequireApiKey = request.RequireApiKey,
            WaitForResult = request.WaitForResult,
            WaitTimeoutSeconds = request.WaitTimeoutSeconds,
            RateLimitPerMinute = request.RateLimitPerMinute,
            IsActive = true,
            TenantId = tenantId
        };

        _dbContext.WorkflowApiEndpoints.Add(entity);
        await _dbContext.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<WorkflowApiEndpointDto?> UpdateEndpointAsync(Guid id, UpdateWorkflowApiEndpointRequest request)
    {
        var tenantId = _userContext.TenantId;
        var endpoint = await _dbContext.WorkflowApiEndpoints
            .Include(e => e.Workflow)
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);

        if (endpoint == null) return null;

        if (request.Route != null && request.Route != endpoint.Route)
        {
            var existing = await _dbContext.WorkflowApiEndpoints
                .AnyAsync(e => e.Route == request.Route && e.HttpMethod == (request.HttpMethod ?? endpoint.HttpMethod) && e.Id != id && e.TenantId == tenantId);
            
            if (existing) throw new InvalidOperationException("Rota zaten kullanımda.");
            endpoint.Route = request.Route;
        }

        if (request.HttpMethod != null) endpoint.HttpMethod = request.HttpMethod.ToUpper();
        if (request.Description != null) endpoint.Description = request.Description;
        if (request.InputSchema != null) endpoint.InputSchema = JsonSerializer.SerializeToDocument(request.InputSchema);
        if (request.OutputTemplate != null) endpoint.OutputTemplate = request.OutputTemplate;
        if (request.ResponseContentType != null) endpoint.ResponseContentType = request.ResponseContentType;
        if (request.RequireApiKey.HasValue) endpoint.RequireApiKey = request.RequireApiKey.Value;
        if (request.WaitForResult.HasValue) endpoint.WaitForResult = request.WaitForResult.Value;
        if (request.WaitTimeoutSeconds.HasValue) endpoint.WaitTimeoutSeconds = request.WaitTimeoutSeconds.Value;
        if (request.RateLimitPerMinute.HasValue) endpoint.RateLimitPerMinute = request.RateLimitPerMinute.Value;
        if (request.IsActive.HasValue) endpoint.IsActive = request.IsActive.Value;

        endpoint.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return MapToDto(endpoint);
    }

    public async Task<bool> DeleteEndpointAsync(Guid id)
    {
        var tenantId = _userContext.TenantId;
        var endpoint = await _dbContext.WorkflowApiEndpoints
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenantId);

        if (endpoint == null) return false;

        _dbContext.WorkflowApiEndpoints.Remove(endpoint);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    private static WorkflowApiEndpointDto MapToDto(WorkflowApiEndpointEntity e)
    {
        return new WorkflowApiEndpointDto
        {
            Id = e.Id,
            WorkflowId = e.WorkflowId,
            WorkflowName = e.Workflow?.Name,
            Route = e.Route,
            HttpMethod = e.HttpMethod,
            Description = e.Description,
            InputSchema = e.InputSchema != null ? JsonSerializer.Deserialize<object>(e.InputSchema) : null,
            OutputTemplate = e.OutputTemplate,
            ResponseContentType = e.ResponseContentType,
            RequireApiKey = e.RequireApiKey,
            WaitForResult = e.WaitForResult,
            WaitTimeoutSeconds = e.WaitTimeoutSeconds,
            RateLimitPerMinute = e.RateLimitPerMinute,
            IsActive = e.IsActive,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        };
    }
}
