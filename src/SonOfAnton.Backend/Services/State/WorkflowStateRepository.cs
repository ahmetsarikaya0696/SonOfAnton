using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Entities;

namespace SonOfAnton.Backend.Services.State;

public class WorkflowStateRepository : IStateRepository
{
    private readonly SonOfAntonDbContext _dbContext;

    public WorkflowStateRepository(SonOfAntonDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ExecutionCheckpointEntity> SaveCheckpointAsync(Guid executionId, Guid nodeId, JsonDocument? variables, JsonDocument? executionMemory, string status, int retryCount = 0)
    {
        var existing = await _dbContext.ExecutionCheckpoints
            .FirstOrDefaultAsync(c => c.ExecutionId == executionId && c.NodeId == nodeId);

        if (existing != null)
        {
            existing.Status = status;
            existing.VariablesJson = variables;
            existing.ExecutionMemory = executionMemory;
            existing.Timestamp = DateTime.UtcNow;
            existing.RetryCount = retryCount;
            await _dbContext.SaveChangesAsync();
            return existing;
        }

        var newCheckpoint = new ExecutionCheckpointEntity
        {
            ExecutionId = executionId,
            NodeId = nodeId,
            Status = status,
            VariablesJson = variables,
            ExecutionMemory = executionMemory,
            RetryCount = retryCount,
            Timestamp = DateTime.UtcNow
        };

        _dbContext.ExecutionCheckpoints.Add(newCheckpoint);
        await _dbContext.SaveChangesAsync();

        return newCheckpoint;
    }

    public async Task<ExecutionCheckpointEntity?> GetLatestCheckpointAsync(Guid executionId)
    {
        return await _dbContext.ExecutionCheckpoints
            .Where(c => c.ExecutionId == executionId)
            .OrderByDescending(c => c.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task<ExecutionCheckpointEntity?> GetCheckpointAsync(Guid executionId, Guid nodeId)
    {
        return await _dbContext.ExecutionCheckpoints
            .Where(c => c.ExecutionId == executionId && c.NodeId == nodeId)
            .OrderByDescending(c => c.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task ClearCheckpointsAsync(Guid executionId)
    {
        var checkpoints = await _dbContext.ExecutionCheckpoints
            .Where(c => c.ExecutionId == executionId)
            .ToListAsync();
            
        if (checkpoints.Any())
        {
            _dbContext.ExecutionCheckpoints.RemoveRange(checkpoints);
            await _dbContext.SaveChangesAsync();
        }
    }
}
