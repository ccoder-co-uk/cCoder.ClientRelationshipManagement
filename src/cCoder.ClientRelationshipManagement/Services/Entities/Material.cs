using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IMaterialStorageBroker
{
    IQueryable<Material> SelectAll();
    ValueTask<Material> InsertAsync(Material entity, CancellationToken cancellationToken = default);
    ValueTask<Material> UpdateAsync(Material entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(Material entity, CancellationToken cancellationToken = default);
}

internal sealed class MaterialStorageBroker : IMaterialStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public MaterialStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<Material> SelectAll() => context.Set<Material>();
    public async ValueTask<Material> InsertAsync(Material entity, CancellationToken cancellationToken = default) { context.Set<Material>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<Material> UpdateAsync(Material entity, CancellationToken cancellationToken = default) { Material local = context.Set<Material>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<Material>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(Material entity, CancellationToken cancellationToken = default) { context.Set<Material>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IMaterialFoundationService
{
    IQueryable<Material> RetrieveAll();
    IQueryable<Material> RetrieveWriteable();
    ValueTask<Material> AddAsync(Material entity, CancellationToken cancellationToken = default);
    ValueTask<Material> ModifyAsync(Material entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(Material entity, CancellationToken cancellationToken = default);
}

internal sealed class MaterialFoundationService(IMaterialStorageBroker broker, ICRMAuthInfo auth) : IMaterialFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<Material> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<Material> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<Material> AddAsync(Material entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Material storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        Material persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<Material> ModifyAsync(Material entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        Material existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        Material storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        Material persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(Material entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        Material existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<Material> Scope(IQueryable<Material> source, string[] tenants) => source.Where(item => tenants.Contains(item.TenantCompanyRelationship.TenantId));

    static Material Copy(Material source) => new()
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
            Name = source.Name,
            Type = source.Type,
            Status = source.Status,
            Notes = source.Notes,
            FilePath = source.FilePath,
            SentOn = source.SentOn,
    };

    static void CopyPersisted(Material source, Material target)
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
        target.Name = source.Name;
        target.Type = source.Type;
        target.Status = source.Status;
        target.Notes = source.Notes;
        target.FilePath = source.FilePath;
        target.SentOn = source.SentOn;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IMaterialProcessingService : IMaterialFoundationService { }
internal sealed class MaterialProcessingService(IMaterialFoundationService foundation) : IMaterialProcessingService
{
    public IQueryable<Material> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<Material> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<Material> AddAsync(Material entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<Material> ModifyAsync(Material entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(Material entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IMaterialEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<Material> message);
    ValueTask RaiseUpdateAsync(EventMessage<Material> message);
    ValueTask RaiseDeleteAsync(EventMessage<Material> message);
}
internal sealed class MaterialEventBroker(IEventHub eventHub) : IMaterialEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<Material> message) => eventHub.RaiseEventAsync("material_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<Material> message) => eventHub.RaiseEventAsync("material_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<Material> message) => eventHub.RaiseEventAsync("material_delete", message);
}
public interface IMaterialEventFoundationService
{
    ValueTask RaiseAddAsync(Material entity);
    ValueTask RaiseUpdateAsync(Material entity);
    ValueTask RaiseDeleteAsync(Material entity);
}
internal sealed class MaterialEventFoundationService(IMaterialEventBroker broker, ICRMAuthInfo auth) : IMaterialEventFoundationService
{
    EventMessage<Material> Message(Material entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(Material entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(Material entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(Material entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IMaterialEventProcessingService : IMaterialEventFoundationService { }
internal sealed class MaterialEventProcessingService(IMaterialEventFoundationService foundation) : IMaterialEventProcessingService
{
    public ValueTask RaiseAddAsync(Material entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(Material entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(Material entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IMaterialOrchestrationService
{
    IQueryable<Material> RetrieveAll();
    IQueryable<Material> RetrieveWriteable();
    ValueTask<Material> AddAsync(Material entity, CancellationToken cancellationToken = default);
    ValueTask<Material> ModifyAsync(Material entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(Material entity, CancellationToken cancellationToken = default);
}
internal sealed class MaterialOrchestrationService(IMaterialProcessingService processing, IMaterialEventProcessingService events) : IMaterialOrchestrationService
{
    public IQueryable<Material> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<Material> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<Material> AddAsync(Material entity, CancellationToken cancellationToken = default) { Material persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<Material> ModifyAsync(Material entity, CancellationToken cancellationToken = default) { Material persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(Material entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
