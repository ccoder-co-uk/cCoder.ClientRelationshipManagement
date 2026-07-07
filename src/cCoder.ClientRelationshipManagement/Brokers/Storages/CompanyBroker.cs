using cCoder.ClientRelationshipManagement.Data;
using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Brokers.Storages;

public class CompanyBroker(ICRMContextFactory contextFactory) : ICompanyBroker
{
    public IQueryable<Company> GetAllCompanies(bool ignoreFilters)
    {
        ClientRelationshipManagementDbContext context = contextFactory.CreateContext();

        return ignoreFilters ? context.Companies.IgnoreQueryFilters() : context.Companies;
    }

    public async ValueTask<Company> AddCompanyAsync(Company entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        Company result = (await context.Companies.AddAsync(entity)).Entity;
        await context.SaveChangesAsync();
        return result;
    }

    public async ValueTask<Company> UpdateCompanyAsync(Company entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        Company result = context.Companies.Update(entity).Entity;
        await context.SaveChangesAsync();
        return result;
    }

    public async ValueTask<int> DeleteCompanyAsync(Company entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        context.Companies.Remove(entity);
        return await context.SaveChangesAsync();
    }
}
