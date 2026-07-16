using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface ILeadStorageBroker
{
    IQueryable<Lead> SelectAll();
    ValueTask<Lead> InsertAsync(Lead entity, CancellationToken cancellationToken = default);
    ValueTask<Lead> UpdateAsync(Lead entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(Lead entity, CancellationToken cancellationToken = default);
}

internal sealed class LeadStorageBroker : ILeadStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public LeadStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<Lead> SelectAll() => context.Set<Lead>();
    public async ValueTask<Lead> InsertAsync(Lead entity, CancellationToken cancellationToken = default) { context.Set<Lead>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<Lead> UpdateAsync(Lead entity, CancellationToken cancellationToken = default) { Lead local = context.Set<Lead>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<Lead>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(Lead entity, CancellationToken cancellationToken = default) { context.Set<Lead>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface ILeadFoundationService
{
    IQueryable<Lead> RetrieveAll();
    IQueryable<Lead> RetrieveWriteable();
    ValueTask<Lead> AddAsync(Lead entity, CancellationToken cancellationToken = default);
    ValueTask<Lead> ModifyAsync(Lead entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(Lead entity, CancellationToken cancellationToken = default);
}

internal sealed class LeadFoundationService(ILeadStorageBroker broker, ICRMAuthInfo auth) : ILeadFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<Lead> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<Lead> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<Lead> AddAsync(Lead entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (string.IsNullOrWhiteSpace(entity.TenantId)) entity.TenantId = Writeable.FirstOrDefault() ?? throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        if (!Writeable.Contains(entity.TenantId)) throw new UnauthorizedAccessException($"The user cannot write tenant '{entity.TenantId}'.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Lead storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        Lead persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<Lead> ModifyAsync(Lead entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        Lead existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        Lead storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        Lead persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(Lead entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        Lead existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<Lead> Scope(IQueryable<Lead> source, string[] tenants) => source.Where(item => tenants.Contains(item.TenantId));

    static Lead Copy(Lead source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            SourceId = source.SourceId,
            SourceSystem = source.SourceSystem,
            SourceRecordId = source.SourceRecordId,
            SourceFileName = source.SourceFileName,
            TenantId = source.TenantId,
            Status = source.Status,
            RawCompanyName = source.RawCompanyName,
            RawTradingName = source.RawTradingName,
            RawCompanyNumber = source.RawCompanyNumber,
            RawVatNumber = source.RawVatNumber,
            RawWebsiteUrl = source.RawWebsiteUrl,
            RawContactEmailAddress = source.RawContactEmailAddress,
            RawContactPhoneNumber = source.RawContactPhoneNumber,
            QualificationNotes = source.QualificationNotes,
            RankingScore = source.RankingScore,
            RankingRationale = source.RankingRationale,
            CompanyId = source.CompanyId,
            TenantCompanyRelationshipId = source.TenantCompanyRelationshipId,
            OpportunityId = source.OpportunityId,
    };

    static void CopyPersisted(Lead source, Lead target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.SourceId = source.SourceId;
        target.SourceSystem = source.SourceSystem;
        target.SourceRecordId = source.SourceRecordId;
        target.SourceFileName = source.SourceFileName;
        target.TenantId = source.TenantId;
        target.Status = source.Status;
        target.RawCompanyName = source.RawCompanyName;
        target.RawTradingName = source.RawTradingName;
        target.RawCompanyNumber = source.RawCompanyNumber;
        target.RawVatNumber = source.RawVatNumber;
        target.RawWebsiteUrl = source.RawWebsiteUrl;
        target.RawContactEmailAddress = source.RawContactEmailAddress;
        target.RawContactPhoneNumber = source.RawContactPhoneNumber;
        target.QualificationNotes = source.QualificationNotes;
        target.RankingScore = source.RankingScore;
        target.RankingRationale = source.RankingRationale;
        target.CompanyId = source.CompanyId;
        target.TenantCompanyRelationshipId = source.TenantCompanyRelationshipId;
        target.OpportunityId = source.OpportunityId;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface ILeadProcessingService : ILeadFoundationService { }
internal sealed class LeadProcessingService(ILeadFoundationService foundation) : ILeadProcessingService
{
    public IQueryable<Lead> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<Lead> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<Lead> AddAsync(Lead entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<Lead> ModifyAsync(Lead entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(Lead entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface ILeadEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<Lead> message);
    ValueTask RaiseUpdateAsync(EventMessage<Lead> message);
    ValueTask RaiseDeleteAsync(EventMessage<Lead> message);
}
internal sealed class LeadEventBroker(IEventHub eventHub) : ILeadEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<Lead> message) => eventHub.RaiseEventAsync("lead_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<Lead> message) => eventHub.RaiseEventAsync("lead_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<Lead> message) => eventHub.RaiseEventAsync("lead_delete", message);
}
public interface ILeadEventFoundationService
{
    ValueTask RaiseAddAsync(Lead entity);
    ValueTask RaiseUpdateAsync(Lead entity);
    ValueTask RaiseDeleteAsync(Lead entity);
}
internal sealed class LeadEventFoundationService(ILeadEventBroker broker, ICRMAuthInfo auth) : ILeadEventFoundationService
{
    EventMessage<Lead> Message(Lead entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(Lead entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(Lead entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(Lead entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface ILeadEventProcessingService : ILeadEventFoundationService { }
internal sealed class LeadEventProcessingService(ILeadEventFoundationService foundation) : ILeadEventProcessingService
{
    public ValueTask RaiseAddAsync(Lead entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(Lead entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(Lead entity) => foundation.RaiseDeleteAsync(entity);
}

public interface ILeadOrchestrationService
{
    IQueryable<Lead> RetrieveAll();
    IQueryable<Lead> RetrieveWriteable();
    ValueTask<Lead> AddAsync(Lead entity, CancellationToken cancellationToken = default);
    ValueTask<Lead> ModifyAsync(Lead entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(Lead entity, CancellationToken cancellationToken = default);
}
internal sealed class LeadOrchestrationService(ILeadProcessingService processing, ILeadEventProcessingService events) : ILeadOrchestrationService
{
    public IQueryable<Lead> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<Lead> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<Lead> AddAsync(Lead entity, CancellationToken cancellationToken = default) { Lead persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<Lead> ModifyAsync(Lead entity, CancellationToken cancellationToken = default) { Lead persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(Lead entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
