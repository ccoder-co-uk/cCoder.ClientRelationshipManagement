using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Foundations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class ClientServiceTests
{
    [Fact]
    public async Task GetAll_ShouldReturnClients()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        IClientService clientService = serviceProvider.GetRequiredService<IClientService>();
        Client client = await TestSupport.AddClientAsync(serviceProvider);

        Assert.Contains(clientService.GetAll(ignoreFilters: true), item => item.Id == client.Id);
    }
}
