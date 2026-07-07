using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Orchestrations;

public interface ICompanyOrchestrationService
{
    Company Get(Guid id, bool ignoreFilters = false);
    IQueryable<Company> GetAll(bool ignoreFilters = false);
    ValueTask<Company> AddAsync(Company company);
    ValueTask<Company> UpdateAsync(Company company);
    ValueTask<Company> UpsertAsync(Company company);
    ValueTask DeleteAsync(Guid id);
}
