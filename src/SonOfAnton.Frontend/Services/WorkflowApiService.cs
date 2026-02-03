using System.Net.Http.Json;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Frontend.Services;

/// <summary>
/// Backend API ile ileti�im servisi
/// </summary>
public class WorkflowApiService
{
    private readonly HttpClient _httpClient;

    public WorkflowApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    #region Workflows

    public async Task<List<WorkflowDto>> GetWorkflowsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<WorkflowDto>>("api/workflows") ?? [];
    }

    public async Task<WorkflowDto?> GetWorkflowAsync(Guid id)
    {
        return await _httpClient.GetFromJsonAsync<WorkflowDto>($"api/workflows/{id}");
    }

    public async Task<WorkflowDto?> SaveWorkflowAsync(SaveWorkflowRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/workflows", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkflowDto>();
    }

    public async Task DeleteWorkflowAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/workflows/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<ExecutionDto?> ExecuteWorkflowAsync(Guid workflowId, Dictionary<string, object?>? initialData = null)
    {
        var request = new StartExecutionRequest 
        { 
            WorkflowId = workflowId,
            InitialData = initialData
        };
        var response = await _httpClient.PostAsJsonAsync($"api/workflows/{workflowId}/execute", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExecutionDto>();
    }

    #endregion

    #region Executions

    public async Task<List<ExecutionDto>> GetExecutionsAsync(Guid? workflowId = null)
    {
        var url = "api/executions";
        if (workflowId.HasValue)
        {
            url += $"?workflowId={workflowId}";
        }
        return await _httpClient.GetFromJsonAsync<List<ExecutionDto>>(url) ?? [];
    }

    public async Task<ExecutionDto?> GetExecutionAsync(Guid id)
    {
        return await _httpClient.GetFromJsonAsync<ExecutionDto>($"api/executions/{id}");
    }

    public async Task<List<LogEventDto>> GetExecutionLogsAsync(Guid executionId)
    {
        return await _httpClient.GetFromJsonAsync<List<LogEventDto>>($"api/executions/{executionId}/logs") ?? [];
    }

    public async Task CancelExecutionAsync(Guid id)
    {
        var response = await _httpClient.PostAsync($"api/workflows/execution/{id}/cancel", null);
        response.EnsureSuccessStatusCode();
    }

    #endregion

    #region Nodes

    public async Task<List<NodeDefinitionDto>> GetNodeDefinitionsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<NodeDefinitionDto>>("api/nodes") ?? [];
    }

    public async Task<NodeDefinitionDto?> GetNodeDefinitionAsync(string type)
    {
        return await _httpClient.GetFromJsonAsync<NodeDefinitionDto>($"api/nodes/{type}");
    }

    #endregion
}

