using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Foundations;

namespace cCoder.ClientRelationshipManagement.Services.Processings;

internal class CompanyProcessingService(ICompanyService companyService) : ICompanyProcessingService
{
    public Company Get(Guid id, bool ignoreFilters = false) => companyService.Get(id, ignoreFilters);
    public IQueryable<Company> GetAll(bool ignoreFilters = false) => companyService.GetAll(ignoreFilters);
    public ValueTask<Company> AddAsync(Company company) => companyService.AddAsync(company);
    public ValueTask<Company> UpdateAsync(Company company) => companyService.UpdateAsync(company);
    public ValueTask DeleteAsync(Guid id) => companyService.DeleteAsync(id);

    public async ValueTask<Company> UpsertAsync(Company company)
    {
        ArgumentNullException.ThrowIfNull(company, nameof(company));

        return company.Id != Guid.Empty && Get(company.Id, ignoreFilters: true) is not null
            ? await UpdateAsync(company)
            : await AddAsync(company);
    }
}
