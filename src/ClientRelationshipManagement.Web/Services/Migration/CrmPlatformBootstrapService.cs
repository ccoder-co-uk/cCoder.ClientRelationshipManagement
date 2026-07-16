using ClientRelationshipManagement.Web.Services.Processes;
using Microsoft.Extensions.Configuration;

namespace ClientRelationshipManagement.Web.Services.Migration;

public sealed class CrmPlatformBootstrapService(
    IWorkflowAutomationService workflowAutomationService,
    IConfiguration configuration)
    : ICrmPlatformBootstrapService
{
    public async ValueTask InitialiseAsync(CancellationToken cancellationToken = default)
    {
        await workflowAutomationService.EnsureSeedProcessesAsync(cancellationToken);

        if (configuration.GetValue<bool>("StartupBootstrap:EnsureWorkflowCoverage"))
            await workflowAutomationService.EnsureCoverageAsync(forceCreate: false, cancellationToken: cancellationToken);
    }
}
