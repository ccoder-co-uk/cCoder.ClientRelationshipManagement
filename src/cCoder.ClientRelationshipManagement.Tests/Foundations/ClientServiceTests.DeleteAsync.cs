using cCoder.ClientRelationshipManagement.Services.Foundations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class ClientServiceTests
{
    [Fact]
    public async Task DeleteAsync_ShouldDeleteClientAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        IClientService clientService = serviceProvider.GetRequiredService<IClientService>();
        var client = await TestSupport.AddClientAsync(serviceProvider);

        await clientService.DeleteAsync(client.Id);

        Assert.Null(clientService.Get(client.Id, ignoreFilters: true));
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "client_delete");
    }
}
