using Hangfire;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Backend.Engine;
using SonOfAnton.Backend.Execution;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.Entities;
using SonOfAnton.Shared.Enums;
using System.Text.Json;

namespace SonOfAnton.Backend.Services;

public class CronValidationResult
{
    public bool IsValid { get; init; }
    public string? Error { get; init; }
    public string? Description { get; init; }
    public DateTime? NextOccurrence { get; init; }
    public static CronValidationResult Valid(string description, DateTime? next) => new() { IsValid = true, Description = description, NextOccurrence = next };
    public static CronValidationResult Invalid(string error) => new() { Error = error };
}

// One persisted execution and one queue entry for all supported API entry points.
public sealed class WorkflowJobService(SonOfAntonDbContext db, IUserContext user,
    IBackgroundJobClient jobs, WorkflowRunner runner, PauseTokenRegistry pauses)
{
    public async Task<ExecutionEntity> StartAsync(Guid workflowId, object? input = null, TimeSpan? delay = null, bool stepMode = false)
    {
        var workflow = await db.Workflows.SingleAsync(w => w.Id == workflowId);
        var tenant = user.TenantId ?? throw new UnauthorizedAccessException();
        var userId = user.CurrentUserId;
        if (!await CanExecuteAsync(workflow, tenant, userId)) throw new UnauthorizedAccessException();
        var execution = new ExecutionEntity { WorkflowId = workflowId,
            Memory = input is null ? null : JsonSerializer.SerializeToDocument(input) };
        db.Executions.Add(execution);
        await db.SaveChangesAsync();
        if (stepMode) { var token = pauses.GetOrCreate(execution.Id); token.IsStepMode = true; token.Pause(); }
        try
        {
            execution.HangfireJobId = delay.HasValue
                ? jobs.Schedule<WorkflowJobService>(s => s.ExecuteAsync(execution.Id, tenant, userId, CancellationToken.None), delay.Value)
                : jobs.Enqueue<WorkflowJobService>(s => s.ExecuteAsync(execution.Id, tenant, userId, CancellationToken.None));
            await db.SaveChangesAsync();
            return execution;
        }
        catch
        {
            execution.Status = ExecutionStatus.Failed;
            execution.ErrorMessage = "Execution could not be queued.";
            execution.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            throw;
        }
    }

    [AutomaticRetry(Attempts = 0)]
    [Queue("workflows")]
    public async Task ExecuteAsync(Guid executionId, string tenantId, Guid userId, CancellationToken ct)
    {
        db.SetJobAccess(tenantId, userId);
        var execution = await db.Executions.Include(e => e.Workflow).SingleOrDefaultAsync(e => e.Id == executionId, ct);
        if (execution is null || execution.Status != ExecutionStatus.Queued) return;
        if (!await CanExecuteAsync(execution.Workflow, tenantId, userId))
        {
            execution.Status = ExecutionStatus.Failed;
            execution.ErrorMessage = "Execution permission was revoked.";
            execution.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }
        await runner.RunAsync(executionId, tenantId, userId, ct);
    }

    private async Task<bool> CanExecuteAsync(WorkflowEntity workflow, string tenantId, Guid userId) =>
        workflow.TenantId == tenantId &&
        await db.Users.AnyAsync(u => u.Id == userId && u.TenantId == tenantId && u.IsActive && u.Tenant!.IsActive) &&
        (workflow.WorkspaceId is null ? workflow.CreatedByUserId == userId :
            await db.Workspaces.AnyAsync(w => w.Id == workflow.WorkspaceId && (w.OwnerUserId == userId ||
                w.Members.Any(m => m.UserId == userId && m.Role != WorkspaceMemberRole.Viewer))));

    public CronValidationResult AddRecurringWorkflow(Guid workflowId, string cronExpression, string? jobName = null)
    {
        var validation = ValidateCronExpression(cronExpression);
        if (!validation.IsValid) return validation;
        var tenant = user.TenantId ?? throw new UnauthorizedAccessException();
        var userId = user.CurrentUserId;
        RecurringJob.AddOrUpdate<WorkflowJobService>($"workflow_{workflowId}",
            s => s.StartRecurringAsync(workflowId, tenant, userId, CancellationToken.None), cronExpression,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        return validation;
    }

    // Generate the execution ID when the recurring invocation actually starts.
    [AutomaticRetry(Attempts = 0)]
    [Queue("workflows")]
    public async Task StartRecurringAsync(Guid workflowId, string tenantId, Guid userId, CancellationToken ct)
    {
        db.SetJobAccess(tenantId, userId);
        var workflow = await db.Workflows.SingleOrDefaultAsync(w => w.Id == workflowId, ct);
        if (workflow is null || !await CanExecuteAsync(workflow, tenantId, userId))
        { RemoveRecurringWorkflow($"workflow_{workflowId}"); return; }
        var execution = new ExecutionEntity { WorkflowId = workflowId };
        db.Executions.Add(execution);
        await db.SaveChangesAsync(ct);
        await ExecuteAsync(execution.Id, tenantId, userId, ct);
    }

    public void RemoveRecurringWorkflow(string name) => RecurringJob.RemoveIfExists(name);
    public void PauseRecurringWorkflow(string name) => RemoveRecurringWorkflow(name);
    public static CronValidationResult ValidateCronExpression(string cronExpression)
    {
        try
        {
            var cron = Cronos.CronExpression.Parse(cronExpression, Cronos.CronFormat.Standard);
            return CronValidationResult.Valid(cronExpression, cron.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc));
        }
        catch (Exception ex) when (ex is ArgumentException or Cronos.CronFormatException)
        { return CronValidationResult.Invalid("Invalid five-field UTC cron expression."); }
    }
}
