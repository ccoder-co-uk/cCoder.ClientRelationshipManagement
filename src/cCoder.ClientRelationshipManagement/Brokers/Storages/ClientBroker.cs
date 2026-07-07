using cCoder.ClientRelationshipManagement.Data;
using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Brokers.Storages;

public class ClientBroker(ICRMContextFactory contextFactory) : IClientBroker
{
    public IQueryable<Client> GetAllClients(bool ignoreFilters)
    {
        ClientRelationshipManagementDbContext context = contextFactory.CreateContext();

        return ignoreFilters ? context.Clients.IgnoreQueryFilters() : context.Clients;
    }

    public async ValueTask<Client> AddClientAsync(Client entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        Client result = (await context.Clients.AddAsync(entity)).Entity;
        await context.SaveChangesAsync();
        return result;
    }

    public async ValueTask<Client> UpdateClientAsync(Client entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        Client result = context.Clients.Update(entity).Entity;
        await context.SaveChangesAsync();
        return result;
    }

    public async ValueTask<int> DeleteClientAsync(Client entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        context.Clients.Remove(entity);
        return await context.SaveChangesAsync();
    }
}
