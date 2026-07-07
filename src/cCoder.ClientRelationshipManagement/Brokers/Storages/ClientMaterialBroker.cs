using cCoder.ClientRelationshipManagement.Data;
using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Brokers.Storages;

public class ClientMaterialBroker(ICRMContextFactory contextFactory) : IClientMaterialBroker
{
    public IQueryable<ClientMaterial> GetAllClientMaterials(bool ignoreFilters)
    {
        ClientRelationshipManagementDbContext context = contextFactory.CreateContext();

        return ignoreFilters ? context.ClientMaterials.IgnoreQueryFilters() : context.ClientMaterials;
    }

    public async ValueTask<ClientMaterial> AddClientMaterialAsync(ClientMaterial entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        ClientMaterial result = (await context.ClientMaterials.AddAsync(entity)).Entity;
        await context.SaveChangesAsync();
        return result;
    }

    public async ValueTask<ClientMaterial> UpdateClientMaterialAsync(ClientMaterial entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        ClientMaterial result = context.ClientMaterials.Update(entity).Entity;
        await context.SaveChangesAsync();
        return result;
    }

    public async ValueTask<int> DeleteClientMaterialAsync(ClientMaterial entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        context.ClientMaterials.Remove(entity);
        return await context.SaveChangesAsync();
    }
}
