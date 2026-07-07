using cCoder.ClientRelationshipManagement.Data;
using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Brokers.Storages;

public class ClientActivityBroker(ICRMContextFactory contextFactory) : IClientActivityBroker
{
    public IQueryable<ClientActivity> GetAllClientActivities(bool ignoreFilters)
    {
        ClientRelationshipManagementDbContext context = contextFactory.CreateContext();

        return ignoreFilters ? context.ClientActivities.IgnoreQueryFilters() : context.ClientActivities;
    }

    public async ValueTask<ClientActivity> AddClientActivityAsync(ClientActivity entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        ClientActivity result = (await context.ClientActivities.AddAsync(entity)).Entity;
        await context.SaveChangesAsync();
        return result;
    }

    public async ValueTask<ClientActivity> UpdateClientActivityAsync(ClientActivity entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        ClientActivity result = context.ClientActivities.Update(entity).Entity;
        await context.SaveChangesAsync();
        return result;
    }

    public async ValueTask<int> DeleteClientActivityAsync(ClientActivity entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        context.ClientActivities.Remove(entity);
        return await context.SaveChangesAsync();
    }
}
