using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Services.Agents;

public interface IAgentRunJournalService
{
    ValueTask<AgentRun> StartAsync(
        AgentRunKind kind,
        string executionUserId,
        string provider,
        string model,
        string workingDirectory,
        CancellationToken cancellationToken = default);

    ValueTask CompleteAsync(
        Guid runId,
        AgentRunState state,
        int iterations,
        string summary,
        string errorMessage,
        int processedItemCount,
        CancellationToken cancellationToken = default);
}
