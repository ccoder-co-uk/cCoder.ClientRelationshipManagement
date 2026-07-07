using System.Security;
using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Enums;
using cCoder.ClientRelationshipManagement.Services.Orchestrations;
using cCoder.ClientRelationshipManagement.Tests.Foundations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Orchestrations;

public sealed class OrchestrationSecurityTests
{
    [Fact]
    public async Task ClientAddAsync_ShouldDenyWhenWritePrivilegeIsMissing()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider("client_read");
        IClientOrchestrationService service = serviceProvider.GetRequiredService<IClientOrchestrationService>();

        await Assert.ThrowsAsync<SecurityException>(async () =>
            await service.AddAsync(new Client
            {
                Id = Guid.NewGuid(),
                TenantId = TestSupport.TenantId
            }).AsTask());
    }

    [Fact]
    public async Task CompanyAddAsync_ShouldDenyWhenWritePrivilegeIsMissing()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider("client_read");
        ICompanyOrchestrationService service = serviceProvider.GetRequiredService<ICompanyOrchestrationService>();
        Guid clientId = Guid.NewGuid();

        await Assert.ThrowsAsync<SecurityException>(async () =>
            await service.AddAsync(new Company
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                Client = new Client
                {
                    Id = clientId,
                    TenantId = TestSupport.TenantId
                }
            }).AsTask());
    }

    [Fact]
    public async Task AddressAddAsync_ShouldDenyWhenWritePrivilegeIsMissing()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider("client_read");
        IAddressOrchestrationService service = serviceProvider.GetRequiredService<IAddressOrchestrationService>();
        Guid clientId = Guid.NewGuid();

        await Assert.ThrowsAsync<SecurityException>(async () =>
            await service.AddAsync(new Address
            {
                Id = Guid.NewGuid(),
                Companies =
                [
                    new Company
                    {
                        Id = Guid.NewGuid(),
                        ClientId = clientId,
                        Client = new Client
                        {
                            Id = clientId,
                            TenantId = TestSupport.TenantId
                        }
                    }
                ]
            }).AsTask());
    }

    [Fact]
    public async Task ClientContactAddAsync_ShouldDenyWhenWritePrivilegeIsMissing()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider("client_read");
        Client client = await TestSupport.AddClientAsync(serviceProvider);
        IClientContactOrchestrationService service =
            serviceProvider.GetRequiredService<IClientContactOrchestrationService>();

        await Assert.ThrowsAsync<SecurityException>(async () =>
            await service.AddAsync(new ClientContact
            {
                Id = Guid.NewGuid(),
                ClientId = client.Id
            }).AsTask());
    }

    [Fact]
    public async Task ClientOpportunityAddAsync_ShouldDenyWhenWritePrivilegeIsMissing()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider("client_read");
        Client client = await TestSupport.AddClientAsync(serviceProvider);
        IClientOpportunityOrchestrationService service =
            serviceProvider.GetRequiredService<IClientOpportunityOrchestrationService>();

        await Assert.ThrowsAsync<SecurityException>(async () =>
            await service.AddAsync(new ClientOpportunity
            {
                Id = Guid.NewGuid(),
                ClientId = client.Id,
                Type = ClientOpportunityType.SupplierPaymentReview
            }).AsTask());
    }

    [Fact]
    public async Task ClientActivityAddAsync_ShouldDenyWhenWritePrivilegeIsMissing()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider("client_read");
        Client client = await TestSupport.AddClientAsync(serviceProvider);
        IClientActivityOrchestrationService service =
            serviceProvider.GetRequiredService<IClientActivityOrchestrationService>();

        await Assert.ThrowsAsync<SecurityException>(async () =>
            await service.AddAsync(new ClientActivity
            {
                Id = Guid.NewGuid(),
                ClientId = client.Id,
                Type = ClientActivityType.Email,
                Direction = ClientActivityDirection.Outbound
            }).AsTask());
    }

    [Fact]
    public async Task ClientMaterialAddAsync_ShouldDenyWhenWritePrivilegeIsMissing()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider("client_read");
        Client client = await TestSupport.AddClientAsync(serviceProvider);
        IClientMaterialOrchestrationService service =
            serviceProvider.GetRequiredService<IClientMaterialOrchestrationService>();

        await Assert.ThrowsAsync<SecurityException>(async () =>
            await service.AddAsync(new ClientMaterial
            {
                Id = Guid.NewGuid(),
                ClientId = client.Id,
                Type = ClientMaterialType.Email,
                Status = ClientMaterialStatus.Draft
            }).AsTask());
    }

    [Fact]
    public async Task ClientHandoffPackAddAsync_ShouldDenyWhenWritePrivilegeIsMissing()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider("client_read");
        Client client = await TestSupport.AddClientAsync(serviceProvider);
        ClientOpportunity opportunity = await TestSupport.AddOpportunityAsync(serviceProvider, client.Id);
        IClientHandoffPackOrchestrationService service =
            serviceProvider.GetRequiredService<IClientHandoffPackOrchestrationService>();

        await Assert.ThrowsAsync<SecurityException>(async () =>
            await service.AddAsync(new ClientHandoffPack
            {
                Id = Guid.NewGuid(),
                ClientId = client.Id,
                ClientOpportunityId = opportunity.Id,
                Status = ClientHandoffStatus.Drafting
            }).AsTask());
    }
}
