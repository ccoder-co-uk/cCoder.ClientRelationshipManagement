using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Brokers.Storages;

public interface IAddressBroker
{
    IQueryable<Address> GetAllAddresses(bool ignoreFilters);

    ValueTask<Address> AddAddressAsync(Address entity);

    ValueTask<Address> UpdateAddressAsync(Address entity);

    ValueTask<int> DeleteAddressAsync(Address entity);
}
