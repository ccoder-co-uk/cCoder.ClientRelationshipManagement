using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface ISourceStorageBroker
{
    IQueryable<Source> SelectAll();
    ValueTask<Source> InsertAsync(Source entity, CancellationToken cancellationToken = default);
    ValueTask<Source> UpdateAsync(Source entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(Source entity, CancellationToken cancellationToken = default);
}

internal sealed class SourceStorageBroker : ISourceStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public SourceStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<Source> SelectAll() => context.Set<Source>();
    public async ValueTask<Source> InsertAsync(Source entity, CancellationToken cancellationToken = default) { context.Set<Source>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<Source> UpdateAsync(Source entity, CancellationToken cancellationToken = default) { Source local = context.Set<Source>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<Source>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(Source entity, CancellationToken cancellationToken = default) { context.Set<Source>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface ISourceFoundationService
{
    IQueryable<Source> RetrieveAll();
    IQueryable<Source> RetrieveWriteable();
    ValueTask<Source> AddAsync(Source entity, CancellationToken cancellationToken = default);
    ValueTask<Source> ModifyAsync(Source entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(Source entity, CancellationToken cancellationToken = default);
}

internal sealed class SourceFoundationService(ISourceStorageBroker broker, ICRMAuthInfo auth) : ISourceFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<Source> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<Source> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<Source> AddAsync(Source entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Source storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        Source persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<Source> ModifyAsync(Source entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        Source existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        Source storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        Source persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(Source entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        Source existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<Source> Scope(IQueryable<Source> source, string[] tenants) => source;

    static Source Copy(Source source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            Name = source.Name,
            SourceType = source.SourceType,
            CountryCode = source.CountryCode,
            IsAuthoritative = source.IsAuthoritative,
            Notes = source.Notes,
    };

    static void CopyPersisted(Source source, Source target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.Name = source.Name;
        target.SourceType = source.SourceType;
        target.CountryCode = source.CountryCode;
        target.IsAuthoritative = source.IsAuthoritative;
        target.Notes = source.Notes;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface ISourceProcessingService : ISourceFoundationService { }
internal sealed class SourceProcessingService(ISourceFoundationService foundation) : ISourceProcessingService
{
    public IQueryable<Source> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<Source> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<Source> AddAsync(Source entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<Source> ModifyAsync(Source entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(Source entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface ISourceEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<Source> message);
    ValueTask RaiseUpdateAsync(EventMessage<Source> message);
    ValueTask RaiseDeleteAsync(EventMessage<Source> message);
}
internal sealed class SourceEventBroker(IEventHub eventHub) : ISourceEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<Source> message) => eventHub.RaiseEventAsync("source_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<Source> message) => eventHub.RaiseEventAsync("source_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<Source> message) => eventHub.RaiseEventAsync("source_delete", message);
}
public interface ISourceEventFoundationService
{
    ValueTask RaiseAddAsync(Source entity);
    ValueTask RaiseUpdateAsync(Source entity);
    ValueTask RaiseDeleteAsync(Source entity);
}
internal sealed class SourceEventFoundationService(ISourceEventBroker broker, ICRMAuthInfo auth) : ISourceEventFoundationService
{
    EventMessage<Source> Message(Source entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(Source entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(Source entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(Source entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface ISourceEventProcessingService : ISourceEventFoundationService { }
internal sealed class SourceEventProcessingService(ISourceEventFoundationService foundation) : ISourceEventProcessingService
{
    public ValueTask RaiseAddAsync(Source entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(Source entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(Source entity) => foundation.RaiseDeleteAsync(entity);
}

public interface ISourceOrchestrationService
{
    IQueryable<Source> RetrieveAll();
    IQueryable<Source> RetrieveWriteable();
    ValueTask<Source> AddAsync(Source entity, CancellationToken cancellationToken = default);
    ValueTask<Source> ModifyAsync(Source entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(Source entity, CancellationToken cancellationToken = default);
}
internal sealed class SourceOrchestrationService(ISourceProcessingService processing, ISourceEventProcessingService events) : ISourceOrchestrationService
{
    public IQueryable<Source> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<Source> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<Source> AddAsync(Source entity, CancellationToken cancellationToken = default) { Source persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<Source> ModifyAsync(Source entity, CancellationToken cancellationToken = default) { Source persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(Source entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
