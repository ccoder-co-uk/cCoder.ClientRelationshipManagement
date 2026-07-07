using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Enums;
using cCoder.ClientRelationshipManagement.Services.Foundations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class ClientOpportunityServiceTests
{
    [Fact]
    public async Task AddAsync_ShouldAddClientOpportunityAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        Client client = await TestSupport.AddClientAsync(serviceProvider);
        IClientOpportunityService service = serviceProvider.GetRequiredService<IClientOpportunityService>();

        ClientOpportunity opportunity = await service.AddAsync(new ClientOpportunity
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Type = ClientOpportunityType.SupplierPaymentReview
        });

        Assert.Equal(ClientOpportunityType.SupplierPaymentReview, opportunity.Type);
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "clientopportunity_add");
    }

    [Fact]
    public async Task Get_ShouldReturnClientOpportunity()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientOpportunity opportunity = await AddClientOpportunityAsync(serviceProvider);
        IClientOpportunityService service = serviceProvider.GetRequiredService<IClientOpportunityService>();

        ClientOpportunity result = service.Get(opportunity.Id, ignoreFilters: true);

        Assert.Equal(opportunity.Id, result.Id);
    }

    [Fact]
    public async Task GetAll_ShouldReturnClientOpportunities()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientOpportunity opportunity = await AddClientOpportunityAsync(serviceProvider);
        IClientOpportunityService service = serviceProvider.GetRequiredService<IClientOpportunityService>();

        Assert.Contains(service.GetAll(ignoreFilters: true), item => item.Id == opportunity.Id);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateClientOpportunityAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientOpportunity opportunity = await AddClientOpportunityAsync(serviceProvider);
        IClientOpportunityService service = serviceProvider.GetRequiredService<IClientOpportunityService>();
        opportunity.PainSummary = "Updated pain";

        await service.UpdateAsync(opportunity);

        Assert.Equal("Updated pain", service.Get(opportunity.Id, ignoreFilters: true).PainSummary);
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "clientopportunity_update");
    }

    [Fact]
    public async Task AddAndUpdateAsync_ShouldManageClientOpportunityAuditFields()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientOpportunity opportunity = await AddClientOpportunityAsync(serviceProvider);
        IClientOpportunityService service = serviceProvider.GetRequiredService<IClientOpportunityService>();

        Assert.Equal("unit-test-user", opportunity.CreatedBy);
        Assert.Equal("unit-test-user", opportunity.LastUpdatedBy);
        Assert.NotEqual(default, opportunity.CreatedOn);
        Assert.NotEqual(default, opportunity.LastUpdated);
        Assert.True(opportunity.LastUpdated >= opportunity.CreatedOn);

        string createdBy = opportunity.CreatedBy;
        DateTimeOffset createdOn = opportunity.CreatedOn;
        DateTimeOffset originalLastUpdated = opportunity.LastUpdated;

        await Task.Delay(10);
        opportunity.ValueHypothesis = "Audited opportunity update";
        ClientOpportunity updatedOpportunity = await service.UpdateAsync(opportunity);

        Assert.Equal(createdBy, updatedOpportunity.CreatedBy);
        Assert.Equal(createdOn, updatedOpportunity.CreatedOn);
        Assert.Equal("unit-test-user", updatedOpportunity.LastUpdatedBy);
        Assert.True(updatedOpportunity.LastUpdated > originalLastUpdated);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteClientOpportunityAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientOpportunity opportunity = await AddClientOpportunityAsync(serviceProvider);
        IClientOpportunityService service = serviceProvider.GetRequiredService<IClientOpportunityService>();

        await service.DeleteAsync(opportunity.Id);

        Assert.Null(service.Get(opportunity.Id, ignoreFilters: true));
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "clientopportunity_delete");
    }

    static async ValueTask<ClientOpportunity> AddClientOpportunityAsync(IServiceProvider serviceProvider)
    {
        Client client = await TestSupport.AddClientAsync(serviceProvider);
        IClientOpportunityService service = serviceProvider.GetRequiredService<IClientOpportunityService>();

        return await service.AddAsync(new ClientOpportunity
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Type = ClientOpportunityType.SupplierPaymentReview
        });
    }
}
