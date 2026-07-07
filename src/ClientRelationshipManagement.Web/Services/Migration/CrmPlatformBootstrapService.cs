using ClientRelationshipManagement.Web.Services.Processes;

namespace ClientRelationshipManagement.Web.Services.Migration;

public sealed class CrmPlatformBootstrapService(
    IWorkflowAutomationService workflowAutomationService)
    : ICrmPlatformBootstrapService
{
    public async ValueTask InitialiseAsync(CancellationToken cancellationToken = default)
    {
        await workflowAutomationService.EnsureSeedProcessesAsync(cancellationToken);
        await workflowAutomationService.EnsureCoverageAsync(forceCreate: false, cancellationToken: cancellationToken);
    }
}
