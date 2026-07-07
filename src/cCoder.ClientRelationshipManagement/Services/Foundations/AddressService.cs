using cCoder.ClientRelationshipManagement.Brokers.Events;
using cCoder.ClientRelationshipManagement.Brokers.Storages;
using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.Eventing.Models;

namespace cCoder.ClientRelationshipManagement.Services.Foundations;

internal partial class AddressService(
    IAddressBroker addressBroker,
    IAddressEventBroker addressEventBroker,
    ICRMAuthInfo authInfo)
    : IAddressService
{
    public Address Get(Guid id, bool ignoreFilters = false) =>
        addressBroker.GetAllAddresses(ignoreFilters)
            .FirstOrDefault(address => address.Id == id);

    public IQueryable<Address> GetAll(bool ignoreFilters = false) =>
        addressBroker.GetAllAddresses(ignoreFilters);

    public async ValueTask<Address> AddAsync(Address address)
    {
        ValidateAddress(address, nameof(address));

        if (address.Id == Guid.Empty)
            address.Id = Guid.NewGuid();

        if (Get(address.Id, ignoreFilters: true) is not null)
            throw new InvalidOperationException($"Address '{address.Id}' already exists.");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Address storageAddress = new()
        {
            Id = address.Id,
            PoBox = address.PoBox,
            Line1 = address.Line1,
            Line2 = address.Line2,
            ZipOrPostalCode = address.ZipOrPostalCode,
            TownOrCity = address.TownOrCity,
            StateOrProvince = address.StateOrProvince,
            CountryId = address.CountryId,
            IsActive = address.IsActive,
            CreatedOn = address.CreatedOn == default ? now : address.CreatedOn,
            LastUpdated = now,
        };

        Address result = await addressBroker.AddAddressAsync(storageAddress);
        result.Companies = address.Companies;
        await addressEventBroker.RaiseAddressAddEventAsync(CreateEventMessage(result));
        return result;
    }

    public async ValueTask<Address> UpdateAsync(Address address)
    {
        ValidateAddress(address, nameof(address));

        Address storageAddress = new()
        {
            Id = address.Id,
            PoBox = address.PoBox,
            Line1 = address.Line1,
            Line2 = address.Line2,
            ZipOrPostalCode = address.ZipOrPostalCode,
            TownOrCity = address.TownOrCity,
            StateOrProvince = address.StateOrProvince,
            CountryId = address.CountryId,
            IsActive = address.IsActive,
            CreatedOn = address.CreatedOn,
            LastUpdated = DateTimeOffset.UtcNow,
        };

        Address result = await addressBroker.UpdateAddressAsync(storageAddress);
        result.Companies = address.Companies;
        await addressEventBroker.RaiseAddressUpdateEventAsync(CreateEventMessage(result));
        return result;
    }

    public async ValueTask DeleteAsync(Guid id)
    {
        Address address = Get(id, ignoreFilters: true);

        if (address is null)
            return;

        await addressEventBroker.RaiseAddressDeleteEventAsync(CreateEventMessage(address));
        await addressBroker.DeleteAddressAsync(address);
    }

    EventMessage<Address> CreateEventMessage(Address address) =>
        new()
        {
            AuthInfo = new EventAuthInfo
            {
                SSOUserId = authInfo.SSOUserId,
            },
            Data = address,
        };

    static void ValidateAddress(Address address, string parameterName) =>
        ArgumentNullException.ThrowIfNull(address, parameterName);
}
