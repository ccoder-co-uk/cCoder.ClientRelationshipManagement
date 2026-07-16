using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface ITenantCompanyRelationshipStorageBroker
{
    IQueryable<TenantCompanyRelationship> SelectAll();
    ValueTask<TenantCompanyRelationship> InsertAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default);
    ValueTask<TenantCompanyRelationship> UpdateAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default);
}

internal sealed class TenantCompanyRelationshipStorageBroker : ITenantCompanyRelationshipStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public TenantCompanyRelationshipStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<TenantCompanyRelationship> SelectAll() => context.Set<TenantCompanyRelationship>();
    public async ValueTask<TenantCompanyRelationship> InsertAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default) { context.Set<TenantCompanyRelationship>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<TenantCompanyRelationship> UpdateAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default) { TenantCompanyRelationship local = context.Set<TenantCompanyRelationship>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<TenantCompanyRelationship>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default) { context.Set<TenantCompanyRelationship>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface ITenantCompanyRelationshipFoundationService
{
    IQueryable<TenantCompanyRelationship> RetrieveAll();
    IQueryable<TenantCompanyRelationship> RetrieveWriteable();
    ValueTask<TenantCompanyRelationship> AddAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default);
    ValueTask<TenantCompanyRelationship> ModifyAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default);
}

internal sealed class TenantCompanyRelationshipFoundationService(ITenantCompanyRelationshipStorageBroker broker, ICRMAuthInfo auth) : ITenantCompanyRelationshipFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<TenantCompanyRelationship> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<TenantCompanyRelationship> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<TenantCompanyRelationship> AddAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (string.IsNullOrWhiteSpace(entity.TenantId)) entity.TenantId = Writeable.FirstOrDefault() ?? throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        if (!Writeable.Contains(entity.TenantId)) throw new UnauthorizedAccessException($"The user cannot write tenant '{entity.TenantId}'.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TenantCompanyRelationship storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        TenantCompanyRelationship persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<TenantCompanyRelationship> ModifyAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        TenantCompanyRelationship existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        TenantCompanyRelationship storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        TenantCompanyRelationship persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        TenantCompanyRelationship existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<TenantCompanyRelationship> Scope(IQueryable<TenantCompanyRelationship> source, string[] tenants) => source.Where(item => tenants.Contains(item.TenantId));

    static TenantCompanyRelationship Copy(TenantCompanyRelationship source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            LegacyId = source.LegacyId,
            TenantId = source.TenantId,
            CompanyId = source.CompanyId,
            AccountOwnerUserId = source.AccountOwnerUserId,
            AccountOwnerDisplayName = source.AccountOwnerDisplayName,
            Status = source.Status,
            CurrentStage = source.CurrentStage,
            Priority = source.Priority,
            LeadSource = source.LeadSource,
            InitialRoute = source.InitialRoute,
            FitScore = source.FitScore,
            OpportunitySummary = source.OpportunitySummary,
            PreferredOpeningAngle = source.PreferredOpeningAngle,
            ResearchSummary = source.ResearchSummary,
            DataQualityNotes = source.DataQualityNotes,
            IsArchived = source.IsArchived,
    };

    static void CopyPersisted(TenantCompanyRelationship source, TenantCompanyRelationship target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.LegacyId = source.LegacyId;
        target.TenantId = source.TenantId;
        target.CompanyId = source.CompanyId;
        target.AccountOwnerUserId = source.AccountOwnerUserId;
        target.AccountOwnerDisplayName = source.AccountOwnerDisplayName;
        target.Status = source.Status;
        target.CurrentStage = source.CurrentStage;
        target.Priority = source.Priority;
        target.LeadSource = source.LeadSource;
        target.InitialRoute = source.InitialRoute;
        target.FitScore = source.FitScore;
        target.OpportunitySummary = source.OpportunitySummary;
        target.PreferredOpeningAngle = source.PreferredOpeningAngle;
        target.ResearchSummary = source.ResearchSummary;
        target.DataQualityNotes = source.DataQualityNotes;
        target.IsArchived = source.IsArchived;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface ITenantCompanyRelationshipProcessingService : ITenantCompanyRelationshipFoundationService { }
internal sealed class TenantCompanyRelationshipProcessingService(ITenantCompanyRelationshipFoundationService foundation) : ITenantCompanyRelationshipProcessingService
{
    public IQueryable<TenantCompanyRelationship> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<TenantCompanyRelationship> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<TenantCompanyRelationship> AddAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<TenantCompanyRelationship> ModifyAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface ITenantCompanyRelationshipEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<TenantCompanyRelationship> message);
    ValueTask RaiseUpdateAsync(EventMessage<TenantCompanyRelationship> message);
    ValueTask RaiseDeleteAsync(EventMessage<TenantCompanyRelationship> message);
}
internal sealed class TenantCompanyRelationshipEventBroker(IEventHub eventHub) : ITenantCompanyRelationshipEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<TenantCompanyRelationship> message) => eventHub.RaiseEventAsync("tenant_company_relationship_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<TenantCompanyRelationship> message) => eventHub.RaiseEventAsync("tenant_company_relationship_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<TenantCompanyRelationship> message) => eventHub.RaiseEventAsync("tenant_company_relationship_delete", message);
}
public interface ITenantCompanyRelationshipEventFoundationService
{
    ValueTask RaiseAddAsync(TenantCompanyRelationship entity);
    ValueTask RaiseUpdateAsync(TenantCompanyRelationship entity);
    ValueTask RaiseDeleteAsync(TenantCompanyRelationship entity);
}
internal sealed class TenantCompanyRelationshipEventFoundationService(ITenantCompanyRelationshipEventBroker broker, ICRMAuthInfo auth) : ITenantCompanyRelationshipEventFoundationService
{
    EventMessage<TenantCompanyRelationship> Message(TenantCompanyRelationship entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(TenantCompanyRelationship entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(TenantCompanyRelationship entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(TenantCompanyRelationship entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface ITenantCompanyRelationshipEventProcessingService : ITenantCompanyRelationshipEventFoundationService { }
internal sealed class TenantCompanyRelationshipEventProcessingService(ITenantCompanyRelationshipEventFoundationService foundation) : ITenantCompanyRelationshipEventProcessingService
{
    public ValueTask RaiseAddAsync(TenantCompanyRelationship entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(TenantCompanyRelationship entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(TenantCompanyRelationship entity) => foundation.RaiseDeleteAsync(entity);
}

public interface ITenantCompanyRelationshipOrchestrationService
{
    IQueryable<TenantCompanyRelationship> RetrieveAll();
    IQueryable<TenantCompanyRelationship> RetrieveWriteable();
    ValueTask<TenantCompanyRelationship> AddAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default);
    ValueTask<TenantCompanyRelationship> ModifyAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default);
}
internal sealed class TenantCompanyRelationshipOrchestrationService(ITenantCompanyRelationshipProcessingService processing, ITenantCompanyRelationshipEventProcessingService events) : ITenantCompanyRelationshipOrchestrationService
{
    public IQueryable<TenantCompanyRelationship> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<TenantCompanyRelationship> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<TenantCompanyRelationship> AddAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default) { TenantCompanyRelationship persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<TenantCompanyRelationship> ModifyAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default) { TenantCompanyRelationship persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(TenantCompanyRelationship entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
