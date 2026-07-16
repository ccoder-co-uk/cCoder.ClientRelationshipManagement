using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IClientAccountStorageBroker
{
    IQueryable<ClientAccount> SelectAll();
    ValueTask<ClientAccount> InsertAsync(ClientAccount entity, CancellationToken cancellationToken = default);
    ValueTask<ClientAccount> UpdateAsync(ClientAccount entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(ClientAccount entity, CancellationToken cancellationToken = default);
}

internal sealed class ClientAccountStorageBroker : IClientAccountStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public ClientAccountStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<ClientAccount> SelectAll() => context.Set<ClientAccount>();
    public async ValueTask<ClientAccount> InsertAsync(ClientAccount entity, CancellationToken cancellationToken = default) { context.Set<ClientAccount>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<ClientAccount> UpdateAsync(ClientAccount entity, CancellationToken cancellationToken = default) { ClientAccount local = context.Set<ClientAccount>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<ClientAccount>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(ClientAccount entity, CancellationToken cancellationToken = default) { context.Set<ClientAccount>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IClientAccountFoundationService
{
    IQueryable<ClientAccount> RetrieveAll();
    IQueryable<ClientAccount> RetrieveWriteable();
    ValueTask<ClientAccount> AddAsync(ClientAccount entity, CancellationToken cancellationToken = default);
    ValueTask<ClientAccount> ModifyAsync(ClientAccount entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(ClientAccount entity, CancellationToken cancellationToken = default);
}

internal sealed class ClientAccountFoundationService(IClientAccountStorageBroker broker, ICRMAuthInfo auth) : IClientAccountFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<ClientAccount> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<ClientAccount> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<ClientAccount> AddAsync(ClientAccount entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ClientAccount storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        ClientAccount persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<ClientAccount> ModifyAsync(ClientAccount entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        ClientAccount existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        ClientAccount storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        ClientAccount persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(ClientAccount entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        ClientAccount existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<ClientAccount> Scope(IQueryable<ClientAccount> source, string[] tenants) => source.Where(item => tenants.Contains(item.TenantCompanyRelationship.TenantId));

    static ClientAccount Copy(ClientAccount source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            TenantCompanyRelationshipId = source.TenantCompanyRelationshipId,
            WonOpportunityId = source.WonOpportunityId,
            Status = source.Status,
            ContractSignedOn = source.ContractSignedOn,
            GoLiveOn = source.GoLiveOn,
            AccountReference = source.AccountReference,
            BillingNotes = source.BillingNotes,
    };

    static void CopyPersisted(ClientAccount source, ClientAccount target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.TenantCompanyRelationshipId = source.TenantCompanyRelationshipId;
        target.WonOpportunityId = source.WonOpportunityId;
        target.Status = source.Status;
        target.ContractSignedOn = source.ContractSignedOn;
        target.GoLiveOn = source.GoLiveOn;
        target.AccountReference = source.AccountReference;
        target.BillingNotes = source.BillingNotes;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IClientAccountProcessingService : IClientAccountFoundationService { }
internal sealed class ClientAccountProcessingService(IClientAccountFoundationService foundation) : IClientAccountProcessingService
{
    public IQueryable<ClientAccount> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<ClientAccount> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<ClientAccount> AddAsync(ClientAccount entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<ClientAccount> ModifyAsync(ClientAccount entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(ClientAccount entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IClientAccountEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<ClientAccount> message);
    ValueTask RaiseUpdateAsync(EventMessage<ClientAccount> message);
    ValueTask RaiseDeleteAsync(EventMessage<ClientAccount> message);
}
internal sealed class ClientAccountEventBroker(IEventHub eventHub) : IClientAccountEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<ClientAccount> message) => eventHub.RaiseEventAsync("client_account_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<ClientAccount> message) => eventHub.RaiseEventAsync("client_account_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<ClientAccount> message) => eventHub.RaiseEventAsync("client_account_delete", message);
}
public interface IClientAccountEventFoundationService
{
    ValueTask RaiseAddAsync(ClientAccount entity);
    ValueTask RaiseUpdateAsync(ClientAccount entity);
    ValueTask RaiseDeleteAsync(ClientAccount entity);
}
internal sealed class ClientAccountEventFoundationService(IClientAccountEventBroker broker, ICRMAuthInfo auth) : IClientAccountEventFoundationService
{
    EventMessage<ClientAccount> Message(ClientAccount entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(ClientAccount entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(ClientAccount entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(ClientAccount entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IClientAccountEventProcessingService : IClientAccountEventFoundationService { }
internal sealed class ClientAccountEventProcessingService(IClientAccountEventFoundationService foundation) : IClientAccountEventProcessingService
{
    public ValueTask RaiseAddAsync(ClientAccount entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(ClientAccount entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(ClientAccount entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IClientAccountOrchestrationService
{
    IQueryable<ClientAccount> RetrieveAll();
    IQueryable<ClientAccount> RetrieveWriteable();
    ValueTask<ClientAccount> AddAsync(ClientAccount entity, CancellationToken cancellationToken = default);
    ValueTask<ClientAccount> ModifyAsync(ClientAccount entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(ClientAccount entity, CancellationToken cancellationToken = default);
}
internal sealed class ClientAccountOrchestrationService(IClientAccountProcessingService processing, IClientAccountEventProcessingService events) : IClientAccountOrchestrationService
{
    public IQueryable<ClientAccount> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<ClientAccount> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<ClientAccount> AddAsync(ClientAccount entity, CancellationToken cancellationToken = default) { ClientAccount persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<ClientAccount> ModifyAsync(ClientAccount entity, CancellationToken cancellationToken = default) { ClientAccount persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(ClientAccount entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
