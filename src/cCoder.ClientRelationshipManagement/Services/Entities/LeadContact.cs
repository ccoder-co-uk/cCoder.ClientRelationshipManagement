using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface ILeadContactStorageBroker
{
    IQueryable<LeadContact> SelectAll();
    ValueTask<LeadContact> InsertAsync(LeadContact entity, CancellationToken cancellationToken = default);
    ValueTask<LeadContact> UpdateAsync(LeadContact entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(LeadContact entity, CancellationToken cancellationToken = default);
}

internal sealed class LeadContactStorageBroker : ILeadContactStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public LeadContactStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<LeadContact> SelectAll() => context.Set<LeadContact>();
    public async ValueTask<LeadContact> InsertAsync(LeadContact entity, CancellationToken cancellationToken = default) { context.Set<LeadContact>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<LeadContact> UpdateAsync(LeadContact entity, CancellationToken cancellationToken = default) { LeadContact local = context.Set<LeadContact>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<LeadContact>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(LeadContact entity, CancellationToken cancellationToken = default) { context.Set<LeadContact>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface ILeadContactFoundationService
{
    IQueryable<LeadContact> RetrieveAll();
    IQueryable<LeadContact> RetrieveWriteable();
    ValueTask<LeadContact> AddAsync(LeadContact entity, CancellationToken cancellationToken = default);
    ValueTask<LeadContact> ModifyAsync(LeadContact entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(LeadContact entity, CancellationToken cancellationToken = default);
}

internal sealed class LeadContactFoundationService(ILeadContactStorageBroker broker, ICRMAuthInfo auth) : ILeadContactFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<LeadContact> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<LeadContact> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<LeadContact> AddAsync(LeadContact entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        LeadContact storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        LeadContact persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<LeadContact> ModifyAsync(LeadContact entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        LeadContact existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        LeadContact storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        LeadContact persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(LeadContact entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        LeadContact existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<LeadContact> Scope(IQueryable<LeadContact> source, string[] tenants) => source.Where(item => tenants.Contains(item.Lead.TenantId));

    static LeadContact Copy(LeadContact source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            LeadId = source.LeadId,
            IsPrimary = source.IsPrimary,
            Name = source.Name,
            Position = source.Position,
            EmailAddress = source.EmailAddress,
            PhoneNumber = source.PhoneNumber,
            LinkedInUrl = source.LinkedInUrl,
            Notes = source.Notes,
    };

    static void CopyPersisted(LeadContact source, LeadContact target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.LeadId = source.LeadId;
        target.IsPrimary = source.IsPrimary;
        target.Name = source.Name;
        target.Position = source.Position;
        target.EmailAddress = source.EmailAddress;
        target.PhoneNumber = source.PhoneNumber;
        target.LinkedInUrl = source.LinkedInUrl;
        target.Notes = source.Notes;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface ILeadContactProcessingService : ILeadContactFoundationService { }
internal sealed class LeadContactProcessingService(ILeadContactFoundationService foundation) : ILeadContactProcessingService
{
    public IQueryable<LeadContact> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<LeadContact> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<LeadContact> AddAsync(LeadContact entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<LeadContact> ModifyAsync(LeadContact entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(LeadContact entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface ILeadContactEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<LeadContact> message);
    ValueTask RaiseUpdateAsync(EventMessage<LeadContact> message);
    ValueTask RaiseDeleteAsync(EventMessage<LeadContact> message);
}
internal sealed class LeadContactEventBroker(IEventHub eventHub) : ILeadContactEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<LeadContact> message) => eventHub.RaiseEventAsync("lead_contact_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<LeadContact> message) => eventHub.RaiseEventAsync("lead_contact_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<LeadContact> message) => eventHub.RaiseEventAsync("lead_contact_delete", message);
}
public interface ILeadContactEventFoundationService
{
    ValueTask RaiseAddAsync(LeadContact entity);
    ValueTask RaiseUpdateAsync(LeadContact entity);
    ValueTask RaiseDeleteAsync(LeadContact entity);
}
internal sealed class LeadContactEventFoundationService(ILeadContactEventBroker broker, ICRMAuthInfo auth) : ILeadContactEventFoundationService
{
    EventMessage<LeadContact> Message(LeadContact entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(LeadContact entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(LeadContact entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(LeadContact entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface ILeadContactEventProcessingService : ILeadContactEventFoundationService { }
internal sealed class LeadContactEventProcessingService(ILeadContactEventFoundationService foundation) : ILeadContactEventProcessingService
{
    public ValueTask RaiseAddAsync(LeadContact entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(LeadContact entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(LeadContact entity) => foundation.RaiseDeleteAsync(entity);
}

public interface ILeadContactOrchestrationService
{
    IQueryable<LeadContact> RetrieveAll();
    IQueryable<LeadContact> RetrieveWriteable();
    ValueTask<LeadContact> AddAsync(LeadContact entity, CancellationToken cancellationToken = default);
    ValueTask<LeadContact> ModifyAsync(LeadContact entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(LeadContact entity, CancellationToken cancellationToken = default);
}
internal sealed class LeadContactOrchestrationService(ILeadContactProcessingService processing, ILeadContactEventProcessingService events) : ILeadContactOrchestrationService
{
    public IQueryable<LeadContact> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<LeadContact> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<LeadContact> AddAsync(LeadContact entity, CancellationToken cancellationToken = default) { LeadContact persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<LeadContact> ModifyAsync(LeadContact entity, CancellationToken cancellationToken = default) { LeadContact persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(LeadContact entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
