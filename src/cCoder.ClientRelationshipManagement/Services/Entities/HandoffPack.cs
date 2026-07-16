using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IHandoffPackStorageBroker
{
    IQueryable<HandoffPack> SelectAll();
    ValueTask<HandoffPack> InsertAsync(HandoffPack entity, CancellationToken cancellationToken = default);
    ValueTask<HandoffPack> UpdateAsync(HandoffPack entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(HandoffPack entity, CancellationToken cancellationToken = default);
}

internal sealed class HandoffPackStorageBroker : IHandoffPackStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public HandoffPackStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<HandoffPack> SelectAll() => context.Set<HandoffPack>();
    public async ValueTask<HandoffPack> InsertAsync(HandoffPack entity, CancellationToken cancellationToken = default) { context.Set<HandoffPack>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<HandoffPack> UpdateAsync(HandoffPack entity, CancellationToken cancellationToken = default) { HandoffPack local = context.Set<HandoffPack>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<HandoffPack>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(HandoffPack entity, CancellationToken cancellationToken = default) { context.Set<HandoffPack>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IHandoffPackFoundationService
{
    IQueryable<HandoffPack> RetrieveAll();
    IQueryable<HandoffPack> RetrieveWriteable();
    ValueTask<HandoffPack> AddAsync(HandoffPack entity, CancellationToken cancellationToken = default);
    ValueTask<HandoffPack> ModifyAsync(HandoffPack entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(HandoffPack entity, CancellationToken cancellationToken = default);
}

internal sealed class HandoffPackFoundationService(IHandoffPackStorageBroker broker, ICRMAuthInfo auth) : IHandoffPackFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<HandoffPack> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<HandoffPack> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<HandoffPack> AddAsync(HandoffPack entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        HandoffPack storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        HandoffPack persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<HandoffPack> ModifyAsync(HandoffPack entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        HandoffPack existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        HandoffPack storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        HandoffPack persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(HandoffPack entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        HandoffPack existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<HandoffPack> Scope(IQueryable<HandoffPack> source, string[] tenants) => source.Where(item => tenants.Contains(item.ClientAccount.TenantCompanyRelationship.TenantId));

    static HandoffPack Copy(HandoffPack source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            LegacyId = source.LegacyId,
            ClientAccountId = source.ClientAccountId,
            AgreedScope = source.AgreedScope,
            CommercialTermsSummary = source.CommercialTermsSummary,
            PromisedOutcomes = source.PromisedOutcomes,
            PrimaryCommercialContact = source.PrimaryCommercialContact,
            PrimaryOperationalContact = source.PrimaryOperationalContact,
            PrimaryTechnicalContact = source.PrimaryTechnicalContact,
            KnownRisks = source.KnownRisks,
            OnboardingOwner = source.OnboardingOwner,
            LegalEntity = source.LegalEntity,
            SignedContractPath = source.SignedContractPath,
            Status = source.Status,
    };

    static void CopyPersisted(HandoffPack source, HandoffPack target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.LegacyId = source.LegacyId;
        target.ClientAccountId = source.ClientAccountId;
        target.AgreedScope = source.AgreedScope;
        target.CommercialTermsSummary = source.CommercialTermsSummary;
        target.PromisedOutcomes = source.PromisedOutcomes;
        target.PrimaryCommercialContact = source.PrimaryCommercialContact;
        target.PrimaryOperationalContact = source.PrimaryOperationalContact;
        target.PrimaryTechnicalContact = source.PrimaryTechnicalContact;
        target.KnownRisks = source.KnownRisks;
        target.OnboardingOwner = source.OnboardingOwner;
        target.LegalEntity = source.LegalEntity;
        target.SignedContractPath = source.SignedContractPath;
        target.Status = source.Status;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IHandoffPackProcessingService : IHandoffPackFoundationService { }
internal sealed class HandoffPackProcessingService(IHandoffPackFoundationService foundation) : IHandoffPackProcessingService
{
    public IQueryable<HandoffPack> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<HandoffPack> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<HandoffPack> AddAsync(HandoffPack entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<HandoffPack> ModifyAsync(HandoffPack entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(HandoffPack entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IHandoffPackEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<HandoffPack> message);
    ValueTask RaiseUpdateAsync(EventMessage<HandoffPack> message);
    ValueTask RaiseDeleteAsync(EventMessage<HandoffPack> message);
}
internal sealed class HandoffPackEventBroker(IEventHub eventHub) : IHandoffPackEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<HandoffPack> message) => eventHub.RaiseEventAsync("handoff_pack_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<HandoffPack> message) => eventHub.RaiseEventAsync("handoff_pack_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<HandoffPack> message) => eventHub.RaiseEventAsync("handoff_pack_delete", message);
}
public interface IHandoffPackEventFoundationService
{
    ValueTask RaiseAddAsync(HandoffPack entity);
    ValueTask RaiseUpdateAsync(HandoffPack entity);
    ValueTask RaiseDeleteAsync(HandoffPack entity);
}
internal sealed class HandoffPackEventFoundationService(IHandoffPackEventBroker broker, ICRMAuthInfo auth) : IHandoffPackEventFoundationService
{
    EventMessage<HandoffPack> Message(HandoffPack entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(HandoffPack entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(HandoffPack entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(HandoffPack entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IHandoffPackEventProcessingService : IHandoffPackEventFoundationService { }
internal sealed class HandoffPackEventProcessingService(IHandoffPackEventFoundationService foundation) : IHandoffPackEventProcessingService
{
    public ValueTask RaiseAddAsync(HandoffPack entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(HandoffPack entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(HandoffPack entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IHandoffPackOrchestrationService
{
    IQueryable<HandoffPack> RetrieveAll();
    IQueryable<HandoffPack> RetrieveWriteable();
    ValueTask<HandoffPack> AddAsync(HandoffPack entity, CancellationToken cancellationToken = default);
    ValueTask<HandoffPack> ModifyAsync(HandoffPack entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(HandoffPack entity, CancellationToken cancellationToken = default);
}
internal sealed class HandoffPackOrchestrationService(IHandoffPackProcessingService processing, IHandoffPackEventProcessingService events) : IHandoffPackOrchestrationService
{
    public IQueryable<HandoffPack> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<HandoffPack> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<HandoffPack> AddAsync(HandoffPack entity, CancellationToken cancellationToken = default) { HandoffPack persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<HandoffPack> ModifyAsync(HandoffPack entity, CancellationToken cancellationToken = default) { HandoffPack persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(HandoffPack entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
