using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Coordinations;
using cCoder.ClientRelationshipManagement.Services.Processings;

namespace cCoder.ClientRelationshipManagement.Services.Orchestrations;

internal class AddressOrchestrationService(
    IAddressProcessingService addressService,
    IClientProcessingService clientService,
    IDeletionCoordinationService deletionCoordinationService,
    IAuthorizationProcessingService authorizationService)
    : IAddressOrchestrationService
{
    public Address Get(Guid id, bool ignoreFilters = false)
    {
        Address address = addressService.Get(id, ignoreFilters: true);
        if (address is null)
            return null;

        authorizationService.Authorize(ResolveTenantId(address), "client_read");
        return ignoreFilters ? address : addressService.Get(id);
    }

    public IQueryable<Address> GetAll(bool ignoreFilters = false)
    {
        authorizationService.AuthorizeAny("client_read");
        return addressService.GetAll(ignoreFilters);
    }

    public async ValueTask<Address> AddAsync(Address address)
    {
        authorizationService.Authorize(ResolveTenantId(address), "client_write");
        return await addressService.AddAsync(address);
    }

    public async ValueTask<Address> UpdateAsync(Address address)
    {
        authorizationService.Authorize(ResolveTenantId(address), "client_write");
        return await addressService.UpdateAsync(address);
    }

    public async ValueTask<Address> UpsertAsync(Address address)
    {
        Address existingAddress = address is not null && address.Id != Guid.Empty
            ? addressService.Get(address.Id, ignoreFilters: true)
            : null;

        authorizationService.Authorize(
            ResolveTenantId(address ?? existingAddress),
            "client_write");

        return await addressService.UpsertAsync(address);
    }

    public async ValueTask DeleteAsync(Guid id)
    {
        Address address = addressService.Get(id, ignoreFilters: true);
        if (address is null)
            return;

        authorizationService.Authorize(ResolveTenantId(address), "client_write");
        await deletionCoordinationService.HandleAddressDeleteAsync(address);
        await addressService.DeleteAsync(id);
    }

    string ResolveTenantId(Address address) =>
        address?.Companies?
            .Select(company => company.Client)
            .Where(client => client is not null)
            .Select(client => client.TenantId)
            .FirstOrDefault(tenantId => !string.IsNullOrWhiteSpace(tenantId))
        ?? clientService.GetAll(ignoreFilters: true)
            .Where(client => client.Company != null && client.Company.RegisteredAddressId == address.Id)
            .Select(client => client.TenantId)
            .SingleOrDefault();
}
