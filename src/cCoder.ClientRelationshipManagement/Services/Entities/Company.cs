using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.Eventing;
using cCoder.Eventing.Models;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Entities;

public interface ICompanyStorageBroker
{
    IQueryable<Company> SelectAll();
    ValueTask<Company> InsertAsync(Company entity, CancellationToken cancellationToken = default);
    ValueTask<Company> UpdateAsync(Company entity, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(Company entity, CancellationToken cancellationToken = default);
}

internal sealed class CompanyStorageBroker : ICompanyStorageBroker
{
    readonly ClientRelationshipDbContext context;
    public CompanyStorageBroker(ClientRelationshipDbContext context) => this.context = context;
    public IQueryable<Company> SelectAll() => context.Set<Company>();
    public async ValueTask<Company> InsertAsync(Company entity, CancellationToken cancellationToken = default) { context.Set<Company>().Add(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask<Company> UpdateAsync(Company entity, CancellationToken cancellationToken = default) { Company local = context.Set<Company>().Local.FirstOrDefault(item => item.Id == entity.Id); if (local is null) context.Set<Company>().Update(entity); else context.Entry(local).CurrentValues.SetValues(entity); await context.SaveChangesAsync(cancellationToken); return entity; }
    public async ValueTask DeleteAsync(Company entity, CancellationToken cancellationToken = default) { context.Set<Company>().Remove(entity); await context.SaveChangesAsync(cancellationToken); }

}

public interface ICompanyFoundationService
{
    IQueryable<Company> RetrieveAll();
    IQueryable<Company> RetrieveWriteable();
    ValueTask<Company> AddAsync(Company entity, CancellationToken cancellationToken = default);
    ValueTask<Company> ModifyAsync(Company entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(Company entity, CancellationToken cancellationToken = default);
}

internal sealed class CompanyFoundationService(
    ICompanyStorageBroker broker,
    ILeadOrchestrationService leads,
    ICRMAuthInfo auth) : ICompanyFoundationService
{
    string[] Readable => auth.ReadableTenants?.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants ?? [];
    string[] Writeable => auth.WriteableTenants ?? [];
    public IQueryable<Company> RetrieveAll() => Scope(broker.SelectAll(), Readable);
    public IQueryable<Company> RetrieveWriteable() => Scope(broker.SelectAll(), Writeable);

    public async ValueTask<Company> AddAsync(Company entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (Writeable.Length == 0) throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Company storage = Copy(entity);
        storage.CreatedOn = now;
        storage.CreatedBy = auth.SSOUserId;
        storage.LastUpdated = now;
        storage.LastUpdatedBy = auth.SSOUserId;
        Company persisted = await broker.InsertAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask<Company> ModifyAsync(Company entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        Company existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        Company storage = Copy(entity);
        storage.CreatedOn = existing.CreatedOn;
        storage.CreatedBy = existing.CreatedBy;
        storage.LastUpdated = DateTimeOffset.UtcNow;
        storage.LastUpdatedBy = auth.SSOUserId;
        Company persisted = await broker.UpdateAsync(storage, cancellationToken);
        CopyPersisted(persisted, entity);
        return entity;
    }

    public async ValueTask RemoveAsync(Company entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureAuthenticated();
        Company existing = await RetrieveWriteable().SingleOrDefaultAsync(item => item.Id == entity.Id, cancellationToken)
            ?? throw new UnauthorizedAccessException("The entity is outside the requesting user's write scope.");
        await broker.DeleteAsync(existing, cancellationToken);
    }

    IQueryable<Company> Scope(IQueryable<Company> source, string[] tenants) => source.Where(item =>
        item.SourceSystem == "CompaniesHouse"
        || item.Relationships.Any(r => tenants.Contains(r.TenantId))
        || item.History.Any(h => tenants.Contains(h.TenantId))
        || leads.RetrieveAll().Any(lead => lead.CompanyId == item.Id));

    static Company Copy(Company source) => new()
    {
            Id = source.Id,
            CreatedBy = source.CreatedBy,
            LastUpdatedBy = source.LastUpdatedBy,
            CreatedOn = source.CreatedOn,
            LastUpdated = source.LastUpdated,
            LegacyId = source.LegacyId,
            SourceSystem = source.SourceSystem,
            SourceRecordId = source.SourceRecordId,
            AuthorityRecordHash = source.AuthorityRecordHash,
            IsVerified = source.IsVerified,
            OfficialName = source.OfficialName,
            LegalEntityName = source.LegalEntityName,
            TradingName = source.TradingName,
            CompanyNumber = source.CompanyNumber,
            VatNumber = source.VatNumber,
            CompanyCategory = source.CompanyCategory,
            CompanyStatus = source.CompanyStatus,
            CountryOfOrigin = source.CountryOfOrigin,
            IncorporatedOn = source.IncorporatedOn,
            DissolvedOn = source.DissolvedOn,
            PrimarySicCodes = source.PrimarySicCodes,
            RegistryUri = source.RegistryUri,
            PreviousNamesJson = source.PreviousNamesJson,
            WebsiteUrl = source.WebsiteUrl,
            ContactEmailAddress = source.ContactEmailAddress,
            ContactPhoneNumber = source.ContactPhoneNumber,
            ResearchSummary = source.ResearchSummary,
            VerificationNotes = source.VerificationNotes,
            AnnualRevenue = source.AnnualRevenue,
            RevenueCurrency = source.RevenueCurrency,
            EmployeeCount = source.EmployeeCount,
            RankingScore = source.RankingScore,
            RankingRationale = source.RankingRationale,
            IsProspectingSuppressed = source.IsProspectingSuppressed,
            ProspectingSuppressedReason = source.ProspectingSuppressedReason,
            ProspectingSuppressedOn = source.ProspectingSuppressedOn,
            RegisteredAddressId = source.RegisteredAddressId,
    };

    static void CopyPersisted(Company source, Company target)
    {
        target.Id = source.Id;
        target.CreatedBy = source.CreatedBy;
        target.LastUpdatedBy = source.LastUpdatedBy;
        target.CreatedOn = source.CreatedOn;
        target.LastUpdated = source.LastUpdated;
        target.LegacyId = source.LegacyId;
        target.SourceSystem = source.SourceSystem;
        target.SourceRecordId = source.SourceRecordId;
        target.AuthorityRecordHash = source.AuthorityRecordHash;
        target.IsVerified = source.IsVerified;
        target.OfficialName = source.OfficialName;
        target.LegalEntityName = source.LegalEntityName;
        target.TradingName = source.TradingName;
        target.CompanyNumber = source.CompanyNumber;
        target.VatNumber = source.VatNumber;
        target.CompanyCategory = source.CompanyCategory;
        target.CompanyStatus = source.CompanyStatus;
        target.CountryOfOrigin = source.CountryOfOrigin;
        target.IncorporatedOn = source.IncorporatedOn;
        target.DissolvedOn = source.DissolvedOn;
        target.PrimarySicCodes = source.PrimarySicCodes;
        target.RegistryUri = source.RegistryUri;
        target.PreviousNamesJson = source.PreviousNamesJson;
        target.WebsiteUrl = source.WebsiteUrl;
        target.ContactEmailAddress = source.ContactEmailAddress;
        target.ContactPhoneNumber = source.ContactPhoneNumber;
        target.ResearchSummary = source.ResearchSummary;
        target.VerificationNotes = source.VerificationNotes;
        target.AnnualRevenue = source.AnnualRevenue;
        target.RevenueCurrency = source.RevenueCurrency;
        target.EmployeeCount = source.EmployeeCount;
        target.RankingScore = source.RankingScore;
        target.RankingRationale = source.RankingRationale;
        target.IsProspectingSuppressed = source.IsProspectingSuppressed;
        target.ProspectingSuppressedReason = source.ProspectingSuppressedReason;
        target.ProspectingSuppressedOn = source.ProspectingSuppressedOn;
        target.RegisteredAddressId = source.RegisteredAddressId;
    }


    void EnsureAuthenticated()
    {
        if (string.IsNullOrWhiteSpace(auth.SSOUserId) || string.Equals(auth.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A signed-in CRM user is required.");
    }
}

public interface ICompanyProcessingService : ICompanyFoundationService { }
internal sealed class CompanyProcessingService(ICompanyFoundationService foundation) : ICompanyProcessingService
{
    public IQueryable<Company> RetrieveAll() => foundation.RetrieveAll();
    public IQueryable<Company> RetrieveWriteable() => foundation.RetrieveWriteable();
    public ValueTask<Company> AddAsync(Company entity, CancellationToken cancellationToken = default) => foundation.AddAsync(entity, cancellationToken);
    public ValueTask<Company> ModifyAsync(Company entity, CancellationToken cancellationToken = default) => foundation.ModifyAsync(entity, cancellationToken);
    public ValueTask RemoveAsync(Company entity, CancellationToken cancellationToken = default) => foundation.RemoveAsync(entity, cancellationToken);
}

public interface ICompanyEventBroker
{
    ValueTask RaiseAddAsync(EventMessage<Company> message);
    ValueTask RaiseUpdateAsync(EventMessage<Company> message);
    ValueTask RaiseDeleteAsync(EventMessage<Company> message);
}
internal sealed class CompanyEventBroker(IEventHub eventHub) : ICompanyEventBroker
{
    public ValueTask RaiseAddAsync(EventMessage<Company> message) => eventHub.RaiseEventAsync("company_add", message);
    public ValueTask RaiseUpdateAsync(EventMessage<Company> message) => eventHub.RaiseEventAsync("company_update", message);
    public ValueTask RaiseDeleteAsync(EventMessage<Company> message) => eventHub.RaiseEventAsync("company_delete", message);
}
public interface ICompanyEventFoundationService
{
    ValueTask RaiseAddAsync(Company entity);
    ValueTask RaiseUpdateAsync(Company entity);
    ValueTask RaiseDeleteAsync(Company entity);
}
internal sealed class CompanyEventFoundationService(ICompanyEventBroker broker, ICRMAuthInfo auth) : ICompanyEventFoundationService
{
    EventMessage<Company> Message(Company entity) => new() { AuthInfo = new EventAuthInfo { SSOUserId = auth.SSOUserId }, Data = entity };
    public ValueTask RaiseAddAsync(Company entity) => broker.RaiseAddAsync(Message(entity));
    public ValueTask RaiseUpdateAsync(Company entity) => broker.RaiseUpdateAsync(Message(entity));
    public ValueTask RaiseDeleteAsync(Company entity) => broker.RaiseDeleteAsync(Message(entity));
}
public interface ICompanyEventProcessingService : ICompanyEventFoundationService { }
internal sealed class CompanyEventProcessingService(ICompanyEventFoundationService foundation) : ICompanyEventProcessingService
{
    public ValueTask RaiseAddAsync(Company entity) => foundation.RaiseAddAsync(entity);
    public ValueTask RaiseUpdateAsync(Company entity) => foundation.RaiseUpdateAsync(entity);
    public ValueTask RaiseDeleteAsync(Company entity) => foundation.RaiseDeleteAsync(entity);
}

public interface ICompanyOrchestrationService
{
    IQueryable<Company> RetrieveAll();
    IQueryable<Company> RetrieveWriteable();
    ValueTask<Company> AddAsync(Company entity, CancellationToken cancellationToken = default);
    ValueTask<Company> ModifyAsync(Company entity, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(Company entity, CancellationToken cancellationToken = default);
}
internal sealed class CompanyOrchestrationService(ICompanyProcessingService processing, ICompanyEventProcessingService events) : ICompanyOrchestrationService
{
    public IQueryable<Company> RetrieveAll() => processing.RetrieveAll();
    public IQueryable<Company> RetrieveWriteable() => processing.RetrieveWriteable();
    public async ValueTask<Company> AddAsync(Company entity, CancellationToken cancellationToken = default) { Company persisted = await processing.AddAsync(entity, cancellationToken); await events.RaiseAddAsync(persisted); return persisted; }
    public async ValueTask<Company> ModifyAsync(Company entity, CancellationToken cancellationToken = default) { Company persisted = await processing.ModifyAsync(entity, cancellationToken); await events.RaiseUpdateAsync(persisted); return persisted; }
    public async ValueTask RemoveAsync(Company entity, CancellationToken cancellationToken = default) { await processing.RemoveAsync(entity, cancellationToken); await events.RaiseDeleteAsync(entity); }
}
