using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Services.Agents;

public interface IAgentWorkflowRunner
{
    ValueTask<Guid?> RunTaskAgentAsync(CancellationToken cancellationToken = default);
    ValueTask<Guid?> RunProcessOptimiserAsync(CancellationToken cancellationToken = default);
}
