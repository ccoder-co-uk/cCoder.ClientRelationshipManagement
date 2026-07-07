using System.Net;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientHandoffPackControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_ReturnsSeededClientHandoffPacks()
    {
        SeededClientHandoffPackContext seededContext = await SeedDatabase();

        IReadOnlyList<cCoder.ClientRelationshipManagement.Models.Entities.ClientHandoffPack> actualHandoffPacks =
            await GetListAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientHandoffPack>(BaseUrl);

        actualHandoffPacks.Select(handoffPack => handoffPack.Id).Should().Contain(seededContext.ClientHandoffPack.Id);

        await Teardown(seededContext);
    }

    [CRMAcceptanceFact]
    public async Task GetById_ReturnsSeededClientHandoffPack()
    {
        SeededClientHandoffPackContext seededContext = await SeedDatabase();

        cCoder.ClientRelationshipManagement.Models.Entities.ClientHandoffPack? actualHandoffPack =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientHandoffPack>($"{BaseUrl}/{seededContext.ClientHandoffPack.Id}");

        actualHandoffPack.Should().NotBeNull();
        actualHandoffPack!.Id.Should().Be(seededContext.ClientHandoffPack.Id);
        actualHandoffPack.LegalEntity.Should().Be(seededContext.ClientHandoffPack.LegalEntity);

        await Teardown(seededContext);
    }

    [CRMAcceptanceFact]
    public async Task GetById_WhenMissing_ReturnsNotFound()
    {
        HttpStatusCode actualStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{Guid.NewGuid()}");

        actualStatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
