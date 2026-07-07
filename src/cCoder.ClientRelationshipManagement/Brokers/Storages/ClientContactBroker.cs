using cCoder.ClientRelationshipManagement.Data;
using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Brokers.Storages;

public class ClientContactBroker(ICRMContextFactory contextFactory) : IClientContactBroker
{
    public IQueryable<ClientContact> GetAllClientContacts(bool ignoreFilters)
    {
        ClientRelationshipManagementDbContext context = contextFactory.CreateContext();

        return ignoreFilters ? context.ClientContacts.IgnoreQueryFilters() : context.ClientContacts;
    }

    public async ValueTask<ClientContact> AddClientContactAsync(ClientContact entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        ClientContact result = (await context.ClientContacts.AddAsync(entity)).Entity;
        await context.SaveChangesAsync();
        return result;
    }

    public async ValueTask<ClientContact> UpdateClientContactAsync(ClientContact entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        ClientContact result = context.ClientContacts.Update(entity).Entity;
        await context.SaveChangesAsync();
        return result;
    }

    public async ValueTask<int> DeleteClientContactAsync(ClientContact entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        context.ClientContacts.Remove(entity);
        return await context.SaveChangesAsync();
    }
}
