using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class AgentAutomationSettingsService(IOperationsCoordinationService operations)
    : IAgentAutomationSettingsService
{
    public async ValueTask<AgentAutomationSetting> GetAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return await operations.RetrieveAutomationSettings(userId)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
    }

    public async ValueTask<AgentAutomationSetting> SetAutoApproveProcessEmailsAsync(
        string userId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        AgentAutomationSetting setting = await GetOrCreateAsync(userId, cancellationToken);
        AgentAutomationSetting tracked = await operations.RetrieveAutomationSettings(userId)
            .FirstAsync(item => item.Id == setting.Id, cancellationToken);

        tracked.AutoApproveProcessEmails = enabled;
        tracked.LastUpdatedBy = userId;
        tracked.LastUpdated = DateTimeOffset.UtcNow;
        await operations.SaveAsync(cancellationToken);
        return tracked;
    }

    public async ValueTask SetLastMailboxSyncOnAsync(
        string userId,
        DateTimeOffset syncedOn,
        CancellationToken cancellationToken = default)
    {
        AgentAutomationSetting setting = await GetOrCreateAsync(userId, cancellationToken);
        AgentAutomationSetting tracked = await operations.RetrieveAutomationSettings(userId)
            .FirstAsync(item => item.Id == setting.Id, cancellationToken);

        tracked.LastMailboxSyncOn = syncedOn;
        tracked.LastUpdatedBy = userId;
        tracked.LastUpdated = DateTimeOffset.UtcNow;
        await operations.SaveAsync(cancellationToken);
    }

    public async ValueTask SetMailboxEvidenceBackfillCompletedOnAsync(
        string userId,
        DateTimeOffset completedOn,
        CancellationToken cancellationToken = default)
    {
        AgentAutomationSetting setting = await GetOrCreateAsync(userId, cancellationToken);
        AgentAutomationSetting tracked = await operations.RetrieveAutomationSettings(userId)
            .FirstAsync(item => item.Id == setting.Id, cancellationToken);

        tracked.MailboxEvidenceBackfillCompletedOn = completedOn;
        tracked.LastUpdatedBy = userId;
        tracked.LastUpdated = DateTimeOffset.UtcNow;
        await operations.SaveAsync(cancellationToken);
    }

    async ValueTask<AgentAutomationSetting> GetOrCreateAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("An execution user is required.", nameof(userId));

        AgentAutomationSetting setting = await operations.RetrieveAutomationSettings(userId)
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (setting is not null)
            return setting;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        setting = new AgentAutomationSetting
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedBy = userId,
            LastUpdatedBy = userId,
            CreatedOn = now,
            LastUpdated = now
        };

        operations.Add(setting);
        await operations.SaveAsync(cancellationToken);
        return setting;
    }
}
