using cCoder.ClientRelationshipManagement.Services.Foundations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class ClientServiceTests
{
    [Fact]
    public async Task UpdateAsync_ShouldUpdateClientAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        IClientService clientService = serviceProvider.GetRequiredService<IClientService>();
        var client = await TestSupport.AddClientAsync(serviceProvider);
        client.AccountOwner = "Updated Owner";

        await clientService.UpdateAsync(client);

        Assert.Equal("Updated Owner", clientService.Get(client.Id).AccountOwner);
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "client_update");
    }
}
