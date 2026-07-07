using System.Net;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientMaterialControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_ReturnsSeededClientMaterials()
    {
        SeededClientMaterialContext seededContext = await SeedDatabase();

        IReadOnlyList<cCoder.ClientRelationshipManagement.Models.Entities.ClientMaterial> actualMaterials =
            await GetListAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientMaterial>(BaseUrl);

        actualMaterials.Select(material => material.Id).Should().Contain(seededContext.ClientMaterial.Id);

        await Teardown(seededContext);
    }

    [CRMAcceptanceFact]
    public async Task GetById_ReturnsSeededClientMaterial()
    {
        SeededClientMaterialContext seededContext = await SeedDatabase();

        cCoder.ClientRelationshipManagement.Models.Entities.ClientMaterial? actualMaterial =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientMaterial>($"{BaseUrl}/{seededContext.ClientMaterial.Id}");

        actualMaterial.Should().NotBeNull();
        actualMaterial!.Id.Should().Be(seededContext.ClientMaterial.Id);
        actualMaterial.Name.Should().Be(seededContext.ClientMaterial.Name);

        await Teardown(seededContext);
    }

    [CRMAcceptanceFact]
    public async Task GetById_WhenMissing_ReturnsNotFound()
    {
        HttpStatusCode actualStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{Guid.NewGuid()}");

        actualStatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
