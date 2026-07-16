using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IRelationshipContactStorageBroker
{
    IQueryable<RelationshipContact> SelectAll();
    ValueTask<RelationshipContact> InsertAsync(RelationshipContact entity, CancellationToken cancellationToken = default);
    ValueTask<RelationshipContact> UpdateAsync(RelationshipContact entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(RelationshipContact entity, CancellationToken cancellationToken = default);
}

internal sealed class RelationshipContactStorageBroker : IRelationshipContactStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public RelationshipContactStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<RelationshipContact> SelectAll() => context.Set<RelationshipContact>();
    public async ValueTask<RelationshipContact> InsertAsync(RelationshipContact entity, CancellationToken cancellationToken = default) { context.Set<RelationshipContact>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<RelationshipContact> UpdateAsync(RelationshipContact entity, CancellationToken cancellationToken = default) { RelationshipContact local = context.Set<RelationshipContact>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<RelationshipContact>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(RelationshipContact entity, CancellationToken cancellationToken = default) { context.Set<RelationshipContact>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IRelationshipContactFoundationService
{
    IQueryable<RelationshipContact> RetrieveAll();
    IQueryable<RelationshipContact> RetrieveWriteable();
    ValueTask<RelationshipContact> AddAsync(RelationshipContact entity, CancellationToken cancellationToken = default);
    ValueTask<RelationshipContact> ModifyAsync(RelationshipContact entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(RelationshipContact entity, CancellationToken cancellationToken = default);
}

internal sealed class RelationshipContactFoundationService(IRelationshipContactStorageBroker broker, ICRMAuthInfo auth) : IRelationshipContactFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<RelationshipContact> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<RelationshipContact> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<RelationshipContact> AddAsync(RelationshipContact entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        RelationshipContact storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        RelationshipContact persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<RelationshipContact> ModifyAsync(RelationshipContact entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        RelationshipContact existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        RelationshipContact storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        RelationshipContact persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(RelationshipContact entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        RelationshipContact existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<RelationshipContact> Scope(IQueryable<RelationshipContact> source, string[] tenants) => source.Where(item => tenants.Contains(item.TenantCompanyRelationship.TenantId));

    static RelationshipContact Copy(RelationshipContact source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            LegacyId = source.LegacyId,
            TenantCompanyRelationshipId = source.TenantCompanyRelationshipId,
            CompanyContactId = source.CompanyContactId,
            Status = source.Status,
            IsPrimary = source.IsPrimary,
            RelationshipRoute = source.RelationshipRoute,
            Source = source.Source,
            Notes = source.Notes,
    };

    static void CopyPersisted(RelationshipContact source, RelationshipContact target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.LegacyId = source.LegacyId;
        target.TenantCompanyRelationshipId = source.TenantCompanyRelationshipId;
        target.CompanyContactId = source.CompanyContactId;
        target.Status = source.Status;
        target.IsPrimary = source.IsPrimary;
        target.RelationshipRoute = source.RelationshipRoute;
        target.Source = source.Source;
        target.Notes = source.Notes;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IRelationshipContactProcessingService : IRelationshipContactFoundationService { }
internal sealed class RelationshipContactProcessingService(IRelationshipContactFoundationService foundation) : IRelationshipContactProcessingService
{
    public IQueryable<RelationshipContact> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<RelationshipContact> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<RelationshipContact> AddAsync(RelationshipContact entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<RelationshipContact> ModifyAsync(RelationshipContact entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(RelationshipContact entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IRelationshipContactEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<RelationshipContact> message);
    ValueTask RaiseUpdateAsync(EventMessage<RelationshipContact> message);
    ValueTask RaiseDeleteAsync(EventMessage<RelationshipContact> message);
}
internal sealed class RelationshipContactEventBroker(IEventHub eventHub) : IRelationshipContactEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<RelationshipContact> message) => eventHub.RaiseEventAsync("relationship_contact_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<RelationshipContact> message) => eventHub.RaiseEventAsync("relationship_contact_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<RelationshipContact> message) => eventHub.RaiseEventAsync("relationship_contact_delete", message);
}
public interface IRelationshipContactEventFoundationService
{
    ValueTask RaiseAddAsync(RelationshipContact entity);
    ValueTask RaiseUpdateAsync(RelationshipContact entity);
    ValueTask RaiseDeleteAsync(RelationshipContact entity);
}
internal sealed class RelationshipContactEventFoundationService(IRelationshipContactEventBroker broker, ICRMAuthInfo auth) : IRelationshipContactEventFoundationService
{
    EventMessage<RelationshipContact> Message(RelationshipContact entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(RelationshipContact entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(RelationshipContact entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(RelationshipContact entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IRelationshipContactEventProcessingService : IRelationshipContactEventFoundationService { }
internal sealed class RelationshipContactEventProcessingService(IRelationshipContactEventFoundationService foundation) : IRelationshipContactEventProcessingService
{
    public ValueTask RaiseAddAsync(RelationshipContact entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(RelationshipContact entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(RelationshipContact entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IRelationshipContactOrchestrationService
{
    IQueryable<RelationshipContact> RetrieveAll();
    IQueryable<RelationshipContact> RetrieveWriteable();
    ValueTask<RelationshipContact> AddAsync(RelationshipContact entity, CancellationToken cancellationToken = default);
    ValueTask<RelationshipContact> ModifyAsync(RelationshipContact entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(RelationshipContact entity, CancellationToken cancellationToken = default);
}
internal sealed class RelationshipContactOrchestrationService(IRelationshipContactProcessingService processing, IRelationshipContactEventProcessingService events) : IRelationshipContactOrchestrationService
{
    public IQueryable<RelationshipContact> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<RelationshipContact> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<RelationshipContact> AddAsync(RelationshipContact entity, CancellationToken cancellationToken = default) { RelationshipContact persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<RelationshipContact> ModifyAsync(RelationshipContact entity, CancellationToken cancellationToken = default) { RelationshipContact persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(RelationshipContact entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
