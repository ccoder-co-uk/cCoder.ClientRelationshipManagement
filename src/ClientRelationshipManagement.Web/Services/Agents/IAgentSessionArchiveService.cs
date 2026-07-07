using cCoder.AI.Models.Responses;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Services.Agents;

public interface IAgentSessionArchiveService
{
    ValueTask ArchiveCompletedRunAsync(
        AgentRunKind kind,
        Guid runId,
        string executionUserId,
        string provider,
        string model,
        string workingDirectory,
        string systemPrompt,
        string instructions,
        int processedItemCount,
        AgentRunResponse response,
        CancellationToken cancellationToken = default);

    ValueTask ArchiveFailedRunAsync(
        AgentRunKind kind,
        Guid runId,
        string executionUserId,
        string provider,
        string model,
        string workingDirectory,
        string systemPrompt,
        string instructions,
        int processedItemCount,
        Exception exception,
        CancellationToken cancellationToken = default);
}
