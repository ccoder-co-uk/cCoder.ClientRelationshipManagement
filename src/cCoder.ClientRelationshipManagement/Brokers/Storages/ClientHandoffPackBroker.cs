using cCoder.ClientRelationshipManagement.Data;
using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Brokers.Storages;

public class ClientHandoffPackBroker(ICRMContextFactory contextFactory) : IClientHandoffPackBroker
{
    public IQueryable<ClientHandoffPack> GetAllClientHandoffPacks(bool ignoreFilters)
    {
        ClientRelationshipManagementDbContext context = contextFactory.CreateContext();

        return ignoreFilters ? context.ClientHandoffPacks.IgnoreQueryFilters() : context.ClientHandoffPacks;
    }

    public async ValueTask<ClientHandoffPack> AddClientHandoffPackAsync(ClientHandoffPack entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        ClientHandoffPack result = (await context.ClientHandoffPacks.AddAsync(entity)).Entity;
        await context.SaveChangesAsync();
        return result;
    }

    public async ValueTask<ClientHandoffPack> UpdateClientHandoffPackAsync(ClientHandoffPack entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        ClientHandoffPack result = context.ClientHandoffPacks.Update(entity).Entity;
        await context.SaveChangesAsync();
        return result;
    }

    public async ValueTask<int> DeleteClientHandoffPackAsync(ClientHandoffPack entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        context.ClientHandoffPacks.Remove(entity);
        return await context.SaveChangesAsync();
    }
}
