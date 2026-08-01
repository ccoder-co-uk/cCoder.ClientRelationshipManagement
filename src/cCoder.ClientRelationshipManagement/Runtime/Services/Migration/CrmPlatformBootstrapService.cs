using cCoder.ClientRelationshipManagement.Runtime.Services.Processes;
using Microsoft.Extensions.Configuration;

namespace cCoder.ClientRelationshipManagement.Runtime.Services.Migration;

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
