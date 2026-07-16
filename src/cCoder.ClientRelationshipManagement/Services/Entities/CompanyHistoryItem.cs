using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface ICompanyHistoryItemStorageBroker
{
    IQueryable<CompanyHistoryItem> SelectAll();
    ValueTask<CompanyHistoryItem> InsertAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default);
    ValueTask<CompanyHistoryItem> UpdateAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default);
}

internal sealed class CompanyHistoryItemStorageBroker : ICompanyHistoryItemStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public CompanyHistoryItemStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<CompanyHistoryItem> SelectAll() => context.Set<CompanyHistoryItem>();
    public async ValueTask<CompanyHistoryItem> InsertAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default) { context.Set<CompanyHistoryItem>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<CompanyHistoryItem> UpdateAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default) { CompanyHistoryItem local = context.Set<CompanyHistoryItem>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<CompanyHistoryItem>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default) { context.Set<CompanyHistoryItem>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface ICompanyHistoryItemFoundationService
{
    IQueryable<CompanyHistoryItem> RetrieveAll();
    IQueryable<CompanyHistoryItem> RetrieveWriteable();
    ValueTask<CompanyHistoryItem> AddAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default);
    ValueTask<CompanyHistoryItem> ModifyAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default);
}

internal sealed class CompanyHistoryItemFoundationService(ICompanyHistoryItemStorageBroker broker, ICRMAuthInfo auth) : ICompanyHistoryItemFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<CompanyHistoryItem> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<CompanyHistoryItem> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<CompanyHistoryItem> AddAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (string.IsNullOrWhiteSpace(entity.TenantId)) entity.TenantId = Writeable.FirstOrDefault() ?? throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        if (!Writeable.Contains(entity.TenantId)) throw new UnauthorizedAccessException($"The user cannot write tenant '{entity.TenantId}'.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompanyHistoryItem storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        CompanyHistoryItem persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<CompanyHistoryItem> ModifyAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        CompanyHistoryItem existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        CompanyHistoryItem storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        CompanyHistoryItem persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        CompanyHistoryItem existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<CompanyHistoryItem> Scope(IQueryable<CompanyHistoryItem> source, string[] tenants) => source.Where(item => tenants.Contains(item.TenantId));

    static CompanyHistoryItem Copy(CompanyHistoryItem source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            CompanyId = source.CompanyId,
            TenantId = source.TenantId,
            OccurredOn = source.OccurredOn,
            Lane = source.Lane,
            EventType = source.EventType,
            Summary = source.Summary,
            Details = source.Details,
            FactKey = source.FactKey,
            FactValue = source.FactValue,
            Confidence = source.Confidence,
            SourceType = source.SourceType,
            SourceId = source.SourceId,
            ProcessDefinitionId = source.ProcessDefinitionId,
            ProcessInstanceId = source.ProcessInstanceId,
            ProcessStepId = source.ProcessStepId,
            ProcessTaskId = source.ProcessTaskId,
            IsPrivate = source.IsPrivate,
    };

    static void CopyPersisted(CompanyHistoryItem source, CompanyHistoryItem target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.CompanyId = source.CompanyId;
        target.TenantId = source.TenantId;
        target.OccurredOn = source.OccurredOn;
        target.Lane = source.Lane;
        target.EventType = source.EventType;
        target.Summary = source.Summary;
        target.Details = source.Details;
        target.FactKey = source.FactKey;
        target.FactValue = source.FactValue;
        target.Confidence = source.Confidence;
        target.SourceType = source.SourceType;
        target.SourceId = source.SourceId;
        target.ProcessDefinitionId = source.ProcessDefinitionId;
        target.ProcessInstanceId = source.ProcessInstanceId;
        target.ProcessStepId = source.ProcessStepId;
        target.ProcessTaskId = source.ProcessTaskId;
        target.IsPrivate = source.IsPrivate;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface ICompanyHistoryItemProcessingService : ICompanyHistoryItemFoundationService { }
internal sealed class CompanyHistoryItemProcessingService(ICompanyHistoryItemFoundationService foundation) : ICompanyHistoryItemProcessingService
{
    public IQueryable<CompanyHistoryItem> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<CompanyHistoryItem> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<CompanyHistoryItem> AddAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<CompanyHistoryItem> ModifyAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface ICompanyHistoryItemEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<CompanyHistoryItem> message);
    ValueTask RaiseUpdateAsync(EventMessage<CompanyHistoryItem> message);
    ValueTask RaiseDeleteAsync(EventMessage<CompanyHistoryItem> message);
}
internal sealed class CompanyHistoryItemEventBroker(IEventHub eventHub) : ICompanyHistoryItemEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<CompanyHistoryItem> message) => eventHub.RaiseEventAsync("company_history_item_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<CompanyHistoryItem> message) => eventHub.RaiseEventAsync("company_history_item_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<CompanyHistoryItem> message) => eventHub.RaiseEventAsync("company_history_item_delete", message);
}
public interface ICompanyHistoryItemEventFoundationService
{
    ValueTask RaiseAddAsync(CompanyHistoryItem entity);
    ValueTask RaiseUpdateAsync(CompanyHistoryItem entity);
    ValueTask RaiseDeleteAsync(CompanyHistoryItem entity);
}
internal sealed class CompanyHistoryItemEventFoundationService(ICompanyHistoryItemEventBroker broker, ICRMAuthInfo auth) : ICompanyHistoryItemEventFoundationService
{
    EventMessage<CompanyHistoryItem> Message(CompanyHistoryItem entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(CompanyHistoryItem entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(CompanyHistoryItem entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(CompanyHistoryItem entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface ICompanyHistoryItemEventProcessingService : ICompanyHistoryItemEventFoundationService { }
internal sealed class CompanyHistoryItemEventProcessingService(ICompanyHistoryItemEventFoundationService foundation) : ICompanyHistoryItemEventProcessingService
{
    public ValueTask RaiseAddAsync(CompanyHistoryItem entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(CompanyHistoryItem entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(CompanyHistoryItem entity) => foundation.RaiseDeleteAsync(entity);
}

public interface ICompanyHistoryItemOrchestrationService
{
    IQueryable<CompanyHistoryItem> RetrieveAll();
    IQueryable<CompanyHistoryItem> RetrieveWriteable();
    ValueTask<CompanyHistoryItem> AddAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default);
    ValueTask<CompanyHistoryItem> ModifyAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default);
}
internal sealed class CompanyHistoryItemOrchestrationService(ICompanyHistoryItemProcessingService processing, ICompanyHistoryItemEventProcessingService events) : ICompanyHistoryItemOrchestrationService
{
    public IQueryable<CompanyHistoryItem> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<CompanyHistoryItem> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<CompanyHistoryItem> AddAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default) { CompanyHistoryItem persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<CompanyHistoryItem> ModifyAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default) { CompanyHistoryItem persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(CompanyHistoryItem entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
