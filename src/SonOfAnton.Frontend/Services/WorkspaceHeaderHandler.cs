using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SonOfAnton.Frontend.Services;

public class WorkspaceHeaderHandler : DelegatingHandler
{
    private readonly WorkspaceStateService _workspaceState;

    public WorkspaceHeaderHandler(WorkspaceStateService workspaceState)
    {
        _workspaceState = workspaceState;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_workspaceState.SelectedWorkspaceId.HasValue)
        {
            request.Headers.Add("X-Workspace-Id", _workspaceState.SelectedWorkspaceId.Value.ToString());
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
