using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface ICompanyEvidenceStorageBroker
{
    IQueryable<CompanyEvidence> SelectAll();
    ValueTask<CompanyEvidence> InsertAsync(CompanyEvidence entity, CancellationToken cancellationToken = default);
    ValueTask<CompanyEvidence> UpdateAsync(CompanyEvidence entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(CompanyEvidence entity, CancellationToken cancellationToken = default);
}

internal sealed class CompanyEvidenceStorageBroker(ClientRelationshipDbContext context) : ICompanyEvidenceStorageBroker
{
    public IQueryable<CompanyEvidence> SelectAll() => context.CompanyEvidence;
    public async ValueTask<CompanyEvidence> InsertAsync(CompanyEvidence entity, CancellationToken cancellationToken = default) { context.CompanyEvidence.Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<CompanyEvidence> UpdateAsync(CompanyEvidence entity, CancellationToken cancellationToken = default) { context.CompanyEvidence.Update(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(CompanyEvidence entity, CancellationToken cancellationToken = default) { context.CompanyEvidence.Remove(entity); await context.SaveChangesAsync(cancellationToken); }
}

public interface ICompanyEvidenceFoundationService
{
    IQueryable<CompanyEvidence> RetrieveAll();
    IQueryable<CompanyEvidence> RetrieveWriteable();
    ValueTask<CompanyEvidence> AddAsync(CompanyEvidence entity, CancellationToken cancellationToken = default);
    ValueTask<CompanyEvidence> ModifyAsync(CompanyEvidence entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(CompanyEvidence entity, CancellationToken cancellationToken = default);
}

internal sealed class CompanyEvidenceFoundationService(ICompanyEvidenceStorageBroker broker, ICRMAuthInfo auth, ClientRelationshipDbContext context) : ICompanyEvidenceFoundationService
{
    public IQueryable<CompanyEvidence> RetrieveAll() => Scope(broker.SelectAll(), auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? []);
    public IQueryable<CompanyEvidence> RetrieveWriteable() => Scope(broker.SelectAll(), auth.WriteableTenants ?? []);

    public async ValueTask<CompanyEvidence> AddAsync(CompanyEvidence entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        bool canWriteCompany = await CanWriteCompanyAsync(entity.CompanyId, cancellationToken);
        if (!canWriteCompany) throw new UnauthorizedAccessException("The company is outside the requesting user's write scope.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.CreatedOn = now;
        entity.CreatedBy = auth.SSOUserId;
        entity.LastUpdated = now;
        entity.LastUpdatedBy = auth.SSOUserId;
        return await broker.InsertAsync(entity, cancellationToken);
    }

    public async ValueTask<CompanyEvidence> ModifyAsync(CompanyEvidence entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        CompanyEvidence existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The evidence is outside the requesting user's write scope.");
        entity.CreatedOn = existing.CreatedOn;
        entity.CreatedBy = existing.CreatedBy;
        entity.LastUpdated = DateTimeOffset.UtcNow;
        entity.LastUpdatedBy = auth.SSOUserId;
        return await broker.UpdateAsync(entity, cancellationToken);
    }

    public async ValueTask RemoveAsync(CompanyEvidence entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        CompanyEvidence existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The evidence is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    ValueTask<bool> CanWriteCompanyAsync(Guid companyId, CancellationToken cancellationToken)
    {
        string[] tenants = auth.WriteableTenants ?? [];

        return new(context.Companies.AnyAsync(
            company => company.Id == companyId
                && company.Relationships.Any(relationship => tenants.Contains(relationship.TenantId)),
            cancellationToken));
    }

    static IQueryable<CompanyEvidence> Scope(IQueryable<CompanyEvidence> source, string[] tenants) =>
        source.Where(item => item.Company.Relationships.Any(relationship => tenants.Contains(relationship.TenantId)));

    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface ICompanyEvidenceProcessingService : ICompanyEvidenceFoundationService { }

internal sealed class CompanyEvidenceProcessingService(ICompanyEvidenceFoundationService foundation) : ICompanyEvidenceProcessingService
{
    public IQueryable<CompanyEvidence> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<CompanyEvidence> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<CompanyEvidence> AddAsync(CompanyEvidence entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<CompanyEvidence> ModifyAsync(CompanyEvidence entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(CompanyEvidence entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface ICompanyEvidenceEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<CompanyEvidence> message);
    ValueTask RaiseUpdateAsync(EventMessage<CompanyEvidence> message);
    ValueTask RaiseDeleteAsync(EventMessage<CompanyEvidence> message);
}

internal sealed class CompanyEvidenceEventBroker(IEventHub eventHub) : ICompanyEvidenceEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<CompanyEvidence> message) => eventHub.RaiseEventAsync("company_evidence_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<CompanyEvidence> message) => eventHub.RaiseEventAsync("company_evidence_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<CompanyEvidence> message) => eventHub.RaiseEventAsync("company_evidence_delete", message);
}

public interface ICompanyEvidenceEventFoundationService
{
    ValueTask RaiseAddAsync(CompanyEvidence entity);
    ValueTask RaiseUpdateAsync(CompanyEvidence entity);
    ValueTask RaiseDeleteAsync(CompanyEvidence entity);
}

internal sealed class CompanyEvidenceEventFoundationService(ICompanyEvidenceEventBroker broker, ICRMAuthInfo auth) : ICompanyEvidenceEventFoundationService
{
    EventMessage<CompanyEvidence> CreateMessage(CompanyEvidence entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(CompanyEvidence entity) => broker.RaiseAddAsync(CreateMessage(entity));
    public ValueTask RaiseUpdateAsync(CompanyEvidence entity) => broker.RaiseUpdateAsync(CreateMessage(entity));
    public ValueTask RaiseDeleteAsync(CompanyEvidence entity) => broker.RaiseDeleteAsync(CreateMessage(entity));
}

public interface ICompanyEvidenceEventProcessingService : ICompanyEvidenceEventFoundationService { }

internal sealed class CompanyEvidenceEventProcessingService(ICompanyEvidenceEventFoundationService foundation) : ICompanyEvidenceEventProcessingService
{
    public ValueTask RaiseAddAsync(CompanyEvidence entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(CompanyEvidence entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(CompanyEvidence entity) => foundation.RaiseDeleteAsync(entity);
}

public interface ICompanyEvidenceOrchestrationService
{
    IQueryable<CompanyEvidence> RetrieveAll();
    IQueryable<CompanyEvidence> RetrieveWriteable();
    ValueTask<CompanyEvidence> AddAsync(CompanyEvidence entity, CancellationToken cancellationToken = default);
    ValueTask<CompanyEvidence> ModifyAsync(CompanyEvidence entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(CompanyEvidence entity, CancellationToken cancellationToken = default);
}

internal sealed class CompanyEvidenceOrchestrationService(ICompanyEvidenceProcessingService processing, ICompanyEvidenceEventProcessingService events) : ICompanyEvidenceOrchestrationService
{
    public IQueryable<CompanyEvidence> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<CompanyEvidence> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<CompanyEvidence> AddAsync(CompanyEvidence entity, CancellationToken cancellationToken = default) { CompanyEvidence persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<CompanyEvidence> ModifyAsync(CompanyEvidence entity, CancellationToken cancellationToken = default) { CompanyEvidence persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(CompanyEvidence entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
