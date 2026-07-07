using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Enums;
using cCoder.ClientRelationshipManagement.Services.Foundations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class ClientActivityServiceTests
{
    [Fact]
    public async Task AddAsync_ShouldAddClientActivityAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        Client client = await TestSupport.AddClientAsync(serviceProvider);
        IClientActivityService service = serviceProvider.GetRequiredService<IClientActivityService>();

        ClientActivity activity = await service.AddAsync(new ClientActivity
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Type = ClientActivityType.Email,
            Direction = ClientActivityDirection.Outbound,
            Summary = "Initial contact"
        });

        Assert.Equal("Initial contact", activity.Summary);
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "clientactivity_add");
    }

    [Fact]
    public async Task Get_ShouldReturnClientActivity()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientActivity activity = await AddClientActivityAsync(serviceProvider);
        IClientActivityService service = serviceProvider.GetRequiredService<IClientActivityService>();

        ClientActivity result = service.Get(activity.Id, ignoreFilters: true);

        Assert.Equal(activity.Id, result.Id);
    }

    [Fact]
    public async Task GetAll_ShouldReturnClientActivities()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientActivity activity = await AddClientActivityAsync(serviceProvider);
        IClientActivityService service = serviceProvider.GetRequiredService<IClientActivityService>();

        Assert.Contains(service.GetAll(ignoreFilters: true), item => item.Id == activity.Id);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateClientActivityAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientActivity activity = await AddClientActivityAsync(serviceProvider);
        IClientActivityService service = serviceProvider.GetRequiredService<IClientActivityService>();
        activity.Summary = "Updated initial contact";

        await service.UpdateAsync(activity);

        Assert.Equal("Updated initial contact", service.Get(activity.Id, ignoreFilters: true).Summary);
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "clientactivity_update");
    }

    [Fact]
    public async Task AddAndUpdateAsync_ShouldManageClientActivityAuditFields()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientActivity activity = await AddClientActivityAsync(serviceProvider);
        IClientActivityService service = serviceProvider.GetRequiredService<IClientActivityService>();

        Assert.Equal("unit-test-user", activity.CreatedBy);
        Assert.NotEqual(default, activity.CreatedOn);

        string createdBy = activity.CreatedBy;
        DateTimeOffset createdOn = activity.CreatedOn;

        activity.Summary = "Audited activity update";
        ClientActivity updatedActivity = await service.UpdateAsync(activity);

        Assert.Equal(createdBy, updatedActivity.CreatedBy);
        Assert.Equal(createdOn, updatedActivity.CreatedOn);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteClientActivityAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientActivity activity = await AddClientActivityAsync(serviceProvider);
        IClientActivityService service = serviceProvider.GetRequiredService<IClientActivityService>();

        await service.DeleteAsync(activity.Id);

        Assert.Null(service.Get(activity.Id, ignoreFilters: true));
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "clientactivity_delete");
    }

    static async ValueTask<ClientActivity> AddClientActivityAsync(IServiceProvider serviceProvider)
    {
        Client client = await TestSupport.AddClientAsync(serviceProvider);
        IClientActivityService service = serviceProvider.GetRequiredService<IClientActivityService>();

        return await service.AddAsync(new ClientActivity
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Type = ClientActivityType.Email,
            Direction = ClientActivityDirection.Outbound,
            Summary = "Initial contact"
        });
    }
}
