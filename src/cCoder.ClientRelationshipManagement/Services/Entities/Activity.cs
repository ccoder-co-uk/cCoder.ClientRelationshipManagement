using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IActivityStorageBroker
{
    IQueryable<Activity> SelectAll();
    ValueTask<Activity> InsertAsync(Activity entity, CancellationToken cancellationToken = default);
    ValueTask<Activity> UpdateAsync(Activity entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(Activity entity, CancellationToken cancellationToken = default);
}

internal sealed class ActivityStorageBroker : IActivityStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public ActivityStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<Activity> SelectAll() => context.Set<Activity>();
    public async ValueTask<Activity> InsertAsync(Activity entity, CancellationToken cancellationToken = default) { context.Set<Activity>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<Activity> UpdateAsync(Activity entity, CancellationToken cancellationToken = default) { Activity local = context.Set<Activity>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<Activity>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(Activity entity, CancellationToken cancellationToken = default) { context.Set<Activity>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IActivityFoundationService
{
    IQueryable<Activity> RetrieveAll();
    IQueryable<Activity> RetrieveWriteable();
    ValueTask<Activity> AddAsync(Activity entity, CancellationToken cancellationToken = default);
    ValueTask<Activity> ModifyAsync(Activity entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(Activity entity, CancellationToken cancellationToken = default);
}

internal sealed class ActivityFoundationService(IActivityStorageBroker broker, ICRMAuthInfo auth) : IActivityFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<Activity> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<Activity> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<Activity> AddAsync(Activity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Activity storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        Activity persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<Activity> ModifyAsync(Activity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        Activity existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        Activity storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        Activity persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(Activity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        Activity existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<Activity> Scope(IQueryable<Activity> source, string[] tenants) => source.Where(item => tenants.Contains(item.TenantCompanyRelationship.TenantId));

    static Activity Copy(Activity source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            LegacyId = source.LegacyId,
            TenantCompanyRelationshipId = source.TenantCompanyRelationshipId,
            OpportunityId = source.OpportunityId,
            ClientAccountId = source.ClientAccountId,
            CompanyContactId = source.CompanyContactId,
            MaterialId = source.MaterialId,
            ActivityOn = source.ActivityOn,
            Type = source.Type,
            Direction = source.Direction,
            Summary = source.Summary,
            Outcome = source.Outcome,
            NextAction = source.NextAction,
            NextActionDueOn = source.NextActionDueOn,
    };

    static void CopyPersisted(Activity source, Activity target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.LegacyId = source.LegacyId;
        target.TenantCompanyRelationshipId = source.TenantCompanyRelationshipId;
        target.OpportunityId = source.OpportunityId;
        target.ClientAccountId = source.ClientAccountId;
        target.CompanyContactId = source.CompanyContactId;
        target.MaterialId = source.MaterialId;
        target.ActivityOn = source.ActivityOn;
        target.Type = source.Type;
        target.Direction = source.Direction;
        target.Summary = source.Summary;
        target.Outcome = source.Outcome;
        target.NextAction = source.NextAction;
        target.NextActionDueOn = source.NextActionDueOn;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IActivityProcessingService : IActivityFoundationService { }
internal sealed class ActivityProcessingService(IActivityFoundationService foundation) : IActivityProcessingService
{
    public IQueryable<Activity> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<Activity> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<Activity> AddAsync(Activity entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<Activity> ModifyAsync(Activity entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(Activity entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IActivityEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<Activity> message);
    ValueTask RaiseUpdateAsync(EventMessage<Activity> message);
    ValueTask RaiseDeleteAsync(EventMessage<Activity> message);
}
internal sealed class ActivityEventBroker(IEventHub eventHub) : IActivityEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<Activity> message) => eventHub.RaiseEventAsync("activity_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<Activity> message) => eventHub.RaiseEventAsync("activity_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<Activity> message) => eventHub.RaiseEventAsync("activity_delete", message);
}
public interface IActivityEventFoundationService
{
    ValueTask RaiseAddAsync(Activity entity);
    ValueTask RaiseUpdateAsync(Activity entity);
    ValueTask RaiseDeleteAsync(Activity entity);
}
internal sealed class ActivityEventFoundationService(IActivityEventBroker broker, ICRMAuthInfo auth) : IActivityEventFoundationService
{
    EventMessage<Activity> Message(Activity entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(Activity entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(Activity entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(Activity entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IActivityEventProcessingService : IActivityEventFoundationService { }
internal sealed class ActivityEventProcessingService(IActivityEventFoundationService foundation) : IActivityEventProcessingService
{
    public ValueTask RaiseAddAsync(Activity entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(Activity entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(Activity entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IActivityOrchestrationService
{
    IQueryable<Activity> RetrieveAll();
    IQueryable<Activity> RetrieveWriteable();
    ValueTask<Activity> AddAsync(Activity entity, CancellationToken cancellationToken = default);
    ValueTask<Activity> ModifyAsync(Activity entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(Activity entity, CancellationToken cancellationToken = default);
}
internal sealed class ActivityOrchestrationService(IActivityProcessingService processing, IActivityEventProcessingService events) : IActivityOrchestrationService
{
    public IQueryable<Activity> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<Activity> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<Activity> AddAsync(Activity entity, CancellationToken cancellationToken = default) { Activity persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<Activity> ModifyAsync(Activity entity, CancellationToken cancellationToken = default) { Activity persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(Activity entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
