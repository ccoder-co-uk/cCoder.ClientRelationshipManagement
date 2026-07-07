using System.Net;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientMaterialControllerTests
{
    [CRMAcceptanceFact]
    public async Task Put_UpdatesClientMaterial()
    {
        SeededClientMaterialContext seededContext = await SeedDatabase();
        seededContext.ClientMaterial.Name = Unique("updated-material");
        seededContext.ClientMaterial.Notes = Unique("updated-notes");

        HttpStatusCode actualStatusCode =
            await PutAsync($"{BaseUrl}/{seededContext.ClientMaterial.Id}", ToPayload(seededContext.ClientMaterial));
        cCoder.ClientRelationshipManagement.Models.Entities.ClientMaterial? actualMaterial =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientMaterial>($"{BaseUrl}/{seededContext.ClientMaterial.Id}");

        actualStatusCode.Should().Be(HttpStatusCode.OK);
        actualMaterial.Should().NotBeNull();
        actualMaterial!.Name.Should().Be(seededContext.ClientMaterial.Name);
        actualMaterial.Notes.Should().Be(seededContext.ClientMaterial.Notes);

        await Teardown(seededContext);
    }
}
