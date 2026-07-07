using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Enums;
using cCoder.ClientRelationshipManagement.Services.Foundations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class ClientHandoffPackServiceTests
{
    [Fact]
    public async Task AddAsync_ShouldAddClientHandoffPackAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        Client client = await TestSupport.AddClientAsync(serviceProvider);
        ClientOpportunity opportunity = await TestSupport.AddOpportunityAsync(serviceProvider, client.Id);
        IClientHandoffPackService service = serviceProvider.GetRequiredService<IClientHandoffPackService>();

        ClientHandoffPack handoffPack = await service.AddAsync(new ClientHandoffPack
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            ClientOpportunityId = opportunity.Id,
            LegalEntity = "Acceptance Ltd",
            Status = ClientHandoffStatus.Drafting
        });

        Assert.Equal("Acceptance Ltd", handoffPack.LegalEntity);
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "clienthandoffpack_add");
    }

    [Fact]
    public async Task Get_ShouldReturnClientHandoffPack()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientHandoffPack handoffPack = await AddClientHandoffPackAsync(serviceProvider);
        IClientHandoffPackService service = serviceProvider.GetRequiredService<IClientHandoffPackService>();

        ClientHandoffPack result = service.Get(handoffPack.Id, ignoreFilters: true);

        Assert.Equal(handoffPack.Id, result.Id);
    }

    [Fact]
    public async Task GetAll_ShouldReturnClientHandoffPacks()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientHandoffPack handoffPack = await AddClientHandoffPackAsync(serviceProvider);
        IClientHandoffPackService service = serviceProvider.GetRequiredService<IClientHandoffPackService>();

        Assert.Contains(service.GetAll(ignoreFilters: true), item => item.Id == handoffPack.Id);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateClientHandoffPackAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientHandoffPack handoffPack = await AddClientHandoffPackAsync(serviceProvider);
        IClientHandoffPackService service = serviceProvider.GetRequiredService<IClientHandoffPackService>();
        handoffPack.LegalEntity = "Updated Acceptance Ltd";

        await service.UpdateAsync(handoffPack);

        Assert.Equal("Updated Acceptance Ltd", service.Get(handoffPack.Id, ignoreFilters: true).LegalEntity);
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "clienthandoffpack_update");
    }

    [Fact]
    public async Task AddAndUpdateAsync_ShouldManageClientHandoffPackAuditFields()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientHandoffPack handoffPack = await AddClientHandoffPackAsync(serviceProvider);
        IClientHandoffPackService service = serviceProvider.GetRequiredService<IClientHandoffPackService>();

        Assert.Equal("unit-test-user", handoffPack.CreatedBy);
        Assert.Equal("unit-test-user", handoffPack.LastUpdatedBy);
        Assert.NotEqual(default, handoffPack.CreatedOn);
        Assert.NotEqual(default, handoffPack.LastUpdated);
        Assert.True(handoffPack.LastUpdated >= handoffPack.CreatedOn);

        string createdBy = handoffPack.CreatedBy;
        DateTimeOffset createdOn = handoffPack.CreatedOn;
        DateTimeOffset originalLastUpdated = handoffPack.LastUpdated;

        await Task.Delay(10);
        handoffPack.OnboardingOwner = "Audited Owner";
        ClientHandoffPack updatedHandoffPack = await service.UpdateAsync(handoffPack);

        Assert.Equal(createdBy, updatedHandoffPack.CreatedBy);
        Assert.Equal(createdOn, updatedHandoffPack.CreatedOn);
        Assert.Equal("unit-test-user", updatedHandoffPack.LastUpdatedBy);
        Assert.True(updatedHandoffPack.LastUpdated > originalLastUpdated);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteClientHandoffPackAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientHandoffPack handoffPack = await AddClientHandoffPackAsync(serviceProvider);
        IClientHandoffPackService service = serviceProvider.GetRequiredService<IClientHandoffPackService>();

        await service.DeleteAsync(handoffPack.Id);

        Assert.Null(service.Get(handoffPack.Id, ignoreFilters: true));
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "clienthandoffpack_delete");
    }

    static async ValueTask<ClientHandoffPack> AddClientHandoffPackAsync(IServiceProvider serviceProvider)
    {
        Client client = await TestSupport.AddClientAsync(serviceProvider);
        ClientOpportunity opportunity = await TestSupport.AddOpportunityAsync(serviceProvider, client.Id);
        IClientHandoffPackService service = serviceProvider.GetRequiredService<IClientHandoffPackService>();

        return await service.AddAsync(new ClientHandoffPack
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            ClientOpportunityId = opportunity.Id,
            LegalEntity = "Acceptance Ltd",
            Status = ClientHandoffStatus.Drafting
        });
    }
}
