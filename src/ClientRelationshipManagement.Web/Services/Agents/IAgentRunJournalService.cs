using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Services.Agents;

public interface IAgentRunJournalService
{
    ValueTask<int> FailAbandonedAsync(
        AgentRunKind kind,
        DateTimeOffset startedBefore,
        CancellationToken cancellationToken = default);

    ValueTask<AgentRun> StartAsync(
        AgentRunKind kind,
        string executionUserId,
        string provider,
        string model,
        string workingDirectory,
        CancellationToken cancellationToken = default,
        AgentWorkLane? workLane = null,
        Guid? processTaskId = null,
        Guid? processStepId = null,
        string processStepKey = null);

    ValueTask CompleteAsync(
        Guid runId,
        AgentRunState state,
        int iterations,
        string summary,
        string errorMessage,
        int processedItemCount,
        CancellationToken cancellationToken = default);
}
