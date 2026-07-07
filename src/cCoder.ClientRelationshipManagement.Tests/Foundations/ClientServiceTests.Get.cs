using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Foundations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class ClientServiceTests
{
    [Fact]
    public async Task Get_ShouldReturnClient()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        IClientService clientService = serviceProvider.GetRequiredService<IClientService>();
        Client client = await TestSupport.AddClientAsync(serviceProvider);

        Client result = clientService.Get(client.Id, ignoreFilters: true);

        Assert.Equal(client.Id, result.Id);
    }
}
