using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Brokers.Storages;

public interface ICompanyBroker
{
    IQueryable<Company> GetAllCompanies(bool ignoreFilters);

    ValueTask<Company> AddCompanyAsync(Company entity);

    ValueTask<Company> UpdateCompanyAsync(Company entity);

    ValueTask<int> DeleteCompanyAsync(Company entity);
}
