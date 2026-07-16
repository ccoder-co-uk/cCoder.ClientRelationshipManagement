using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IProcessInstanceStorageBroker
{
    IQueryable<ProcessInstance> SelectAll();
    ValueTask<ProcessInstance> InsertAsync(ProcessInstance entity, CancellationToken cancellationToken = default);
    ValueTask<ProcessInstance> UpdateAsync(ProcessInstance entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(ProcessInstance entity, CancellationToken cancellationToken = default);
}

internal sealed class ProcessInstanceStorageBroker : IProcessInstanceStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public ProcessInstanceStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<ProcessInstance> SelectAll() => context.Set<ProcessInstance>();
    public async ValueTask<ProcessInstance> InsertAsync(ProcessInstance entity, CancellationToken cancellationToken = default) { context.Set<ProcessInstance>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<ProcessInstance> UpdateAsync(ProcessInstance entity, CancellationToken cancellationToken = default) { ProcessInstance local = context.Set<ProcessInstance>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<ProcessInstance>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(ProcessInstance entity, CancellationToken cancellationToken = default) { context.Set<ProcessInstance>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IProcessInstanceFoundationService
{
    IQueryable<ProcessInstance> RetrieveAll();
    IQueryable<ProcessInstance> RetrieveWriteable();
    ValueTask<ProcessInstance> AddAsync(ProcessInstance entity, CancellationToken cancellationToken = default);
    ValueTask<ProcessInstance> ModifyAsync(ProcessInstance entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(ProcessInstance entity, CancellationToken cancellationToken = default);
}

internal sealed class ProcessInstanceFoundationService(IProcessInstanceStorageBroker broker, ICRMAuthInfo auth) : IProcessInstanceFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<ProcessInstance> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<ProcessInstance> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<ProcessInstance> AddAsync(ProcessInstance entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ProcessInstance storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        ProcessInstance persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<ProcessInstance> ModifyAsync(ProcessInstance entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        ProcessInstance existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        ProcessInstance storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        ProcessInstance persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(ProcessInstance entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        ProcessInstance existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<ProcessInstance> Scope(IQueryable<ProcessInstance> source, string[] tenants) => source.Where(item => tenants.Contains(item.ProcessDefinition.TenantId));

    static ProcessInstance Copy(ProcessInstance source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            ProcessDefinitionId = source.ProcessDefinitionId,
            LeadId = source.LeadId,
            TenantCompanyRelationshipId = source.TenantCompanyRelationshipId,
            OpportunityId = source.OpportunityId,
            ClientAccountId = source.ClientAccountId,
            CurrentProcessStepId = source.CurrentProcessStepId,
            CurrentProcessTaskId = source.CurrentProcessTaskId,
            State = source.State,
            CompletionOutcomeKey = source.CompletionOutcomeKey,
            StartedOn = source.StartedOn,
            CompletedOn = source.CompletedOn,
    };

    static void CopyPersisted(ProcessInstance source, ProcessInstance target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.ProcessDefinitionId = source.ProcessDefinitionId;
        target.LeadId = source.LeadId;
        target.TenantCompanyRelationshipId = source.TenantCompanyRelationshipId;
        target.OpportunityId = source.OpportunityId;
        target.ClientAccountId = source.ClientAccountId;
        target.CurrentProcessStepId = source.CurrentProcessStepId;
        target.CurrentProcessTaskId = source.CurrentProcessTaskId;
        target.State = source.State;
        target.CompletionOutcomeKey = source.CompletionOutcomeKey;
        target.StartedOn = source.StartedOn;
        target.CompletedOn = source.CompletedOn;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IProcessInstanceProcessingService : IProcessInstanceFoundationService { }
internal sealed class ProcessInstanceProcessingService(IProcessInstanceFoundationService foundation) : IProcessInstanceProcessingService
{
    public IQueryable<ProcessInstance> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<ProcessInstance> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<ProcessInstance> AddAsync(ProcessInstance entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<ProcessInstance> ModifyAsync(ProcessInstance entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(ProcessInstance entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IProcessInstanceEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<ProcessInstance> message);
    ValueTask RaiseUpdateAsync(EventMessage<ProcessInstance> message);
    ValueTask RaiseDeleteAsync(EventMessage<ProcessInstance> message);
}
internal sealed class ProcessInstanceEventBroker(IEventHub eventHub) : IProcessInstanceEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<ProcessInstance> message) => eventHub.RaiseEventAsync("process_instance_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<ProcessInstance> message) => eventHub.RaiseEventAsync("process_instance_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<ProcessInstance> message) => eventHub.RaiseEventAsync("process_instance_delete", message);
}
public interface IProcessInstanceEventFoundationService
{
    ValueTask RaiseAddAsync(ProcessInstance entity);
    ValueTask RaiseUpdateAsync(ProcessInstance entity);
    ValueTask RaiseDeleteAsync(ProcessInstance entity);
}
internal sealed class ProcessInstanceEventFoundationService(IProcessInstanceEventBroker broker, ICRMAuthInfo auth) : IProcessInstanceEventFoundationService
{
    EventMessage<ProcessInstance> Message(ProcessInstance entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(ProcessInstance entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(ProcessInstance entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(ProcessInstance entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IProcessInstanceEventProcessingService : IProcessInstanceEventFoundationService { }
internal sealed class ProcessInstanceEventProcessingService(IProcessInstanceEventFoundationService foundation) : IProcessInstanceEventProcessingService
{
    public ValueTask RaiseAddAsync(ProcessInstance entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(ProcessInstance entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(ProcessInstance entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IProcessInstanceOrchestrationService
{
    IQueryable<ProcessInstance> RetrieveAll();
    IQueryable<ProcessInstance> RetrieveWriteable();
    ValueTask<ProcessInstance> AddAsync(ProcessInstance entity, CancellationToken cancellationToken = default);
    ValueTask<ProcessInstance> ModifyAsync(ProcessInstance entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(ProcessInstance entity, CancellationToken cancellationToken = default);
}
internal sealed class ProcessInstanceOrchestrationService(IProcessInstanceProcessingService processing, IProcessInstanceEventProcessingService events) : IProcessInstanceOrchestrationService
{
    public IQueryable<ProcessInstance> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<ProcessInstance> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<ProcessInstance> AddAsync(ProcessInstance entity, CancellationToken cancellationToken = default) { ProcessInstance persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<ProcessInstance> ModifyAsync(ProcessInstance entity, CancellationToken cancellationToken = default) { ProcessInstance persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(ProcessInstance entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
