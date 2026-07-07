using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Foundations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class ClientContactServiceTests
{
    [Fact]
    public async Task AddAsync_ShouldAddClientContactAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        Client client = await TestSupport.AddClientAsync(serviceProvider);
        IClientContactService service = serviceProvider.GetRequiredService<IClientContactService>();

        ClientContact contact = await service.AddAsync(new ClientContact
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Name = "Tiffany"
        });

        Assert.Equal("Tiffany", contact.Name);
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "clientcontact_add");
    }

    [Fact]
    public async Task Get_ShouldReturnClientContact()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientContact contact = await AddClientContactAsync(serviceProvider);
        IClientContactService service = serviceProvider.GetRequiredService<IClientContactService>();

        ClientContact result = service.Get(contact.Id, ignoreFilters: true);

        Assert.Equal(contact.Id, result.Id);
    }

    [Fact]
    public async Task GetAll_ShouldReturnClientContacts()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientContact contact = await AddClientContactAsync(serviceProvider);
        IClientContactService service = serviceProvider.GetRequiredService<IClientContactService>();

        Assert.Contains(service.GetAll(ignoreFilters: true), item => item.Id == contact.Id);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateClientContactAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientContact contact = await AddClientContactAsync(serviceProvider);
        IClientContactService service = serviceProvider.GetRequiredService<IClientContactService>();
        contact.Name = "Updated Tiffany";

        await service.UpdateAsync(contact);

        Assert.Equal("Updated Tiffany", service.Get(contact.Id, ignoreFilters: true).Name);
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "clientcontact_update");
    }

    [Fact]
    public async Task AddAndUpdateAsync_ShouldManageClientContactAuditFields()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientContact contact = await AddClientContactAsync(serviceProvider);
        IClientContactService service = serviceProvider.GetRequiredService<IClientContactService>();

        Assert.Equal("unit-test-user", contact.CreatedBy);
        Assert.Equal("unit-test-user", contact.LastUpdatedBy);
        Assert.NotEqual(default, contact.CreatedOn);
        Assert.NotEqual(default, contact.LastUpdated);
        Assert.True(contact.LastUpdated >= contact.CreatedOn);

        string createdBy = contact.CreatedBy;
        DateTimeOffset createdOn = contact.CreatedOn;
        DateTimeOffset originalLastUpdated = contact.LastUpdated;

        await Task.Delay(10);
        contact.Name = "Audited Contact";
        ClientContact updatedContact = await service.UpdateAsync(contact);

        Assert.Equal(createdBy, updatedContact.CreatedBy);
        Assert.Equal(createdOn, updatedContact.CreatedOn);
        Assert.Equal("unit-test-user", updatedContact.LastUpdatedBy);
        Assert.True(updatedContact.LastUpdated > originalLastUpdated);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteClientContactAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientContact contact = await AddClientContactAsync(serviceProvider);
        IClientContactService service = serviceProvider.GetRequiredService<IClientContactService>();

        await service.DeleteAsync(contact.Id);

        Assert.Null(service.Get(contact.Id, ignoreFilters: true));
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "clientcontact_delete");
    }

    static async ValueTask<ClientContact> AddClientContactAsync(IServiceProvider serviceProvider)
    {
        Client client = await TestSupport.AddClientAsync(serviceProvider);
        IClientContactService service = serviceProvider.GetRequiredService<IClientContactService>();

        return await service.AddAsync(new ClientContact
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Name = "Tiffany"
        });
    }
}
