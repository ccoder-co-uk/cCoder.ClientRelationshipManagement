using cCoder.ClientRelationshipManagement.Brokers.Events;
using cCoder.ClientRelationshipManagement.Brokers.Storages;
using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Services.Foundations;

internal partial class CompanyService(
    ICompanyBroker companyBroker,
    ICompanyEventBroker companyEventBroker,
    ICRMAuthInfo authInfo)
    : ICompanyService
{
    public Company Get(Guid id, bool ignoreFilters = false) =>
        companyBroker.GetAllCompanies(ignoreFilters)
            .FirstOrDefault(company => company.Id == id);

    public IQueryable<Company> GetAll(bool ignoreFilters = false) =>
        companyBroker.GetAllCompanies(ignoreFilters);

    public async ValueTask<Company> AddAsync(Company company)
    {
        ValidateCompany(company, nameof(company));

        if (company.Id == Guid.Empty)
            company.Id = Guid.NewGuid();

        if (Get(company.Id, ignoreFilters: true) is not null)
            throw new InvalidOperationException($"Company '{company.Id}' already exists.");

        Guid clientId = company.ClientId == Guid.Empty && company.Client is not null
            ? company.Client.Id
            : company.ClientId;

        if (clientId == Guid.Empty)
            throw new ArgumentException("A company must be associated with a client.", nameof(company));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Company storageCompany = new()
        {
            Id = company.Id,
            ClientId = clientId,
            Name = company.Name,
            LegalEntityName = company.LegalEntityName,
            TradingName = company.TradingName,
            CompanyNumber = company.CompanyNumber,
            VatNumber = company.VatNumber,
            ContactEmailAddress = company.ContactEmailAddress,
            ContactPhoneNumber = company.ContactPhoneNumber,
            WebsiteUrl = company.WebsiteUrl,
            CreatedBy = company.CreatedBy ?? authInfo.SSOUserId,
            LastUpdatedBy = authInfo.SSOUserId,
            CreatedOn = company.CreatedOn == default ? now : company.CreatedOn,
            LastUpdated = now,
            IsActive = company.IsActive,
            IsVerified = company.IsVerified,
            RegisteredAddressId = company.RegisteredAddress is null ? company.RegisteredAddressId : null,
        };

        Company result = await companyBroker.AddCompanyAsync(storageCompany);
        result.Client = company.Client;
        result.RegisteredAddress = company.RegisteredAddress;
        await companyEventBroker.RaiseCompanyAddEventAsync(CreateEventMessage(result));
        return result;
    }

    public async ValueTask<Company> UpdateAsync(Company company)
    {
        ValidateCompany(company, nameof(company));

        Guid clientId = company.ClientId == Guid.Empty && company.Client is not null
            ? company.Client.Id
            : company.ClientId;

        if (clientId == Guid.Empty)
            throw new ArgumentException("A company must be associated with a client.", nameof(company));

        Company storageCompany = new()
        {
            Id = company.Id,
            ClientId = clientId,
            Name = company.Name,
            LegalEntityName = company.LegalEntityName,
            TradingName = company.TradingName,
            CompanyNumber = company.CompanyNumber,
            VatNumber = company.VatNumber,
            ContactEmailAddress = company.ContactEmailAddress,
            ContactPhoneNumber = company.ContactPhoneNumber,
            WebsiteUrl = company.WebsiteUrl,
            CreatedBy = company.CreatedBy,
            LastUpdatedBy = authInfo.SSOUserId,
            CreatedOn = company.CreatedOn,
            LastUpdated = DateTimeOffset.UtcNow,
            IsActive = company.IsActive,
            IsVerified = company.IsVerified,
            RegisteredAddressId = company.RegisteredAddress is null ? company.RegisteredAddressId : null,
        };

        Company result = await companyBroker.UpdateCompanyAsync(storageCompany);
        result.Client = company.Client;
        result.RegisteredAddress = company.RegisteredAddress;
        result.RegisteredAddressId = company.RegisteredAddressId;
        await companyEventBroker.RaiseCompanyUpdateEventAsync(CreateEventMessage(result));
        return result;
    }

    public async ValueTask DeleteAsync(Guid id)
    {
        Company company = Get(id, ignoreFilters: true);

        if (company is null)
            return;

        await companyEventBroker.RaiseCompanyDeleteEventAsync(CreateEventMessage(company));
        await companyBroker.DeleteCompanyAsync(company);
    }

    EventMessage<Company> CreateEventMessage(Company company) =>
        new()
        {
            AuthInfo = new EventAuthInfo
            {
                SSOUserId = authInfo.SSOUserId,
            },
            Data = company,
        };

    static void ValidateCompany(Company company, string parameterName) =>
        ArgumentNullException.ThrowIfNull(company, parameterName);
}
