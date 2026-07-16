using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface ICompanyContactStorageBroker
{
    IQueryable<CompanyContact> SelectAll();
    ValueTask<CompanyContact> InsertAsync(CompanyContact entity, CancellationToken cancellationToken = default);
    ValueTask<CompanyContact> UpdateAsync(CompanyContact entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(CompanyContact entity, CancellationToken cancellationToken = default);
}

internal sealed class CompanyContactStorageBroker : ICompanyContactStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public CompanyContactStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<CompanyContact> SelectAll() => context.Set<CompanyContact>();
    public async ValueTask<CompanyContact> InsertAsync(CompanyContact entity, CancellationToken cancellationToken = default) { context.Set<CompanyContact>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<CompanyContact> UpdateAsync(CompanyContact entity, CancellationToken cancellationToken = default) { CompanyContact local = context.Set<CompanyContact>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<CompanyContact>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(CompanyContact entity, CancellationToken cancellationToken = default) { context.Set<CompanyContact>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface ICompanyContactFoundationService
{
    IQueryable<CompanyContact> RetrieveAll();
    IQueryable<CompanyContact> RetrieveWriteable();
    ValueTask<CompanyContact> AddAsync(CompanyContact entity, CancellationToken cancellationToken = default);
    ValueTask<CompanyContact> ModifyAsync(CompanyContact entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(CompanyContact entity, CancellationToken cancellationToken = default);
}

internal sealed class CompanyContactFoundationService(
    ICompanyContactStorageBroker broker,
    ICompanyOrchestrationService companies,
    ICRMAuthInfo auth) : ICompanyContactFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<CompanyContact> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<CompanyContact> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<CompanyContact> AddAsync(CompanyContact entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CompanyContact storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        CompanyContact persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<CompanyContact> ModifyAsync(CompanyContact entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        CompanyContact existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        CompanyContact storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        CompanyContact persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(CompanyContact entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        CompanyContact existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<CompanyContact> Scope(IQueryable<CompanyContact> source, string[] tenants) =>
        source.Where(item => companies.RetrieveAll().Any(company => company.Id == item.CompanyId));

    static CompanyContact Copy(CompanyContact source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            LegacyId = source.LegacyId,
            CompanyId = source.CompanyId,
            SourceSystem = source.SourceSystem,
            IsVerified = source.IsVerified,
            IsPrimary = source.IsPrimary,
            Name = source.Name,
            Position = source.Position,
            EmailAddress = source.EmailAddress,
            PhoneNumber = source.PhoneNumber,
            LinkedInUrl = source.LinkedInUrl,
            Notes = source.Notes,
    };

    static void CopyPersisted(CompanyContact source, CompanyContact target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.LegacyId = source.LegacyId;
        target.CompanyId = source.CompanyId;
        target.SourceSystem = source.SourceSystem;
        target.IsVerified = source.IsVerified;
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

public interface ICompanyContactProcessingService : ICompanyContactFoundationService { }
internal sealed class CompanyContactProcessingService(ICompanyContactFoundationService foundation) : ICompanyContactProcessingService
{
    public IQueryable<CompanyContact> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<CompanyContact> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<CompanyContact> AddAsync(CompanyContact entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<CompanyContact> ModifyAsync(CompanyContact entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(CompanyContact entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface ICompanyContactEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<CompanyContact> message);
    ValueTask RaiseUpdateAsync(EventMessage<CompanyContact> message);
    ValueTask RaiseDeleteAsync(EventMessage<CompanyContact> message);
}
internal sealed class CompanyContactEventBroker(IEventHub eventHub) : ICompanyContactEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<CompanyContact> message) => eventHub.RaiseEventAsync("company_contact_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<CompanyContact> message) => eventHub.RaiseEventAsync("company_contact_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<CompanyContact> message) => eventHub.RaiseEventAsync("company_contact_delete", message);
}
public interface ICompanyContactEventFoundationService
{
    ValueTask RaiseAddAsync(CompanyContact entity);
    ValueTask RaiseUpdateAsync(CompanyContact entity);
    ValueTask RaiseDeleteAsync(CompanyContact entity);
}
internal sealed class CompanyContactEventFoundationService(ICompanyContactEventBroker broker, ICRMAuthInfo auth) : ICompanyContactEventFoundationService
{
    EventMessage<CompanyContact> Message(CompanyContact entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(CompanyContact entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(CompanyContact entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(CompanyContact entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface ICompanyContactEventProcessingService : ICompanyContactEventFoundationService { }
internal sealed class CompanyContactEventProcessingService(ICompanyContactEventFoundationService foundation) : ICompanyContactEventProcessingService
{
    public ValueTask RaiseAddAsync(CompanyContact entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(CompanyContact entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(CompanyContact entity) => foundation.RaiseDeleteAsync(entity);
}

public interface ICompanyContactOrchestrationService
{
    IQueryable<CompanyContact> RetrieveAll();
    IQueryable<CompanyContact> RetrieveWriteable();
    ValueTask<CompanyContact> AddAsync(CompanyContact entity, CancellationToken cancellationToken = default);
    ValueTask<CompanyContact> ModifyAsync(CompanyContact entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(CompanyContact entity, CancellationToken cancellationToken = default);
}
internal sealed class CompanyContactOrchestrationService(ICompanyContactProcessingService processing, ICompanyContactEventProcessingService events) : ICompanyContactOrchestrationService
{
    public IQueryable<CompanyContact> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<CompanyContact> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<CompanyContact> AddAsync(CompanyContact entity, CancellationToken cancellationToken = default) { CompanyContact persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<CompanyContact> ModifyAsync(CompanyContact entity, CancellationToken cancellationToken = default) { CompanyContact persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(CompanyContact entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
