using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Services.Agents;

public interface IAgentMessageService
{
    ValueTask<AgentMessage> UpsertAsync(
        AgentMessage message,
        CancellationToken cancellationToken = default);

    ValueTask<AgentMessage> RespondAsync(
        Guid messageId,
        AgentMessageState state,
        string respondedBy,
        string responseNotes,
        CancellationToken cancellationToken = default);

    ValueTask<AgentMessageEntry> AppendEntryAsync(
        Guid messageId,
        string role,
        string body,
        string createdBy,
        CancellationToken cancellationToken = default);

    ValueTask<AgentMessage> ChangeStateAsync(
        Guid messageId,
        AgentMessageState state,
        string changedBy,
        string auditNote,
        CancellationToken cancellationToken = default);
}
