using cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace ClientRelationshipManagement.Web.Services.Agents;

public interface IAgentAutomationSettingsService
{
    ValueTask<AgentAutomationSetting> GetAsync(string userId, CancellationToken cancellationToken = default);
    ValueTask<AgentAutomationSetting> SetAutoApproveProcessEmailsAsync(
        string userId,
        bool enabled,
        CancellationToken cancellationToken = default);
    ValueTask SetLastMailboxSyncOnAsync(
        string userId,
        DateTimeOffset syncedOn,
        CancellationToken cancellationToken = default);
    ValueTask SetMailboxEvidenceBackfillCompletedOnAsync(
        string userId,
        DateTimeOffset completedOn,
        CancellationToken cancellationToken = default);
}
