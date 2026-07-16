using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface IOpportunityStorageBroker
{
    IQueryable<Opportunity> SelectAll();
    ValueTask<Opportunity> InsertAsync(Opportunity entity, CancellationToken cancellationToken = default);
    ValueTask<Opportunity> UpdateAsync(Opportunity entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(Opportunity entity, CancellationToken cancellationToken = default);
}

internal sealed class OpportunityStorageBroker : IOpportunityStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public OpportunityStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<Opportunity> SelectAll() => context.Set<Opportunity>();
    public async ValueTask<Opportunity> InsertAsync(Opportunity entity, CancellationToken cancellationToken = default) { context.Set<Opportunity>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<Opportunity> UpdateAsync(Opportunity entity, CancellationToken cancellationToken = default) { Opportunity local = context.Set<Opportunity>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<Opportunity>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(Opportunity entity, CancellationToken cancellationToken = default) { context.Set<Opportunity>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface IOpportunityFoundationService
{
    IQueryable<Opportunity> RetrieveAll();
    IQueryable<Opportunity> RetrieveWriteable();
    ValueTask<Opportunity> AddAsync(Opportunity entity, CancellationToken cancellationToken = default);
    ValueTask<Opportunity> ModifyAsync(Opportunity entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(Opportunity entity, CancellationToken cancellationToken = default);
}

internal sealed class OpportunityFoundationService(IOpportunityStorageBroker broker, ICRMAuthInfo auth) : IOpportunityFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<Opportunity> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<Opportunity> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<Opportunity> AddAsync(Opportunity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Opportunity storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        Opportunity persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<Opportunity> ModifyAsync(Opportunity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        Opportunity existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        Opportunity storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        Opportunity persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(Opportunity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        Opportunity existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<Opportunity> Scope(IQueryable<Opportunity> source, string[] tenants) => source.Where(item => tenants.Contains(item.TenantCompanyRelationship.TenantId));

    static Opportunity Copy(Opportunity source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            LegacyId = source.LegacyId,
            TenantCompanyRelationshipId = source.TenantCompanyRelationshipId,
            PrimaryRelationshipContactId = source.PrimaryRelationshipContactId,
            Type = source.Type,
            Stage = source.Stage,
            EstimatedAnnualValue = source.EstimatedAnnualValue,
            Probability = source.Probability,
            PainSummary = source.PainSummary,
            ValueHypothesis = source.ValueHypothesis,
            DecisionProcess = source.DecisionProcess,
            WonOn = source.WonOn,
            LostOn = source.LostOn,
    };

    static void CopyPersisted(Opportunity source, Opportunity target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.LegacyId = source.LegacyId;
        target.TenantCompanyRelationshipId = source.TenantCompanyRelationshipId;
        target.PrimaryRelationshipContactId = source.PrimaryRelationshipContactId;
        target.Type = source.Type;
        target.Stage = source.Stage;
        target.EstimatedAnnualValue = source.EstimatedAnnualValue;
        target.Probability = source.Probability;
        target.PainSummary = source.PainSummary;
        target.ValueHypothesis = source.ValueHypothesis;
        target.DecisionProcess = source.DecisionProcess;
        target.WonOn = source.WonOn;
        target.LostOn = source.LostOn;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface IOpportunityProcessingService : IOpportunityFoundationService { }
internal sealed class OpportunityProcessingService(IOpportunityFoundationService foundation) : IOpportunityProcessingService
{
    public IQueryable<Opportunity> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<Opportunity> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<Opportunity> AddAsync(Opportunity entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<Opportunity> ModifyAsync(Opportunity entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(Opportunity entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface IOpportunityEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<Opportunity> message);
    ValueTask RaiseUpdateAsync(EventMessage<Opportunity> message);
    ValueTask RaiseDeleteAsync(EventMessage<Opportunity> message);
}
internal sealed class OpportunityEventBroker(IEventHub eventHub) : IOpportunityEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<Opportunity> message) => eventHub.RaiseEventAsync("opportunity_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<Opportunity> message) => eventHub.RaiseEventAsync("opportunity_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<Opportunity> message) => eventHub.RaiseEventAsync("opportunity_delete", message);
}
public interface IOpportunityEventFoundationService
{
    ValueTask RaiseAddAsync(Opportunity entity);
    ValueTask RaiseUpdateAsync(Opportunity entity);
    ValueTask RaiseDeleteAsync(Opportunity entity);
}
internal sealed class OpportunityEventFoundationService(IOpportunityEventBroker broker, ICRMAuthInfo auth) : IOpportunityEventFoundationService
{
    EventMessage<Opportunity> Message(Opportunity entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(Opportunity entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(Opportunity entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(Opportunity entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface IOpportunityEventProcessingService : IOpportunityEventFoundationService { }
internal sealed class OpportunityEventProcessingService(IOpportunityEventFoundationService foundation) : IOpportunityEventProcessingService
{
    public ValueTask RaiseAddAsync(Opportunity entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(Opportunity entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(Opportunity entity) => foundation.RaiseDeleteAsync(entity);
}

public interface IOpportunityOrchestrationService
{
    IQueryable<Opportunity> RetrieveAll();
    IQueryable<Opportunity> RetrieveWriteable();
    ValueTask<Opportunity> AddAsync(Opportunity entity, CancellationToken cancellationToken = default);
    ValueTask<Opportunity> ModifyAsync(Opportunity entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(Opportunity entity, CancellationToken cancellationToken = default);
}
internal sealed class OpportunityOrchestrationService(IOpportunityProcessingService processing, IOpportunityEventProcessingService events) : IOpportunityOrchestrationService
{
    public IQueryable<Opportunity> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<Opportunity> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<Opportunity> AddAsync(Opportunity entity, CancellationToken cancellationToken = default) { Opportunity persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<Opportunity> ModifyAsync(Opportunity entity, CancellationToken cancellationToken = default) { Opportunity persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(Opportunity entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
