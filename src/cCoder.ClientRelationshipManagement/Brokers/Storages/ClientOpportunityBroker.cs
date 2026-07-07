using cCoder.ClientRelationshipManagement.Data;
using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Brokers.Storages;

public class ClientOpportunityBroker(ICRMContextFactory contextFactory) : IClientOpportunityBroker
{
    public IQueryable<ClientOpportunity> GetAllClientOpportunities(bool ignoreFilters)
    {
        ClientRelationshipManagementDbContext context = contextFactory.CreateContext();

        return ignoreFilters ? context.ClientOpportunities.IgnoreQueryFilters() : context.ClientOpportunities;
    }

    public async ValueTask<ClientOpportunity> AddClientOpportunityAsync(ClientOpportunity entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        ClientOpportunity result = (await context.ClientOpportunities.AddAsync(entity)).Entity;
        await context.SaveChangesAsync();
        return result;
    }

    public async ValueTask<ClientOpportunity> UpdateClientOpportunityAsync(ClientOpportunity entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        ClientOpportunity result = context.ClientOpportunities.Update(entity).Entity;
        await context.SaveChangesAsync();
        return result;
    }

    public async ValueTask<int> DeleteClientOpportunityAsync(ClientOpportunity entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        context.ClientOpportunities.Remove(entity);
        return await context.SaveChangesAsync();
    }
}
