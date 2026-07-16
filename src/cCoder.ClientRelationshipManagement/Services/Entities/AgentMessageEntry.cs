using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IAgentMessageEntryStorageBroker
{
    IQueryable<AgentMessageEntry> SelectAll();
    ValueTask<AgentMessageEntry> InsertAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default);
    ValueTask<AgentMessageEntry> UpdateAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default);
}

internal sealed class AgentMessageEntryStorageBroker : IAgentMessageEntryStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public AgentMessageEntryStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<AgentMessageEntry> SelectAll() => context.Set<AgentMessageEntry>();
    public async ValueTask<AgentMessageEntry> InsertAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default) { context.Set<AgentMessageEntry>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<AgentMessageEntry> UpdateAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default) { AgentMessageEntry local = context.Set<AgentMessageEntry>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<AgentMessageEntry>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default) { context.Set<AgentMessageEntry>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IAgentMessageEntryFoundationService
{
    IQueryable<AgentMessageEntry> RetrieveAll();
    IQueryable<AgentMessageEntry> RetrieveWriteable();
    ValueTask<AgentMessageEntry> AddAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default);
    ValueTask<AgentMessageEntry> ModifyAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default);
}

internal sealed class AgentMessageEntryFoundationService(IAgentMessageEntryStorageBroker broker, ICRMAuthInfo auth) : IAgentMessageEntryFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<AgentMessageEntry> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<AgentMessageEntry> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<AgentMessageEntry> AddAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        AgentMessageEntry storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        AgentMessageEntry persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<AgentMessageEntry> ModifyAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        AgentMessageEntry existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        AgentMessageEntry storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        AgentMessageEntry persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        AgentMessageEntry existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<AgentMessageEntry> Scope(IQueryable<AgentMessageEntry> source, string[] tenants) => source.Where(item => tenants.Contains(item.AgentMessage.TenantId));

    static AgentMessageEntry Copy(AgentMessageEntry source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            AgentMessageId = source.AgentMessageId,
            Role = source.Role,
            Body = source.Body,
    };

    static void CopyPersisted(AgentMessageEntry source, AgentMessageEntry target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.AgentMessageId = source.AgentMessageId;
        target.Role = source.Role;
        target.Body = source.Body;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IAgentMessageEntryProcessingService : IAgentMessageEntryFoundationService { }
internal sealed class AgentMessageEntryProcessingService(IAgentMessageEntryFoundationService foundation) : IAgentMessageEntryProcessingService
{
    public IQueryable<AgentMessageEntry> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<AgentMessageEntry> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<AgentMessageEntry> AddAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<AgentMessageEntry> ModifyAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IAgentMessageEntryEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<AgentMessageEntry> message);
    ValueTask RaiseUpdateAsync(EventMessage<AgentMessageEntry> message);
    ValueTask RaiseDeleteAsync(EventMessage<AgentMessageEntry> message);
}
internal sealed class AgentMessageEntryEventBroker(IEventHub eventHub) : IAgentMessageEntryEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<AgentMessageEntry> message) => eventHub.RaiseEventAsync("agent_message_entry_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<AgentMessageEntry> message) => eventHub.RaiseEventAsync("agent_message_entry_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<AgentMessageEntry> message) => eventHub.RaiseEventAsync("agent_message_entry_delete", message);
}
public interface IAgentMessageEntryEventFoundationService
{
    ValueTask RaiseAddAsync(AgentMessageEntry entity);
    ValueTask RaiseUpdateAsync(AgentMessageEntry entity);
    ValueTask RaiseDeleteAsync(AgentMessageEntry entity);
}
internal sealed class AgentMessageEntryEventFoundationService(IAgentMessageEntryEventBroker broker, ICRMAuthInfo auth) : IAgentMessageEntryEventFoundationService
{
    EventMessage<AgentMessageEntry> Message(AgentMessageEntry entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(AgentMessageEntry entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(AgentMessageEntry entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(AgentMessageEntry entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IAgentMessageEntryEventProcessingService : IAgentMessageEntryEventFoundationService { }
internal sealed class AgentMessageEntryEventProcessingService(IAgentMessageEntryEventFoundationService foundation) : IAgentMessageEntryEventProcessingService
{
    public ValueTask RaiseAddAsync(AgentMessageEntry entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(AgentMessageEntry entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(AgentMessageEntry entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IAgentMessageEntryOrchestrationService
{
    IQueryable<AgentMessageEntry> RetrieveAll();
    IQueryable<AgentMessageEntry> RetrieveWriteable();
    ValueTask<AgentMessageEntry> AddAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default);
    ValueTask<AgentMessageEntry> ModifyAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default);
}
internal sealed class AgentMessageEntryOrchestrationService(IAgentMessageEntryProcessingService processing, IAgentMessageEntryEventProcessingService events) : IAgentMessageEntryOrchestrationService
{
    public IQueryable<AgentMessageEntry> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<AgentMessageEntry> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<AgentMessageEntry> AddAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default) { AgentMessageEntry persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<AgentMessageEntry> ModifyAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default) { AgentMessageEntry persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(AgentMessageEntry entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
