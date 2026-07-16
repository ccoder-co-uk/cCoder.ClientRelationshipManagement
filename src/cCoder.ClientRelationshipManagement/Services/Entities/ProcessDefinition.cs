using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IProcessDefinitionStorageBroker
{
    IQueryable<ProcessDefinition> SelectAll();
    ValueTask<ProcessDefinition> InsertAsync(ProcessDefinition entity, CancellationToken cancellationToken = default);
    ValueTask<ProcessDefinition> UpdateAsync(ProcessDefinition entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(ProcessDefinition entity, CancellationToken cancellationToken = default);
}

internal sealed class ProcessDefinitionStorageBroker : IProcessDefinitionStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public ProcessDefinitionStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<ProcessDefinition> SelectAll() => context.Set<ProcessDefinition>();
    public async ValueTask<ProcessDefinition> InsertAsync(ProcessDefinition entity, CancellationToken cancellationToken = default) { context.Set<ProcessDefinition>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<ProcessDefinition> UpdateAsync(ProcessDefinition entity, CancellationToken cancellationToken = default) { ProcessDefinition local = context.Set<ProcessDefinition>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<ProcessDefinition>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(ProcessDefinition entity, CancellationToken cancellationToken = default) { context.Set<ProcessDefinition>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IProcessDefinitionFoundationService
{
    IQueryable<ProcessDefinition> RetrieveAll();
    IQueryable<ProcessDefinition> RetrieveWriteable();
    ValueTask<ProcessDefinition> AddAsync(ProcessDefinition entity, CancellationToken cancellationToken = default);
    ValueTask<ProcessDefinition> ModifyAsync(ProcessDefinition entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(ProcessDefinition entity, CancellationToken cancellationToken = default);
}

internal sealed class ProcessDefinitionFoundationService(IProcessDefinitionStorageBroker broker, ICRMAuthInfo auth) : IProcessDefinitionFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<ProcessDefinition> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<ProcessDefinition> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<ProcessDefinition> AddAsync(ProcessDefinition entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (string.IsNullOrWhiteSpace(entity.TenantId)) entity.TenantId = Writeable.FirstOrDefault() ?? throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        if (!Writeable.Contains(entity.TenantId)) throw new UnauthorizedAccessException($"The user cannot write tenant '{entity.TenantId}'.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ProcessDefinition storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        ProcessDefinition persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<ProcessDefinition> ModifyAsync(ProcessDefinition entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        ProcessDefinition existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        ProcessDefinition storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        ProcessDefinition persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(ProcessDefinition entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        ProcessDefinition existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<ProcessDefinition> Scope(IQueryable<ProcessDefinition> source, string[] tenants) => source.Where(item => tenants.Contains(item.TenantId));

    static ProcessDefinition Copy(ProcessDefinition source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            TenantId = source.TenantId,
            ScopeType = source.ScopeType,
            FamilyId = source.FamilyId,
            SupersedesProcessDefinitionId = source.SupersedesProcessDefinitionId,
            VersionNumber = source.VersionNumber,
            LifecycleState = source.LifecycleState,
            Name = source.Name,
            Description = source.Description,
            IsDefault = source.IsDefault,
            IsActive = source.IsActive,
            ChangeSummary = source.ChangeSummary,
            ApprovalNotes = source.ApprovalNotes,
            ApprovedBy = source.ApprovedBy,
            ApprovedOn = source.ApprovedOn,
            ProposedByAgent = source.ProposedByAgent,
    };

    static void CopyPersisted(ProcessDefinition source, ProcessDefinition target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.TenantId = source.TenantId;
        target.ScopeType = source.ScopeType;
        target.FamilyId = source.FamilyId;
        target.SupersedesProcessDefinitionId = source.SupersedesProcessDefinitionId;
        target.VersionNumber = source.VersionNumber;
        target.LifecycleState = source.LifecycleState;
        target.Name = source.Name;
        target.Description = source.Description;
        target.IsDefault = source.IsDefault;
        target.IsActive = source.IsActive;
        target.ChangeSummary = source.ChangeSummary;
        target.ApprovalNotes = source.ApprovalNotes;
        target.ApprovedBy = source.ApprovedBy;
        target.ApprovedOn = source.ApprovedOn;
        target.ProposedByAgent = source.ProposedByAgent;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IProcessDefinitionProcessingService : IProcessDefinitionFoundationService { }
internal sealed class ProcessDefinitionProcessingService(IProcessDefinitionFoundationService foundation) : IProcessDefinitionProcessingService
{
    public IQueryable<ProcessDefinition> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<ProcessDefinition> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<ProcessDefinition> AddAsync(ProcessDefinition entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<ProcessDefinition> ModifyAsync(ProcessDefinition entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(ProcessDefinition entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IProcessDefinitionEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<ProcessDefinition> message);
    ValueTask RaiseUpdateAsync(EventMessage<ProcessDefinition> message);
    ValueTask RaiseDeleteAsync(EventMessage<ProcessDefinition> message);
}
internal sealed class ProcessDefinitionEventBroker(IEventHub eventHub) : IProcessDefinitionEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<ProcessDefinition> message) => eventHub.RaiseEventAsync("process_definition_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<ProcessDefinition> message) => eventHub.RaiseEventAsync("process_definition_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<ProcessDefinition> message) => eventHub.RaiseEventAsync("process_definition_delete", message);
}
public interface IProcessDefinitionEventFoundationService
{
    ValueTask RaiseAddAsync(ProcessDefinition entity);
    ValueTask RaiseUpdateAsync(ProcessDefinition entity);
    ValueTask RaiseDeleteAsync(ProcessDefinition entity);
}
internal sealed class ProcessDefinitionEventFoundationService(IProcessDefinitionEventBroker broker, ICRMAuthInfo auth) : IProcessDefinitionEventFoundationService
{
    EventMessage<ProcessDefinition> Message(ProcessDefinition entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(ProcessDefinition entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(ProcessDefinition entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(ProcessDefinition entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IProcessDefinitionEventProcessingService : IProcessDefinitionEventFoundationService { }
internal sealed class ProcessDefinitionEventProcessingService(IProcessDefinitionEventFoundationService foundation) : IProcessDefinitionEventProcessingService
{
    public ValueTask RaiseAddAsync(ProcessDefinition entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(ProcessDefinition entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(ProcessDefinition entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IProcessDefinitionOrchestrationService
{
    IQueryable<ProcessDefinition> RetrieveAll();
    IQueryable<ProcessDefinition> RetrieveWriteable();
    ValueTask<ProcessDefinition> AddAsync(ProcessDefinition entity, CancellationToken cancellationToken = default);
    ValueTask<ProcessDefinition> ModifyAsync(ProcessDefinition entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(ProcessDefinition entity, CancellationToken cancellationToken = default);
}
internal sealed class ProcessDefinitionOrchestrationService(IProcessDefinitionProcessingService processing, IProcessDefinitionEventProcessingService events) : IProcessDefinitionOrchestrationService
{
    public IQueryable<ProcessDefinition> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<ProcessDefinition> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<ProcessDefinition> AddAsync(ProcessDefinition entity, CancellationToken cancellationToken = default) { ProcessDefinition persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<ProcessDefinition> ModifyAsync(ProcessDefinition entity, CancellationToken cancellationToken = default) { ProcessDefinition persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(ProcessDefinition entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
