using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IProcessTransitionStorageBroker
{
    IQueryable<ProcessTransition> SelectAll();
    ValueTask<ProcessTransition> InsertAsync(ProcessTransition entity, CancellationToken cancellationToken = default);
    ValueTask<ProcessTransition> UpdateAsync(ProcessTransition entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(ProcessTransition entity, CancellationToken cancellationToken = default);
}

internal sealed class ProcessTransitionStorageBroker : IProcessTransitionStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public ProcessTransitionStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<ProcessTransition> SelectAll() => context.Set<ProcessTransition>();
    public async ValueTask<ProcessTransition> InsertAsync(ProcessTransition entity, CancellationToken cancellationToken = default) { context.Set<ProcessTransition>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<ProcessTransition> UpdateAsync(ProcessTransition entity, CancellationToken cancellationToken = default) { ProcessTransition local = context.Set<ProcessTransition>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<ProcessTransition>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(ProcessTransition entity, CancellationToken cancellationToken = default) { context.Set<ProcessTransition>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IProcessTransitionFoundationService
{
    IQueryable<ProcessTransition> RetrieveAll();
    IQueryable<ProcessTransition> RetrieveWriteable();
    ValueTask<ProcessTransition> AddAsync(ProcessTransition entity, CancellationToken cancellationToken = default);
    ValueTask<ProcessTransition> ModifyAsync(ProcessTransition entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(ProcessTransition entity, CancellationToken cancellationToken = default);
}

internal sealed class ProcessTransitionFoundationService(IProcessTransitionStorageBroker broker, ICRMAuthInfo auth) : IProcessTransitionFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<ProcessTransition> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<ProcessTransition> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<ProcessTransition> AddAsync(ProcessTransition entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ProcessTransition storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        ProcessTransition persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<ProcessTransition> ModifyAsync(ProcessTransition entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        ProcessTransition existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        ProcessTransition storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        ProcessTransition persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(ProcessTransition entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        ProcessTransition existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<ProcessTransition> Scope(IQueryable<ProcessTransition> source, string[] tenants) => source.Where(item => tenants.Contains(item.ProcessStep.ProcessDefinition.TenantId));

    static ProcessTransition Copy(ProcessTransition source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            ProcessStepId = source.ProcessStepId,
            NextProcessStepId = source.NextProcessStepId,
            OutcomeKey = source.OutcomeKey,
            OutcomeLabel = source.OutcomeLabel,
            IsDefaultOutcome = source.IsDefaultOutcome,
            IsTerminal = source.IsTerminal,
            Effect = source.Effect,
            ResultingRelationshipStatus = source.ResultingRelationshipStatus,
            ResultingSalesStage = source.ResultingSalesStage,
            ResultingClientAccountStatus = source.ResultingClientAccountStatus,
    };

    static void CopyPersisted(ProcessTransition source, ProcessTransition target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.ProcessStepId = source.ProcessStepId;
        target.NextProcessStepId = source.NextProcessStepId;
        target.OutcomeKey = source.OutcomeKey;
        target.OutcomeLabel = source.OutcomeLabel;
        target.IsDefaultOutcome = source.IsDefaultOutcome;
        target.IsTerminal = source.IsTerminal;
        target.Effect = source.Effect;
        target.ResultingRelationshipStatus = source.ResultingRelationshipStatus;
        target.ResultingSalesStage = source.ResultingSalesStage;
        target.ResultingClientAccountStatus = source.ResultingClientAccountStatus;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IProcessTransitionProcessingService : IProcessTransitionFoundationService { }
internal sealed class ProcessTransitionProcessingService(IProcessTransitionFoundationService foundation) : IProcessTransitionProcessingService
{
    public IQueryable<ProcessTransition> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<ProcessTransition> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<ProcessTransition> AddAsync(ProcessTransition entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<ProcessTransition> ModifyAsync(ProcessTransition entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(ProcessTransition entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IProcessTransitionEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<ProcessTransition> message);
    ValueTask RaiseUpdateAsync(EventMessage<ProcessTransition> message);
    ValueTask RaiseDeleteAsync(EventMessage<ProcessTransition> message);
}
internal sealed class ProcessTransitionEventBroker(IEventHub eventHub) : IProcessTransitionEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<ProcessTransition> message) => eventHub.RaiseEventAsync("process_transition_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<ProcessTransition> message) => eventHub.RaiseEventAsync("process_transition_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<ProcessTransition> message) => eventHub.RaiseEventAsync("process_transition_delete", message);
}
public interface IProcessTransitionEventFoundationService
{
    ValueTask RaiseAddAsync(ProcessTransition entity);
    ValueTask RaiseUpdateAsync(ProcessTransition entity);
    ValueTask RaiseDeleteAsync(ProcessTransition entity);
}
internal sealed class ProcessTransitionEventFoundationService(IProcessTransitionEventBroker broker, ICRMAuthInfo auth) : IProcessTransitionEventFoundationService
{
    EventMessage<ProcessTransition> Message(ProcessTransition entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(ProcessTransition entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(ProcessTransition entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(ProcessTransition entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IProcessTransitionEventProcessingService : IProcessTransitionEventFoundationService { }
internal sealed class ProcessTransitionEventProcessingService(IProcessTransitionEventFoundationService foundation) : IProcessTransitionEventProcessingService
{
    public ValueTask RaiseAddAsync(ProcessTransition entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(ProcessTransition entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(ProcessTransition entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IProcessTransitionOrchestrationService
{
    IQueryable<ProcessTransition> RetrieveAll();
    IQueryable<ProcessTransition> RetrieveWriteable();
    ValueTask<ProcessTransition> AddAsync(ProcessTransition entity, CancellationToken cancellationToken = default);
    ValueTask<ProcessTransition> ModifyAsync(ProcessTransition entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(ProcessTransition entity, CancellationToken cancellationToken = default);
}
internal sealed class ProcessTransitionOrchestrationService(IProcessTransitionProcessingService processing, IProcessTransitionEventProcessingService events) : IProcessTransitionOrchestrationService
{
    public IQueryable<ProcessTransition> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<ProcessTransition> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<ProcessTransition> AddAsync(ProcessTransition entity, CancellationToken cancellationToken = default) { ProcessTransition persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<ProcessTransition> ModifyAsync(ProcessTransition entity, CancellationToken cancellationToken = default) { ProcessTransition persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(ProcessTransition entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
