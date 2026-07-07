using System.Net;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientHandoffPackControllerTests
{
    [CRMAcceptanceFact]
    public async Task Put_UpdatesClientHandoffPack()
    {
        SeededClientHandoffPackContext seededContext = await SeedDatabase();
        seededContext.ClientHandoffPack.LegalEntity = Unique("updated-legal-entity");
        seededContext.ClientHandoffPack.PrimaryTechnicalContact = Unique("updated-technical-contact");

        HttpStatusCode actualStatusCode =
            await PutAsync($"{BaseUrl}/{seededContext.ClientHandoffPack.Id}", ToPayload(seededContext.ClientHandoffPack));
        cCoder.ClientRelationshipManagement.Models.Entities.ClientHandoffPack? actualHandoffPack =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientHandoffPack>($"{BaseUrl}/{seededContext.ClientHandoffPack.Id}");

        actualStatusCode.Should().Be(HttpStatusCode.OK);
        actualHandoffPack.Should().NotBeNull();
        actualHandoffPack!.LegalEntity.Should().Be(seededContext.ClientHandoffPack.LegalEntity);
        actualHandoffPack.PrimaryTechnicalContact.Should().Be(seededContext.ClientHandoffPack.PrimaryTechnicalContact);

        await Teardown(seededContext);
    }
}
