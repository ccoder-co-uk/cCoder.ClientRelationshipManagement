using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Foundations;

namespace cCoder.ClientRelationshipManagement.Services.Processings;

internal class AddressProcessingService(IAddressService addressService) : IAddressProcessingService
{
    public Address Get(Guid id, bool ignoreFilters = false) => addressService.Get(id, ignoreFilters);
    public IQueryable<Address> GetAll(bool ignoreFilters = false) => addressService.GetAll(ignoreFilters);
    public ValueTask<Address> AddAsync(Address address) => addressService.AddAsync(address);
    public ValueTask<Address> UpdateAsync(Address address) => addressService.UpdateAsync(address);
    public ValueTask DeleteAsync(Guid id) => addressService.DeleteAsync(id);

    public async ValueTask<Address> UpsertAsync(Address address)
    {
        ArgumentNullException.ThrowIfNull(address, nameof(address));

        return address.Id != Guid.Empty && Get(address.Id, ignoreFilters: true) is not null
            ? await UpdateAsync(address)
            : await AddAsync(address);
    }
}
