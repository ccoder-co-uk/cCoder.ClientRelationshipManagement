using cCoder.ClientRelationshipManagement.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Processings;

public interface IAddressProcessingService
{
    Address Get(Guid id, bool ignoreFilters = false);
    IQueryable<Address> GetAll(bool ignoreFilters = false);
    ValueTask<Address> AddAsync(Address address);
    ValueTask<Address> UpdateAsync(Address address);
    ValueTask<Address> UpsertAsync(Address address);
    ValueTask DeleteAsync(Guid id);
}
