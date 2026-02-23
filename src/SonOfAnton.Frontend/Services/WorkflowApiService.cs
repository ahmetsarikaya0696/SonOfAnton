using System.Net.Http.Json;
using SonOfAnton.Shared.DTOs;

namespace SonOfAnton.Frontend.Services;

/// <summary>
/// Backend API ile iletişim servisi
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

    public async Task<List<WorkflowRevisionDto>> GetRevisionsAsync(Guid workflowId)
    {
        return await _httpClient.GetFromJsonAsync<List<WorkflowRevisionDto>>($"api/workflows/{workflowId}/revisions") ?? [];
    }

    public async Task<WorkflowRevisionDto?> GetRevisionAsync(Guid workflowId, int version)
    {
        return await _httpClient.GetFromJsonAsync<WorkflowRevisionDto>($"api/workflows/{workflowId}/revisions/{version}");
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

    public async Task<ExecutionDto?> TestRunWorkflowAsync(Guid workflowId, Dictionary<string, object?>? initialData = null)
    {
        var request = new StartExecutionRequest 
        { 
            WorkflowId = workflowId,
            InitialData = initialData
        };
        var response = await _httpClient.PostAsJsonAsync($"api/workflows/{workflowId}/test-run", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExecutionDto>();
    }

    public async Task<ExecuteSingleNodeResponse?> ExecuteSingleNodeAsync(Guid workflowId, ExecuteSingleNodeRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/workflows/{workflowId}/execute-node", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExecuteSingleNodeResponse>();
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

    #region Folders

    public async Task<List<FolderDto>> GetFoldersAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<FolderDto>>("api/folders") ?? [];
    }

    public async Task<List<FolderTreeDto>> GetFolderTreeAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<FolderTreeDto>>("api/folders/tree") ?? [];
    }

    public async Task<FolderDto?> CreateFolderAsync(CreateFolderRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/folders", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FolderDto>();
    }

    public async Task DeleteFolderAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/folders/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task MoveWorkflowToFolderAsync(Guid workflowId, Guid? folderId)
    {
        var response = await _httpClient.PatchAsJsonAsync(
            $"api/workflows/{workflowId}/move",
            new MoveWorkflowRequest { FolderId = folderId });
        response.EnsureSuccessStatusCode();
    }

    #endregion

    #region Statistics

    public async Task<DashboardStatsDto?> GetDashboardStatsAsync()
    {
        return await _httpClient.GetFromJsonAsync<DashboardStatsDto>("api/workflows/stats");
    }

    public async Task<WorkflowStatsDto?> GetWorkflowStatsAsync(Guid workflowId, int days = 30)
    {
        return await _httpClient.GetFromJsonAsync<WorkflowStatsDto>(
            $"api/workflows/{workflowId}/stats?days={days}");
    }

    #endregion

    #region Pause / Resume / Snapshots

    public async Task PauseExecutionAsync(Guid executionId)
    {
        var response = await _httpClient.PostAsync($"api/workflows/execution/{executionId}/pause", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResumeExecutionAsync(Guid executionId)
    {
        var response = await _httpClient.PostAsync($"api/workflows/execution/{executionId}/resume", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResumeExecutionFromNodeAsync(Guid executionId, Guid nodeId)
    {
        var response = await _httpClient.PostAsync($"api/workflows/execution/{executionId}/resume-from-node/{nodeId}", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task StepExecutionAsync(Guid executionId)
    {
        var response = await _httpClient.PostAsync($"api/workflows/execution/{executionId}/step", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<SnapshotDto?> TakeSnapshotAsync(Guid executionId, string? label = null)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/workflows/execution/{executionId}/snapshot",
            new CreateSnapshotRequest { Label = label });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SnapshotDto>();
    }

    public async Task<List<SnapshotDto>> GetSnapshotsAsync(Guid executionId)
    {
        return await _httpClient.GetFromJsonAsync<List<SnapshotDto>>(
            $"api/workflows/execution/{executionId}/snapshots") ?? [];
    }

    public async Task<ExecutionDto?> RestoreFromSnapshotAsync(Guid executionId, Guid snapshotId)
    {
        var response = await _httpClient.PostAsync(
            $"api/workflows/execution/{executionId}/snapshot/{snapshotId}/restore", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExecutionDto>();
    }

    #endregion

    #region Chat & YAML

    public async Task<WorkflowChatResponse?> SendChatMessageAsync(WorkflowChatRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/workflow/chat", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkflowChatResponse>();
    }

    public async Task<PlanArtifactDto?> ExportYamlAsync(WorkflowGraphDto graph)
    {
        var response = await _httpClient.PostAsJsonAsync("api/workflow/chat/export-yaml", graph);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PlanArtifactDto>();
    }

    public async Task<YamlValidationResult?> ImportYamlAsync(string yamlContent)
    {
        var response = await _httpClient.PostAsJsonAsync("api/workflow/chat/import-yaml",
            new YamlImportRequest { YamlContent = yamlContent });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<YamlValidationResult>();
    }

    public async Task<List<PlanArtifactDto>> GetArtifactsAsync(Guid workflowId, string? type = null)
    {
        var url = $"api/workflows/{workflowId}/artifacts";
        if (type != null) url += $"?type={type}";
        return await _httpClient.GetFromJsonAsync<List<PlanArtifactDto>>(url) ?? [];
    }

    #endregion

    #region Search

    public async Task<SearchResultDto<object>?> SearchAsync(string query, string? type = null, int page = 1)
    {
        var url = $"api/search?q={Uri.EscapeDataString(query)}&page={page}";
        if (type != null) url += $"&type={type}";
        return await _httpClient.GetFromJsonAsync<SearchResultDto<object>>(url);
    }

    #endregion

    #region A2A

    public async Task<List<AgentCardDto>> GetAgentCardsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<AgentCardDto>>("api/a2a/agents") ?? [];
    }

    public async Task<A2ATaskDto?> SendA2ATaskAsync(A2ATaskRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/a2a/tasks/send", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<A2ATaskDto>();
    }

    public async Task<A2ATaskDto?> GetA2ATaskAsync(Guid taskId)
    {
        return await _httpClient.GetFromJsonAsync<A2ATaskDto>($"api/a2a/tasks/{taskId}");
    }

    #endregion

    #region Published API Endpoints

    public async Task<List<WorkflowApiEndpointDto>> GetPublishedEndpointsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<WorkflowApiEndpointDto>>("api/workflow-endpoints") ?? new List<WorkflowApiEndpointDto>();
    }

    public async Task<WorkflowApiEndpointDto?> CreatePublishedEndpointAsync(CreateWorkflowApiEndpointRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/workflow-endpoints", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkflowApiEndpointDto>();
    }

    public async Task<WorkflowApiEndpointDto?> UpdatePublishedEndpointAsync(Guid id, UpdateWorkflowApiEndpointRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/workflow-endpoints/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkflowApiEndpointDto>();
    }

    public async Task DeletePublishedEndpointAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/workflow-endpoints/{id}");
        response.EnsureSuccessStatusCode();
    }

    #endregion

    #region Webhooks

    public async Task<List<WebhookDto>> GetWebhooksAsync(Guid workflowId)
    {
        return await _httpClient.GetFromJsonAsync<List<WebhookDto>>($"api/webhook/list/{workflowId}") ?? new List<WebhookDto>();
    }

    public async Task<WebhookDto?> CreateWebhookAsync(CreateWebhookRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/webhook/create", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WebhookDto>();
    }

    public async Task DeleteWebhookAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/webhook/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> ToggleWebhookAsync(Guid id)
    {
        var response = await _httpClient.PatchAsync($"api/webhook/{id}/toggle", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ToggleWebhookResponse>();
        return result?.IsActive ?? false;
    }

    #endregion

    #region Data Stores

    public async Task<List<DataStoreDto>> GetDataStoresAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<DataStoreDto>>("api/datastores") ?? [];
    }

    public async Task<DataStoreDto?> GetDataStoreAsync(Guid id)
    {
        return await _httpClient.GetFromJsonAsync<DataStoreDto>($"api/datastores/{id}");
    }

    public async Task<DataStoreDto?> CreateDataStoreAsync(CreateDataStoreRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/datastores", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DataStoreDto>();
    }

    public async Task DeleteDataStoreAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/datastores/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<DataStoreEntryDto>> GetDataStoreEntriesAsync(Guid storeId)
    {
        return await _httpClient.GetFromJsonAsync<List<DataStoreEntryDto>>($"api/datastores/{storeId}/entries") ?? [];
    }

    public async Task<DataStoreEntryDto?> SetDataStoreEntryAsync(Guid storeId, SetDataStoreEntryRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/datastores/{storeId}/entries", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DataStoreEntryDto>();
    }

    public async Task DeleteDataStoreEntryAsync(Guid storeId, string key)
    {
        var response = await _httpClient.DeleteAsync($"api/datastores/{storeId}/entries/{Uri.EscapeDataString(key)}");
        response.EnsureSuccessStatusCode();
    }

    #endregion
}

