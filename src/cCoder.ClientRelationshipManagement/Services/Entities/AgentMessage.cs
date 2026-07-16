using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IAgentMessageStorageBroker
{
    IQueryable<AgentMessage> SelectAll();
    ValueTask<AgentMessage> InsertAsync(AgentMessage entity, CancellationToken cancellationToken = default);
    ValueTask<AgentMessage> UpdateAsync(AgentMessage entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(AgentMessage entity, CancellationToken cancellationToken = default);
}

internal sealed class AgentMessageStorageBroker : IAgentMessageStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public AgentMessageStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<AgentMessage> SelectAll() => context.Set<AgentMessage>();
    public async ValueTask<AgentMessage> InsertAsync(AgentMessage entity, CancellationToken cancellationToken = default) { context.Set<AgentMessage>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<AgentMessage> UpdateAsync(AgentMessage entity, CancellationToken cancellationToken = default) { AgentMessage local = context.Set<AgentMessage>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<AgentMessage>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(AgentMessage entity, CancellationToken cancellationToken = default) { context.Set<AgentMessage>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IAgentMessageFoundationService
{
    IQueryable<AgentMessage> RetrieveAll();
    IQueryable<AgentMessage> RetrieveWriteable();
    ValueTask<AgentMessage> AddAsync(AgentMessage entity, CancellationToken cancellationToken = default);
    ValueTask<AgentMessage> ModifyAsync(AgentMessage entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(AgentMessage entity, CancellationToken cancellationToken = default);
}

internal sealed class AgentMessageFoundationService(IAgentMessageStorageBroker broker, ICRMAuthInfo auth) : IAgentMessageFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<AgentMessage> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<AgentMessage> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<AgentMessage> AddAsync(AgentMessage entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (string.IsNullOrWhiteSpace(entity.TenantId)) entity.TenantId = Writeable.FirstOrDefault() ?? throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        if (!Writeable.Contains(entity.TenantId)) throw new UnauthorizedAccessException($"The user cannot write tenant '{entity.TenantId}'.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        AgentMessage storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        AgentMessage persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<AgentMessage> ModifyAsync(AgentMessage entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        AgentMessage existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        AgentMessage storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        AgentMessage persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(AgentMessage entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        AgentMessage existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<AgentMessage> Scope(IQueryable<AgentMessage> source, string[] tenants) => source.Where(item =>
        tenants.Contains(item.TenantId)
        || (item.ProcessTask != null
            && ((item.ProcessTask.LeadId.HasValue && tenants.Contains(item.ProcessTask.Lead.TenantId))
                || (item.ProcessTask.TenantCompanyRelationshipId.HasValue && tenants.Contains(item.ProcessTask.TenantCompanyRelationship.TenantId))
                || (item.ProcessTask.OpportunityId.HasValue && tenants.Contains(item.ProcessTask.Opportunity.TenantCompanyRelationship.TenantId))
                || (item.ProcessTask.ClientAccountId.HasValue && tenants.Contains(item.ProcessTask.ClientAccount.TenantCompanyRelationship.TenantId)))));

    static AgentMessage Copy(AgentMessage source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            TenantId = source.TenantId,
            AgentRunId = source.AgentRunId,
            LeadId = source.LeadId,
            TenantCompanyRelationshipId = source.TenantCompanyRelationshipId,
            OpportunityId = source.OpportunityId,
            ClientAccountId = source.ClientAccountId,
            ProcessTaskId = source.ProcessTaskId,
            ProcessStepId = source.ProcessStepId,
            EmailId = source.EmailId,
            ProcessDefinitionId = source.ProcessDefinitionId,
            ProposedProcessDefinitionId = source.ProposedProcessDefinitionId,
            Kind = source.Kind,
            State = source.State,
            CorrelationKey = source.CorrelationKey,
            Title = source.Title,
            Body = source.Body,
            AgentName = source.AgentName,
            ResponseNotes = source.ResponseNotes,
            RespondedBy = source.RespondedBy,
            RespondedOn = source.RespondedOn,
    };

    static void CopyPersisted(AgentMessage source, AgentMessage target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.TenantId = source.TenantId;
        target.AgentRunId = source.AgentRunId;
        target.LeadId = source.LeadId;
        target.TenantCompanyRelationshipId = source.TenantCompanyRelationshipId;
        target.OpportunityId = source.OpportunityId;
        target.ClientAccountId = source.ClientAccountId;
        target.ProcessTaskId = source.ProcessTaskId;
        target.ProcessStepId = source.ProcessStepId;
        target.EmailId = source.EmailId;
        target.ProcessDefinitionId = source.ProcessDefinitionId;
        target.ProposedProcessDefinitionId = source.ProposedProcessDefinitionId;
        target.Kind = source.Kind;
        target.State = source.State;
        target.CorrelationKey = source.CorrelationKey;
        target.Title = source.Title;
        target.Body = source.Body;
        target.AgentName = source.AgentName;
        target.ResponseNotes = source.ResponseNotes;
        target.RespondedBy = source.RespondedBy;
        target.RespondedOn = source.RespondedOn;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IAgentMessageProcessingService : IAgentMessageFoundationService { }
internal sealed class AgentMessageProcessingService(IAgentMessageFoundationService foundation) : IAgentMessageProcessingService
{
    public IQueryable<AgentMessage> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<AgentMessage> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<AgentMessage> AddAsync(AgentMessage entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<AgentMessage> ModifyAsync(AgentMessage entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(AgentMessage entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IAgentMessageEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<AgentMessage> message);
    ValueTask RaiseUpdateAsync(EventMessage<AgentMessage> message);
    ValueTask RaiseDeleteAsync(EventMessage<AgentMessage> message);
}
internal sealed class AgentMessageEventBroker(IEventHub eventHub) : IAgentMessageEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<AgentMessage> message) => eventHub.RaiseEventAsync("agent_message_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<AgentMessage> message) => eventHub.RaiseEventAsync("agent_message_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<AgentMessage> message) => eventHub.RaiseEventAsync("agent_message_delete", message);
}
public interface IAgentMessageEventFoundationService
{
    ValueTask RaiseAddAsync(AgentMessage entity);
    ValueTask RaiseUpdateAsync(AgentMessage entity);
    ValueTask RaiseDeleteAsync(AgentMessage entity);
}
internal sealed class AgentMessageEventFoundationService(IAgentMessageEventBroker broker, ICRMAuthInfo auth) : IAgentMessageEventFoundationService
{
    EventMessage<AgentMessage> Message(AgentMessage entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(AgentMessage entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(AgentMessage entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(AgentMessage entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IAgentMessageEventProcessingService : IAgentMessageEventFoundationService { }
internal sealed class AgentMessageEventProcessingService(IAgentMessageEventFoundationService foundation) : IAgentMessageEventProcessingService
{
    public ValueTask RaiseAddAsync(AgentMessage entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(AgentMessage entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(AgentMessage entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IAgentMessageOrchestrationService
{
    IQueryable<AgentMessage> RetrieveAll();
    IQueryable<AgentMessage> RetrieveWriteable();
    ValueTask<AgentMessage> AddAsync(AgentMessage entity, CancellationToken cancellationToken = default);
    ValueTask<AgentMessage> ModifyAsync(AgentMessage entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(AgentMessage entity, CancellationToken cancellationToken = default);
    ValueTask<AgentMessageEntry> AppendEntryAsync(Guid id, string role, string body, CancellationToken cancellationToken = default);
    ValueTask<AgentMessage> RespondAsync(Guid id, AgentMessageState state, string responseNotes, CancellationToken cancellationToken = default);
    ValueTask<AgentMessage> ChangeStateAsync(Guid id, AgentMessageState state, string auditNote, CancellationToken cancellationToken = default);
}
internal sealed class AgentMessageOrchestrationService(
    IAgentMessageProcessingService processing,
    IAgentMessageEventProcessingService events,
    IAgentMessageEntryOrchestrationService entries,
    ICRMAuthInfo auth) : IAgentMessageOrchestrationService
{
    public IQueryable<AgentMessage> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<AgentMessage> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<AgentMessage> AddAsync(AgentMessage entity, CancellationToken cancellationToken = default) { AgentMessage persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<AgentMessage> ModifyAsync(AgentMessage entity, CancellationToken cancellationToken = default) { AgentMessage persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(AgentMessage entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }

    public async ValueTask<AgentMessageEntry> AppendEntryAsync(Guid id, string role, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("A conversation entry requires a body.", nameof(body));
        AgentMessage message = await GetWriteableAsync(id, cancellationToken);
        AgentMessageEntry entry = await entries.AddAsync(new AgentMessageEntry
        {
            Id = Guid.NewGuid(), AgentMessageId = id,
            Role = string.IsNullOrWhiteSpace(role) ? "User" : role.Trim(), Body = body.Trim()
        }, cancellationToken);
        message.State = AgentMessageState.Pending;
        await ModifyAsync(message, cancellationToken);
        return entry;
    }

    public async ValueTask<AgentMessage> RespondAsync(Guid id, AgentMessageState state, string responseNotes, CancellationToken cancellationToken = default)
    {
        AgentMessage message = await GetWriteableAsync(id, cancellationToken);
        message.State = state;
        message.ResponseNotes = string.IsNullOrWhiteSpace(responseNotes) ? null : responseNotes.Trim();
        message.RespondedBy = auth.SSOUserId;
        message.RespondedOn = DateTimeOffset.UtcNow;
        return await ModifyAsync(message, cancellationToken);
    }

    public async ValueTask<AgentMessage> ChangeStateAsync(Guid id, AgentMessageState state, string auditNote, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(auditNote)) await AppendEntryAsync(id, "System", auditNote, cancellationToken);
        return await RespondAsync(id, state, null, cancellationToken);
    }

    async ValueTask<AgentMessage> GetWriteableAsync(Guid id, CancellationToken cancellationToken) =>
        await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException($"Agent message '{id}' was not found.");
}
