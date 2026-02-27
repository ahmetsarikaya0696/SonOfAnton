namespace SonOfAnton.Shared.Core.Identity;

/// <summary>
/// Uygulamadaki tüm granüler izin sabitleri.
/// Roller yerine bu izinler üzerinden yetkilendirme yapılır.
/// Her izin, tek bir eylemi temsil eder.
/// </summary>
public static class Permissions
{
    /// <summary>Claim tipi — ClaimsPrincipal'a eklenen permission claim'lerinin anahtarı</summary>
    public const string ClaimType = "permission";

    // ─── Workflow İzinleri ───────────────────────────────────────
    public const string WorkflowsRead = "Workflows.Read";
    public const string WorkflowsCreate = "Workflows.Create";
    public const string WorkflowsEdit = "Workflows.Edit";
    public const string WorkflowsDelete = "Workflows.Delete";
    public const string WorkflowsExecute = "Workflows.Execute";

    // ─── Execution İzinleri ─────────────────────────────────────
    public const string ExecutionsRead = "Executions.Read";
    public const string ExecutionsCancel = "Executions.Cancel";
    public const string ExecutionsPause = "Executions.Pause";

    // ─── Kullanıcı Yönetimi ─────────────────────────────────────
    public const string UsersRead = "Users.Read";
    public const string UsersManage = "Users.Manage";

    // ─── Sistem Yönetimi ────────────────────────────────────────
    public const string SystemAdmin = "System.Admin";
    public const string TenantsManage = "Tenants.Manage";

    // ─── Webhook ────────────────────────────────────────────────
    public const string WebhooksRead = "Webhooks.Read";
    public const string WebhooksManage = "Webhooks.Manage";

    // ─── Folders ────────────────────────────────────────────────
    public const string FoldersRead = "Folders.Read";
    public const string FoldersManage = "Folders.Manage";

    // ─── Agents ─────────────────────────────────────────────────
    public const string AgentsRead = "Agents.Read";
    public const string AgentsManage = "Agents.Manage";

    // ─── API Keys ────────────────────────────────────────────────
    public const string ApiKeysRead = "ApiKeys.Read";
    public const string ApiKeysManage = "ApiKeys.Manage";

    // ─── API Endpoints ──────────────────────────────────────────
    public const string ApiEndpointsRead = "ApiEndpoints.Read";
    public const string ApiEndpointsManage = "ApiEndpoints.Manage";

    // ─── Search ─────────────────────────────────────────────────
    public const string SearchExecute = "Search.Execute";

    // ─── Chat ───────────────────────────────────────────────────
    public const string ChatUse = "Chat.Use";

    // ─── Workspaces ─────────────────────────────────────────────
    public const string WorkspacesRead = "Workspaces.Read";
    public const string WorkspacesCreate = "Workspaces.Create";
    public const string WorkspacesManage = "Workspaces.Manage";
    public const string WorkspaceMembersManage = "WorkspaceMembers.Manage";

    /// <summary>
    /// Tüm tanımlı izinlerin listesi (policy registration için)
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        WorkflowsRead, WorkflowsCreate, WorkflowsEdit, WorkflowsDelete, WorkflowsExecute,
        ExecutionsRead, ExecutionsCancel, ExecutionsPause,
        UsersRead, UsersManage,
        SystemAdmin, TenantsManage,
        WebhooksRead, WebhooksManage,
        FoldersRead, FoldersManage,
        AgentsRead, AgentsManage,
        ApiKeysRead, ApiKeysManage,
        ApiEndpointsRead, ApiEndpointsManage,
        SearchExecute, ChatUse,
        WorkspacesRead, WorkspacesCreate, WorkspacesManage, WorkspaceMembersManage
    ];
}
