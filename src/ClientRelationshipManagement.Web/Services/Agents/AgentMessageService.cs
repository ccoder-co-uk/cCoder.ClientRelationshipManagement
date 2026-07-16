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
            message.TenantId = await ResolveTenantIdAsync(context, message, cancellationToken);
            message.CreatedOn = now;
            message.LastUpdated = now;
            message.CreatedBy = string.IsNullOrWhiteSpace(message.CreatedBy) ? currentUser : message.CreatedBy;
            message.LastUpdatedBy = currentUser;
            context.AgentMessages.Add(message);
            await context.SaveChangesAsync(cancellationToken);
            return message;
        }

        existing.AgentRunId = message.AgentRunId ?? existing.AgentRunId;
        existing.TenantId = string.IsNullOrWhiteSpace(message.TenantId) ? existing.TenantId : message.TenantId;
        existing.LeadId = message.LeadId ?? existing.LeadId;
        existing.TenantCompanyRelationshipId = message.TenantCompanyRelationshipId ?? existing.TenantCompanyRelationshipId;
        existing.OpportunityId = message.OpportunityId ?? existing.OpportunityId;
        existing.ClientAccountId = message.ClientAccountId ?? existing.ClientAccountId;
        existing.ProcessTaskId = message.ProcessTaskId ?? existing.ProcessTaskId;
        existing.ProcessStepId = message.ProcessStepId ?? existing.ProcessStepId;
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

    public async ValueTask<AgentMessageEntry> AppendEntryAsync(
        Guid messageId,
        string role,
        string body,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        AgentMessage message = await context.AgentMessages.FirstOrDefaultAsync(item => item.Id == messageId, cancellationToken);
        if (message is null)
            return null;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        AgentMessageEntry entry = new()
        {
            Id = Guid.NewGuid(),
            AgentMessageId = messageId,
            Role = string.IsNullOrWhiteSpace(role) ? "User" : role.Trim(),
            Body = body.Trim(),
            CreatedBy = createdBy,
            LastUpdatedBy = createdBy,
            CreatedOn = now,
            LastUpdated = now
        };
        context.AgentMessageEntries.Add(entry);
        message.State = AgentMessageState.Pending;
        message.LastUpdatedBy = createdBy;
        message.LastUpdated = now;
        await context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async ValueTask<AgentMessage> ChangeStateAsync(
        Guid messageId,
        AgentMessageState state,
        string changedBy,
        string auditNote,
        CancellationToken cancellationToken = default)
    {
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        AgentMessage message = await context.AgentMessages.FirstOrDefaultAsync(item => item.Id == messageId, cancellationToken);
        if (message is null)
            return null;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string actor = string.IsNullOrWhiteSpace(changedBy) ? "system" : changedBy.Trim();
        if (!string.IsNullOrWhiteSpace(auditNote))
        {
            context.AgentMessageEntries.Add(new AgentMessageEntry
            {
                Id = Guid.NewGuid(),
                AgentMessageId = messageId,
                Role = "System",
                Body = auditNote.Trim(),
                CreatedBy = actor,
                LastUpdatedBy = actor,
                CreatedOn = now,
                LastUpdated = now
            });
        }

        message.State = state;
        message.RespondedBy = actor;
        message.RespondedOn = now;
        message.LastUpdatedBy = actor;
        message.LastUpdated = now;
        await context.SaveChangesAsync(cancellationToken);
        return message;
    }

    static async ValueTask<string> ResolveTenantIdAsync(
        PlatformDbContext context,
        AgentMessage message,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message.TenantId))
            return message.TenantId.Trim();
        if (message.ProcessDefinitionId.HasValue)
            return await context.ProcessDefinitions.Where(item => item.Id == message.ProcessDefinitionId.Value)
                .Select(item => item.TenantId).FirstOrDefaultAsync(cancellationToken) ?? "default";
        if (message.TenantCompanyRelationshipId.HasValue)
            return await context.TenantCompanyRelationships.Where(item => item.Id == message.TenantCompanyRelationshipId.Value)
                .Select(item => item.TenantId).FirstOrDefaultAsync(cancellationToken) ?? "default";
        if (message.ProcessTaskId.HasValue)
            return await context.ProcessTasks.Where(item => item.Id == message.ProcessTaskId.Value)
                .Select(item => item.ProcessInstance.ProcessDefinition.TenantId).FirstOrDefaultAsync(cancellationToken) ?? "default";
        return "default";
    }
}
