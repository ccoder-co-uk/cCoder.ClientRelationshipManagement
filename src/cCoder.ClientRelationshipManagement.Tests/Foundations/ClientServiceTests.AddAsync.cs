using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Foundations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class ClientServiceTests
{
    [Fact]
    public async Task AddAsync_ShouldAddClientAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        IClientService clientService = serviceProvider.GetRequiredService<IClientService>();
        TestSupport.RecordingEventHub eventHub =
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>();

        Client client = await clientService.AddAsync(new Client
        {
            Id = Guid.NewGuid(),
            TenantId = TestSupport.TenantId,
            Company = new Company
            {
                Id = Guid.NewGuid(),
                Name = "Stannah"
            }
        });

        Assert.Equal(TestSupport.TenantId, client.TenantId);
        Assert.Contains(eventHub.RaisedEvents, record => record.Name == "client_add");
    }

    [Fact]
    public async Task AddAndUpdateAsync_ShouldManageClientAuditFields()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        IClientService clientService = serviceProvider.GetRequiredService<IClientService>();

        Client client = await clientService.AddAsync(new Client
        {
            Id = Guid.NewGuid(),
            TenantId = TestSupport.TenantId,
            AccountOwner = "Initial Owner"
        });

        Assert.Equal("unit-test-user", client.CreatedBy);
        Assert.Equal("unit-test-user", client.LastUpdatedBy);
        Assert.NotEqual(default, client.CreatedOn);
        Assert.NotEqual(default, client.LastUpdated);
        Assert.True(client.LastUpdated >= client.CreatedOn);

        string createdBy = client.CreatedBy;
        DateTimeOffset createdOn = client.CreatedOn;
        DateTimeOffset originalLastUpdated = client.LastUpdated;

        await Task.Delay(10);
        client.AccountOwner = "Updated Owner";
        Client updatedClient = await clientService.UpdateAsync(client);

        Assert.Equal(createdBy, updatedClient.CreatedBy);
        Assert.Equal(createdOn, updatedClient.CreatedOn);
        Assert.Equal("unit-test-user", updatedClient.LastUpdatedBy);
        Assert.True(updatedClient.LastUpdated > originalLastUpdated);
    }
}
