using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using Microsoft.EntityFrameworkCore;
using IPlatformAgentMessageService = cCoder.ClientRelationshipManagement.Services.Entities.IAgentMessageOrchestrationService;

namespace ClientRelationshipManagement.Web.Services.Agents;

public sealed class AgentMessageService(
    IPlatformAgentMessageService messages,
    IProcessCoordinationService processes,
    ISalesCoordinationService sales) : IAgentMessageService
{
    public async ValueTask<AgentMessage> UpsertAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        AgentMessage existing = string.IsNullOrWhiteSpace(message.CorrelationKey)
            ? null
            : await messages.RetrieveAll().FirstOrDefaultAsync(
                item => item.CorrelationKey == message.CorrelationKey, cancellationToken);

        if (existing is null)
        {
            message.TenantId = await ResolveTenantIdAsync(message, cancellationToken);
            return await messages.AddAsync(message, cancellationToken);
        }

        existing.AgentRunId = message.AgentRunId ?? existing.AgentRunId;
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
        return await messages.ModifyAsync(existing, cancellationToken);
    }

    public async ValueTask<AgentMessage> RespondAsync(Guid messageId, AgentMessageState state,
        string respondedBy, string responseNotes, CancellationToken cancellationToken = default) =>
        await ExistsAsync(messageId, cancellationToken)
            ? await messages.RespondAsync(messageId, state, responseNotes, cancellationToken)
            : null;

    public async ValueTask<AgentMessageEntry> AppendEntryAsync(Guid messageId, string role,
        string body, string createdBy, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(body) || !await ExistsAsync(messageId, cancellationToken)
            ? null
            : await messages.AppendEntryAsync(messageId, role, body, cancellationToken);

    public async ValueTask<AgentMessage> ChangeStateAsync(Guid messageId, AgentMessageState state,
        string changedBy, string auditNote, CancellationToken cancellationToken = default) =>
        await ExistsAsync(messageId, cancellationToken)
            ? await messages.ChangeStateAsync(messageId, state, auditNote, cancellationToken)
            : null;

    async ValueTask<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        await messages.RetrieveAll().AnyAsync(item => item.Id == id, cancellationToken);

    async ValueTask<string> ResolveTenantIdAsync(AgentMessage message, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(message.TenantId)) return message.TenantId.Trim();
        if (message.ProcessDefinitionId.HasValue)
            return await processes.RetrieveDefinitions().Where(item => item.Id == message.ProcessDefinitionId)
                .Select(item => item.TenantId).FirstOrDefaultAsync(cancellationToken) ?? "default";
        if (message.TenantCompanyRelationshipId.HasValue)
            return await sales.RetrieveRelationships().Where(item => item.Id == message.TenantCompanyRelationshipId)
                .Select(item => item.TenantId).FirstOrDefaultAsync(cancellationToken) ?? "default";
        if (message.ProcessTaskId.HasValue)
            return await processes.RetrieveTasks().Where(item => item.Id == message.ProcessTaskId)
                .Select(item => item.ProcessStep.ProcessDefinition.TenantId).FirstOrDefaultAsync(cancellationToken) ?? "default";
        return "default";
    }
}
