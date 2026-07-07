using System.Security;
using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Services.Orchestrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class ClientServiceTests
{
    [Fact]
    public async Task AddAsync_ShouldDenyWhenPrivilegeIsMissing()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider("client_read");
        IClientOrchestrationService clientService = serviceProvider.GetRequiredService<IClientOrchestrationService>();

        await Assert.ThrowsAsync<SecurityException>(async () =>
            await clientService.AddAsync(new Client
            {
                Id = Guid.NewGuid(),
                TenantId = TestSupport.TenantId,
                Company = new Company
                {
                    Id = Guid.NewGuid(),
                    Name = "Denied"
                }
            }).AsTask());
    }
}
