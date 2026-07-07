using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Processings;

namespace cCoder.ClientRelationshipManagement.Services.Orchestrations;

internal class CompanyOrchestrationService(
    ICompanyProcessingService companyService,
    IClientProcessingService clientService,
    IAuthorizationProcessingService authorizationService)
    : ICompanyOrchestrationService
{
    public Company Get(Guid id, bool ignoreFilters = false)
    {
        Company company = companyService.Get(id, ignoreFilters: true);
        if (company is null)
            return null;

        authorizationService.Authorize(ResolveTenantId(company), "client_read");
        return ignoreFilters ? company : companyService.Get(id);
    }

    public IQueryable<Company> GetAll(bool ignoreFilters = false)
    {
        authorizationService.AuthorizeAny("client_read");
        return companyService.GetAll(ignoreFilters);
    }

    public async ValueTask<Company> AddAsync(Company company)
    {
        authorizationService.Authorize(ResolveTenantId(company), "client_write");
        return await companyService.AddAsync(company);
    }

    public async ValueTask<Company> UpdateAsync(Company company)
    {
        authorizationService.Authorize(ResolveTenantId(company), "client_write");
        return await companyService.UpdateAsync(company);
    }

    public async ValueTask<Company> UpsertAsync(Company company)
    {
        Company existingCompany = company is not null && company.Id != Guid.Empty
            ? companyService.Get(company.Id, ignoreFilters: true)
            : null;

        authorizationService.Authorize(
            ResolveTenantId(company ?? existingCompany),
            "client_write");

        return await companyService.UpsertAsync(company);
    }

    public async ValueTask DeleteAsync(Guid id)
    {
        Company company = companyService.Get(id, ignoreFilters: true);
        if (company is null)
            return;

        authorizationService.Authorize(ResolveTenantId(company), "client_write");
        await companyService.DeleteAsync(id);
    }

    string ResolveTenantId(Company company) =>
        !string.IsNullOrWhiteSpace(company?.Client?.TenantId)
            ? company.Client.TenantId
            : company?.ClientId is Guid clientId && clientId != Guid.Empty
                ? clientService.Get(clientId, ignoreFilters: true)?.TenantId
                : null;
}
