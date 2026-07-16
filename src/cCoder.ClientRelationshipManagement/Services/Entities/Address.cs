using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IAddressStorageBroker
{
    IQueryable<Address> SelectAll();
    ValueTask<Address> InsertAsync(Address entity, CancellationToken cancellationToken = default);
    ValueTask<Address> UpdateAsync(Address entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(Address entity, CancellationToken cancellationToken = default);
}

internal sealed class AddressStorageBroker : IAddressStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public AddressStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<Address> SelectAll() => context.Set<Address>();
    public async ValueTask<Address> InsertAsync(Address entity, CancellationToken cancellationToken = default) { context.Set<Address>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<Address> UpdateAsync(Address entity, CancellationToken cancellationToken = default) { Address local = context.Set<Address>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<Address>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(Address entity, CancellationToken cancellationToken = default) { context.Set<Address>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IAddressFoundationService
{
    IQueryable<Address> RetrieveAll();
    IQueryable<Address> RetrieveWriteable();
    ValueTask<Address> AddAsync(Address entity, CancellationToken cancellationToken = default);
    ValueTask<Address> ModifyAsync(Address entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(Address entity, CancellationToken cancellationToken = default);
}

internal sealed class AddressFoundationService(
    IAddressStorageBroker broker,
    ICompanyOrchestrationService companies,
    ICRMAuthInfo auth) : IAddressFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<Address> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<Address> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<Address> AddAsync(Address entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Address storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        Address persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<Address> ModifyAsync(Address entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        Address existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        Address storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        Address persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(Address entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        Address existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<Address> Scope(IQueryable<Address> source, string[] tenants) => source.Where(item =>
        companies.RetrieveAll().Any(company => company.RegisteredAddressId == item.Id));

    static Address Copy(Address source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            LegacyId = source.LegacyId,
            SourceSystem = source.SourceSystem,
            IsVerified = source.IsVerified,
            PoBox = source.PoBox,
            Line1 = source.Line1,
            Line2 = source.Line2,
            TownOrCity = source.TownOrCity,
            StateOrProvince = source.StateOrProvince,
            ZipOrPostalCode = source.ZipOrPostalCode,
            CountryId = source.CountryId,
            VerificationNotes = source.VerificationNotes,
    };

    static void CopyPersisted(Address source, Address target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.LegacyId = source.LegacyId;
        target.SourceSystem = source.SourceSystem;
        target.IsVerified = source.IsVerified;
        target.PoBox = source.PoBox;
        target.Line1 = source.Line1;
        target.Line2 = source.Line2;
        target.TownOrCity = source.TownOrCity;
        target.StateOrProvince = source.StateOrProvince;
        target.ZipOrPostalCode = source.ZipOrPostalCode;
        target.CountryId = source.CountryId;
        target.VerificationNotes = source.VerificationNotes;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IAddressProcessingService : IAddressFoundationService { }
internal sealed class AddressProcessingService(IAddressFoundationService foundation) : IAddressProcessingService
{
    public IQueryable<Address> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<Address> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<Address> AddAsync(Address entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<Address> ModifyAsync(Address entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(Address entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IAddressEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<Address> message);
    ValueTask RaiseUpdateAsync(EventMessage<Address> message);
    ValueTask RaiseDeleteAsync(EventMessage<Address> message);
}
internal sealed class AddressEventBroker(IEventHub eventHub) : IAddressEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<Address> message) => eventHub.RaiseEventAsync("address_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<Address> message) => eventHub.RaiseEventAsync("address_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<Address> message) => eventHub.RaiseEventAsync("address_delete", message);
}
public interface IAddressEventFoundationService
{
    ValueTask RaiseAddAsync(Address entity);
    ValueTask RaiseUpdateAsync(Address entity);
    ValueTask RaiseDeleteAsync(Address entity);
}
internal sealed class AddressEventFoundationService(IAddressEventBroker broker, ICRMAuthInfo auth) : IAddressEventFoundationService
{
    EventMessage<Address> Message(Address entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(Address entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(Address entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(Address entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IAddressEventProcessingService : IAddressEventFoundationService { }
internal sealed class AddressEventProcessingService(IAddressEventFoundationService foundation) : IAddressEventProcessingService
{
    public ValueTask RaiseAddAsync(Address entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(Address entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(Address entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IAddressOrchestrationService
{
    IQueryable<Address> RetrieveAll();
    IQueryable<Address> RetrieveWriteable();
    ValueTask<Address> AddAsync(Address entity, CancellationToken cancellationToken = default);
    ValueTask<Address> ModifyAsync(Address entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(Address entity, CancellationToken cancellationToken = default);
}
internal sealed class AddressOrchestrationService(IAddressProcessingService processing, IAddressEventProcessingService events) : IAddressOrchestrationService
{
    public IQueryable<Address> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<Address> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<Address> AddAsync(Address entity, CancellationToken cancellationToken = default) { Address persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<Address> ModifyAsync(Address entity, CancellationToken cancellationToken = default) { Address persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(Address entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
