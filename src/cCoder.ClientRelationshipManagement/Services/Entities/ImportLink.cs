using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IImportLinkStorageBroker
{
    IQueryable<ImportLink> SelectAll();
    ValueTask<ImportLink> InsertAsync(ImportLink entity, CancellationToken cancellationToken = default);
    ValueTask<ImportLink> UpdateAsync(ImportLink entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(ImportLink entity, CancellationToken cancellationToken = default);
}

internal sealed class ImportLinkStorageBroker : IImportLinkStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public ImportLinkStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<ImportLink> SelectAll() => context.Set<ImportLink>();
    public async ValueTask<ImportLink> InsertAsync(ImportLink entity, CancellationToken cancellationToken = default) { context.Set<ImportLink>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<ImportLink> UpdateAsync(ImportLink entity, CancellationToken cancellationToken = default) { ImportLink local = context.Set<ImportLink>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<ImportLink>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(ImportLink entity, CancellationToken cancellationToken = default) { context.Set<ImportLink>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IImportLinkFoundationService
{
    IQueryable<ImportLink> RetrieveAll();
    IQueryable<ImportLink> RetrieveWriteable();
    ValueTask<ImportLink> AddAsync(ImportLink entity, CancellationToken cancellationToken = default);
    ValueTask<ImportLink> ModifyAsync(ImportLink entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(ImportLink entity, CancellationToken cancellationToken = default);
}

internal sealed class ImportLinkFoundationService(IImportLinkStorageBroker broker, ICRMAuthInfo auth) : IImportLinkFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<ImportLink> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<ImportLink> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<ImportLink> AddAsync(ImportLink entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ImportLink storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        ImportLink persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<ImportLink> ModifyAsync(ImportLink entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        ImportLink existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        ImportLink storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        ImportLink persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(ImportLink entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        ImportLink existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<ImportLink> Scope(IQueryable<ImportLink> source, string[] tenants) => source;

    static ImportLink Copy(ImportLink source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            ImportId = source.ImportId,
            SourceId = source.SourceId,
            CompanyId = source.CompanyId,
            LeadId = source.LeadId,
            CompanyContactId = source.CompanyContactId,
            SourceRowKey = source.SourceRowKey,
            SourceRowNumber = source.SourceRowNumber,
    };

    static void CopyPersisted(ImportLink source, ImportLink target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.ImportId = source.ImportId;
        target.SourceId = source.SourceId;
        target.CompanyId = source.CompanyId;
        target.LeadId = source.LeadId;
        target.CompanyContactId = source.CompanyContactId;
        target.SourceRowKey = source.SourceRowKey;
        target.SourceRowNumber = source.SourceRowNumber;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IImportLinkProcessingService : IImportLinkFoundationService { }
internal sealed class ImportLinkProcessingService(IImportLinkFoundationService foundation) : IImportLinkProcessingService
{
    public IQueryable<ImportLink> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<ImportLink> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<ImportLink> AddAsync(ImportLink entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<ImportLink> ModifyAsync(ImportLink entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(ImportLink entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IImportLinkEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<ImportLink> message);
    ValueTask RaiseUpdateAsync(EventMessage<ImportLink> message);
    ValueTask RaiseDeleteAsync(EventMessage<ImportLink> message);
}
internal sealed class ImportLinkEventBroker(IEventHub eventHub) : IImportLinkEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<ImportLink> message) => eventHub.RaiseEventAsync("import_link_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<ImportLink> message) => eventHub.RaiseEventAsync("import_link_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<ImportLink> message) => eventHub.RaiseEventAsync("import_link_delete", message);
}
public interface IImportLinkEventFoundationService
{
    ValueTask RaiseAddAsync(ImportLink entity);
    ValueTask RaiseUpdateAsync(ImportLink entity);
    ValueTask RaiseDeleteAsync(ImportLink entity);
}
internal sealed class ImportLinkEventFoundationService(IImportLinkEventBroker broker, ICRMAuthInfo auth) : IImportLinkEventFoundationService
{
    EventMessage<ImportLink> Message(ImportLink entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(ImportLink entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(ImportLink entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(ImportLink entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IImportLinkEventProcessingService : IImportLinkEventFoundationService { }
internal sealed class ImportLinkEventProcessingService(IImportLinkEventFoundationService foundation) : IImportLinkEventProcessingService
{
    public ValueTask RaiseAddAsync(ImportLink entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(ImportLink entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(ImportLink entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IImportLinkOrchestrationService
{
    IQueryable<ImportLink> RetrieveAll();
    IQueryable<ImportLink> RetrieveWriteable();
    ValueTask<ImportLink> AddAsync(ImportLink entity, CancellationToken cancellationToken = default);
    ValueTask<ImportLink> ModifyAsync(ImportLink entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(ImportLink entity, CancellationToken cancellationToken = default);
}
internal sealed class ImportLinkOrchestrationService(IImportLinkProcessingService processing, IImportLinkEventProcessingService events) : IImportLinkOrchestrationService
{
    public IQueryable<ImportLink> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<ImportLink> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<ImportLink> AddAsync(ImportLink entity, CancellationToken cancellationToken = default) { ImportLink persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<ImportLink> ModifyAsync(ImportLink entity, CancellationToken cancellationToken = default) { ImportLink persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(ImportLink entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
