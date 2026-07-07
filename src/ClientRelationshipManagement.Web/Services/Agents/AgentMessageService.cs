using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class AgentMessageService(IPlatformDbContextFactory dbContextFactory) : IAgentMessageService
{
    public async ValueTask<AgentMessage> UpsertAsync(
        AgentMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        AgentMessage existing = null;

        if (!string.IsNullOrWhiteSpace(message.CorrelationKey))
        {
            existing = await context.AgentMessages
                .FirstOrDefaultAsync(item => item.CorrelationKey == message.CorrelationKey, cancellationToken);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string currentUser = string.IsNullOrWhiteSpace(message.LastUpdatedBy) ? "system" : message.LastUpdatedBy;

        if (existing is null)
        {
            message.CreatedOn = now;
            message.LastUpdated = now;
            message.CreatedBy = string.IsNullOrWhiteSpace(message.CreatedBy) ? currentUser : message.CreatedBy;
            message.LastUpdatedBy = currentUser;
            context.AgentMessages.Add(message);
            await context.SaveChangesAsync(cancellationToken);
            return message;
        }

        existing.AgentRunId = message.AgentRunId ?? existing.AgentRunId;
        existing.LeadId = message.LeadId ?? existing.LeadId;
        existing.TenantCompanyRelationshipId = message.TenantCompanyRelationshipId ?? existing.TenantCompanyRelationshipId;
        existing.OpportunityId = message.OpportunityId ?? existing.OpportunityId;
        existing.ClientAccountId = message.ClientAccountId ?? existing.ClientAccountId;
        existing.ProcessTaskId = message.ProcessTaskId ?? existing.ProcessTaskId;
        existing.EmailId = message.EmailId ?? existing.EmailId;
        existing.ProcessDefinitionId = message.ProcessDefinitionId ?? existing.ProcessDefinitionId;
        existing.ProposedProcessDefinitionId = message.ProposedProcessDefinitionId ?? existing.ProposedProcessDefinitionId;
        existing.Kind = message.Kind;
        existing.State = message.State;
        existing.Title = message.Title;
        existing.Body = message.Body;
        existing.AgentName = message.AgentName;
        existing.LastUpdatedBy = currentUser;
        existing.LastUpdated = now;

        await context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async ValueTask<AgentMessage> RespondAsync(
        Guid messageId,
        AgentMessageState state,
        string respondedBy,
        string responseNotes,
        CancellationToken cancellationToken = default)
    {
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        AgentMessage message = await context.AgentMessages.FirstOrDefaultAsync(item => item.Id == messageId, cancellationToken);
        if (message is null)
            return null;

        message.State = state;
        message.ResponseNotes = string.IsNullOrWhiteSpace(responseNotes) ? null : responseNotes.Trim();
        message.RespondedBy = respondedBy;
        message.RespondedOn = DateTimeOffset.UtcNow;
        message.LastUpdatedBy = respondedBy;
        message.LastUpdated = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return message;
    }
}
