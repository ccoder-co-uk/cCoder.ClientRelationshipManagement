using cCoder.ClientRelationshipManagement.Data;
using cCoder.ClientRelationshipManagement.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Brokers.Storages;

public class AddressBroker(ICRMContextFactory contextFactory) : IAddressBroker
{
    public IQueryable<Address> GetAllAddresses(bool ignoreFilters)
    {
        ClientRelationshipManagementDbContext context = contextFactory.CreateContext();

        return ignoreFilters ? context.Addresses.IgnoreQueryFilters() : context.Addresses;
    }

    public async ValueTask<Address> AddAddressAsync(Address entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        Address result = (await context.Addresses.AddAsync(entity)).Entity;
        await context.SaveChangesAsync();
        return result;
    }

    public async ValueTask<Address> UpdateAddressAsync(Address entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        Address result = context.Addresses.Update(entity).Entity;
        await context.SaveChangesAsync();
        return result;
    }

    public async ValueTask<int> DeleteAddressAsync(Address entity)
    {
        using ClientRelationshipManagementDbContext context = contextFactory.CreateContext();
        context.Addresses.Remove(entity);
        return await context.SaveChangesAsync();
    }
}
