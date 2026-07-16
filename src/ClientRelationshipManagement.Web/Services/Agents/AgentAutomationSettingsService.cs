using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class AgentAutomationSettingsService(IPlatformDbContextFactory dbContextFactory)
    : IAgentAutomationSettingsService
{
    public async ValueTask<AgentAutomationSetting> GetAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        return await context.AgentAutomationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
    }

    public async ValueTask<AgentAutomationSetting> SetAutoApproveProcessEmailsAsync(
        string userId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        AgentAutomationSetting setting = await GetOrCreateAsync(userId, cancellationToken);
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        AgentAutomationSetting tracked = await context.AgentAutomationSettings
            .FirstAsync(item => item.Id == setting.Id, cancellationToken);

        tracked.AutoApproveProcessEmails = enabled;
        tracked.LastUpdatedBy = userId;
        tracked.LastUpdated = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return tracked;
    }

    public async ValueTask SetLastMailboxSyncOnAsync(
        string userId,
        DateTimeOffset syncedOn,
        CancellationToken cancellationToken = default)
    {
        AgentAutomationSetting setting = await GetOrCreateAsync(userId, cancellationToken);
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        AgentAutomationSetting tracked = await context.AgentAutomationSettings
            .FirstAsync(item => item.Id == setting.Id, cancellationToken);

        tracked.LastMailboxSyncOn = syncedOn;
        tracked.LastUpdatedBy = userId;
        tracked.LastUpdated = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask SetMailboxEvidenceBackfillCompletedOnAsync(
        string userId,
        DateTimeOffset completedOn,
        CancellationToken cancellationToken = default)
    {
        AgentAutomationSetting setting = await GetOrCreateAsync(userId, cancellationToken);
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        AgentAutomationSetting tracked = await context.AgentAutomationSettings
            .FirstAsync(item => item.Id == setting.Id, cancellationToken);

        tracked.MailboxEvidenceBackfillCompletedOn = completedOn;
        tracked.LastUpdatedBy = userId;
        tracked.LastUpdated = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    async ValueTask<AgentAutomationSetting> GetOrCreateAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("An execution user is required.", nameof(userId));

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        AgentAutomationSetting setting = await context.AgentAutomationSettings
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

        context.AgentAutomationSettings.Add(setting);
        await context.SaveChangesAsync(cancellationToken);
        return setting;
    }
}
