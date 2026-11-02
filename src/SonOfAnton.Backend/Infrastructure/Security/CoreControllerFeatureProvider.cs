using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace SonOfAnton.Backend.Infrastructure.Security;

// Only audited core surfaces are exposed. Retained experimental code is not an API contract.
public sealed class CoreControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    private static readonly HashSet<string> Core = [
        "WorkflowsController", "ExecutionsController", "NodesController", "LogsController",
        "WorkspacesController", "FoldersController", "NotificationsController", "QuotasController",
        "ApiKeysController"
    ];

    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (var controller in feature.Controllers.ToArray())
            if (!Core.Contains(controller.Name))
                feature.Controllers.Remove(controller);
    }
}
