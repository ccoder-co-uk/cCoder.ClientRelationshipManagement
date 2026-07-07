using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Enums;
using cCoder.ClientRelationshipManagement.Services.Foundations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Foundations;

public partial class ClientMaterialServiceTests
{
    [Fact]
    public async Task AddAsync_ShouldAddClientMaterialAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        Client client = await TestSupport.AddClientAsync(serviceProvider);
        IClientMaterialService service = serviceProvider.GetRequiredService<IClientMaterialService>();

        ClientMaterial material = await service.AddAsync(new ClientMaterial
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Name = "Introduction Email",
            Type = ClientMaterialType.Email,
            Status = ClientMaterialStatus.Sent
        });

        Assert.Equal("Introduction Email", material.Name);
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "clientmaterial_add");
    }

    [Fact]
    public async Task Get_ShouldReturnClientMaterial()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientMaterial material = await AddClientMaterialAsync(serviceProvider);
        IClientMaterialService service = serviceProvider.GetRequiredService<IClientMaterialService>();

        ClientMaterial result = service.Get(material.Id, ignoreFilters: true);

        Assert.Equal(material.Id, result.Id);
    }

    [Fact]
    public async Task GetAll_ShouldReturnClientMaterials()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientMaterial material = await AddClientMaterialAsync(serviceProvider);
        IClientMaterialService service = serviceProvider.GetRequiredService<IClientMaterialService>();

        Assert.Contains(service.GetAll(ignoreFilters: true), item => item.Id == material.Id);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateClientMaterialAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientMaterial material = await AddClientMaterialAsync(serviceProvider);
        IClientMaterialService service = serviceProvider.GetRequiredService<IClientMaterialService>();
        material.Name = "Updated Introduction Email";

        await service.UpdateAsync(material);

        Assert.Equal("Updated Introduction Email", service.Get(material.Id, ignoreFilters: true).Name);
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "clientmaterial_update");
    }

    [Fact]
    public async Task AddAndUpdateAsync_ShouldManageClientMaterialAuditFields()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientMaterial material = await AddClientMaterialAsync(serviceProvider);
        IClientMaterialService service = serviceProvider.GetRequiredService<IClientMaterialService>();

        Assert.Equal("unit-test-user", material.CreatedBy);
        Assert.Equal("unit-test-user", material.LastUpdatedBy);
        Assert.NotEqual(default, material.CreatedOn);
        Assert.NotEqual(default, material.LastUpdated);
        Assert.True(material.LastUpdated >= material.CreatedOn);

        string createdBy = material.CreatedBy;
        DateTimeOffset createdOn = material.CreatedOn;
        DateTimeOffset originalLastUpdated = material.LastUpdated;

        await Task.Delay(10);
        material.Name = "Audited material update";
        ClientMaterial updatedMaterial = await service.UpdateAsync(material);

        Assert.Equal(createdBy, updatedMaterial.CreatedBy);
        Assert.Equal(createdOn, updatedMaterial.CreatedOn);
        Assert.Equal("unit-test-user", updatedMaterial.LastUpdatedBy);
        Assert.True(updatedMaterial.LastUpdated > originalLastUpdated);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteClientMaterialAndRaiseEvent()
    {
        using ServiceProvider serviceProvider = TestSupport.CreateServiceProvider();
        ClientMaterial material = await AddClientMaterialAsync(serviceProvider);
        IClientMaterialService service = serviceProvider.GetRequiredService<IClientMaterialService>();

        await service.DeleteAsync(material.Id);

        Assert.Null(service.Get(material.Id, ignoreFilters: true));
        Assert.Contains(
            serviceProvider.GetRequiredService<TestSupport.RecordingEventHub>().RaisedEvents,
            record => record.Name == "clientmaterial_delete");
    }

    static async ValueTask<ClientMaterial> AddClientMaterialAsync(IServiceProvider serviceProvider)
    {
        Client client = await TestSupport.AddClientAsync(serviceProvider);
        IClientMaterialService service = serviceProvider.GetRequiredService<IClientMaterialService>();

        return await service.AddAsync(new ClientMaterial
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Name = "Introduction Email",
            Type = ClientMaterialType.Email,
            Status = ClientMaterialStatus.Sent
        });
    }
}
