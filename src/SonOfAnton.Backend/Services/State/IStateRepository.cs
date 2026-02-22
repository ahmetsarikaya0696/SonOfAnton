using System.Text.Json;
using SonOfAnton.Shared.Entities;

namespace SonOfAnton.Backend.Services.State;

public interface IStateRepository
{
    Task<ExecutionCheckpointEntity> SaveCheckpointAsync(Guid executionId, Guid nodeId, JsonDocument? variables, JsonDocument? executionMemory, string status, int retryCount = 0);
    Task<ExecutionCheckpointEntity?> GetLatestCheckpointAsync(Guid executionId);
    Task<ExecutionCheckpointEntity?> GetCheckpointAsync(Guid executionId, Guid nodeId);
    Task ClearCheckpointsAsync(Guid executionId);
}
