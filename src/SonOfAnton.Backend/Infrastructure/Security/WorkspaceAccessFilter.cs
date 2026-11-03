using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SonOfAnton.Backend.Data;
using SonOfAnton.Shared.Core.Identity;
using SonOfAnton.Shared.DTOs;
using SonOfAnton.Shared.Entities;
namespace SonOfAnton.Backend.Infrastructure.Security;

// A global permission is necessary but cannot grant write access to a read-only workspace.
public sealed class WorkspaceAccessFilter(SonOfAntonDbContext db, IUserContext user) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controller = context.RouteData.Values["controller"]?.ToString();
        var args = context.ActionArguments;
        Guid? Arg(string key) => args.TryGetValue(key, out var value) && value is Guid id ? id : null;
        if (controller == "Workspaces" && Arg("id") is Guid resource && !await db.Workspaces.AnyAsync(w => w.Id == resource))
        { context.Result = new NotFoundResult(); return; }
        if (HttpMethods.IsGet(context.HttpContext.Request.Method)) { await next(); return; }
        Guid? workspaceId = null, workflowId = null;
        var admin = controller == "Workspaces";
        if (admin) workspaceId = Arg("id");
        if (controller is "Workflows" or "Executions")
        {
            var executionId = Arg("executionId");
            if ((controller == "Executions" && Arg("id").HasValue) || context.ActionDescriptor.RouteValues["action"] == "StepExecution") executionId = Arg("id");
            if (executionId.HasValue)
            {
                var execution = await db.Executions.AsNoTracking().FirstOrDefaultAsync(e => e.Id == executionId);
                if (execution is null) { context.Result = new NotFoundResult(); return; }
                workflowId = execution.WorkflowId;
            }
            else workflowId = Arg("workflowId") ?? (controller == "Workflows" ? Arg("id") : null);
            if (args.TryGetValue("jobName", out var job) && job is string name && name.StartsWith("workflow_") && Guid.TryParse(name[9..], out var recurring)) workflowId = recurring;
            if (args.TryGetValue("request", out var body) && body is SaveWorkflowRequest save)
            {
                workflowId = save.Id; workspaceId = save.WorkspaceId;
                if (!workspaceId.HasValue && save.Id.HasValue) workspaceId = await db.Workflows.Where(w => w.Id == save.Id).Select(w => w.WorkspaceId).FirstOrDefaultAsync();
                if (!workspaceId.HasValue && Guid.TryParse(context.HttpContext.Request.Headers["X-Workspace-Id"].FirstOrDefault(), out var header)) workspaceId = header;
                workspaceId ??= await db.Workspaces.OrderByDescending(w => w.IsDefault).Select(w => (Guid?)w.Id).FirstOrDefaultAsync();
                if (workspaceId.HasValue && !await CanWrite(workspaceId.Value, false)) { context.Result = new ForbidResult(); return; }
            }
            if (workflowId.HasValue)
            {
                var workflow = await db.Workflows.AsNoTracking().FirstOrDefaultAsync(w => w.Id == workflowId);
                if (workflow is null) { context.Result = new NotFoundResult(); return; }
                workspaceId = workflow.WorkspaceId;
            }
        }
        if (controller == "Folders")
        {
            if (Arg("id") is Guid folderId)
            {
                var folder = await db.Folders.AsNoTracking().FirstOrDefaultAsync(f => f.Id == folderId);
                if (folder is null) { context.Result = new NotFoundResult(); return; }
                workspaceId = folder.WorkspaceId;
            }
            else if (args.TryGetValue("request", out var body) && body is CreateFolderRequest folder) workspaceId = folder.WorkspaceId;
            if (!workspaceId.HasValue && Guid.TryParse(context.HttpContext.Request.Headers["X-Workspace-Id"].FirstOrDefault(), out var header)) workspaceId = header;
        }
        if (workspaceId.HasValue && !await CanWrite(workspaceId.Value, admin)) { context.Result = new ForbidResult(); return; }
        await next();
    }

    // Enum values are stored as text: never use numeric ordering in SQL for roles.
    private Task<bool> CanWrite(Guid workspaceId, bool admin) => db.Workspaces.AnyAsync(w => w.Id == workspaceId &&
        (w.OwnerUserId == user.CurrentUserId || w.Members.Any(m => m.UserId == user.CurrentUserId &&
            (m.Role == WorkspaceMemberRole.Admin || m.Role == WorkspaceMemberRole.Owner || !admin && m.Role == WorkspaceMemberRole.Editor))));
}
