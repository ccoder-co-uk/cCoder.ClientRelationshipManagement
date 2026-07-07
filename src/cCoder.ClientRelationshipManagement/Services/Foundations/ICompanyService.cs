using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Foundations;

public interface ICompanyService
{
    Company Get(Guid id, bool ignoreFilters = false);

    IQueryable<Company> GetAll(bool ignoreFilters = false);

    ValueTask<Company> AddAsync(Company company);

    ValueTask<Company> UpdateAsync(Company company);

    ValueTask DeleteAsync(Guid id);
}
