using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Foundations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class AddressServiceTests
{
    [Fact]
    public async Task AddAsync_ShouldAddAddressAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        IAddressService addressService = serviceProvider.GetRequiredService<IAddressService>();
        Guid clientId = Guid.NewGuid();

        Address address = await addressService.AddAsync(new Address
        {
            Id = Guid.NewGuid(),
            Line1 = "180 Hardgate Road",
            Companies =
            [
                new Company
                {
                    Id = Guid.NewGuid(),
                    ClientId = clientId,
                    Name = "Barclay & Mathieson Limited",
                    Client = new Client
                    {
                        Id = clientId,
                        TenantId = TestSupport.TenantId
                    }
                }
            ]
        });

        Assert.Equal("180 Hardgate Road", address.Line1);
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "address_add");
    }

    [Fact]
    public async Task Get_ShouldReturnAddress()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        IAddressService addressService = serviceProvider.GetRequiredService<IAddressService>();
        Address address = await AddAddressAsync(addressService);

        Address result = addressService.Get(address.Id, ignoreFilters: true);

        Assert.Equal(address.Id, result.Id);
    }

    [Fact]
    public async Task GetAll_ShouldReturnAddresses()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        IAddressService addressService = serviceProvider.GetRequiredService<IAddressService>();
        Address address = await AddAddressAsync(addressService);

        Assert.Contains(addressService.GetAll(ignoreFilters: true), item => item.Id == address.Id);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateAddressAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        IAddressService addressService = serviceProvider.GetRequiredService<IAddressService>();
        Address address = await AddAddressAsync(addressService);
        address.Line1 = "Updated address line";

        await addressService.UpdateAsync(address);

        Assert.Equal("Updated address line", addressService.Get(address.Id, ignoreFilters: true).Line1);
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "address_update");
    }

    [Fact]
    public async Task AddAndUpdateAsync_ShouldManageAddressAuditFields()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        IAddressService addressService = serviceProvider.GetRequiredService<IAddressService>();

        Address address = await addressService.AddAsync(new Address
        {
            Id = Guid.NewGuid(),
            Line1 = "12 Audit Lane"
        });

        Assert.NotEqual(default, address.CreatedOn);
        Assert.NotEqual(default, address.LastUpdated);
        Assert.True(address.LastUpdated >= address.CreatedOn);

        DateTimeOffset createdOn = address.CreatedOn;
        DateTimeOffset originalLastUpdated = address.LastUpdated;

        await Task.Delay(10);
        address.Line1 = "14 Audit Lane";
        Address updatedAddress = await addressService.UpdateAsync(address);

        Assert.Equal(createdOn, updatedAddress.CreatedOn);
        Assert.True(updatedAddress.LastUpdated > originalLastUpdated);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteAddressAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        IAddressService addressService = serviceProvider.GetRequiredService<IAddressService>();
        Address address = await AddAddressAsync(addressService);

        await addressService.DeleteAsync(address.Id);

        Assert.Null(addressService.Get(address.Id, ignoreFilters: true));
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "address_delete");
    }

    static async ValueTask<Address> AddAddressAsync(IAddressService addressService)
    {
        Guid clientId = Guid.NewGuid();

        return await addressService.AddAsync(new Address
        {
            Id = Guid.NewGuid(),
            Line1 = "180 Hardgate Road",
            Companies =
            [
                new Company
                {
                    Id = Guid.NewGuid(),
                    ClientId = clientId,
                    Name = "Barclay & Mathieson Limited",
                    Client = new Client
                    {
                        Id = clientId,
                        TenantId = TestSupport.TenantId
                    }
                }
            ]
        });
    }
}
