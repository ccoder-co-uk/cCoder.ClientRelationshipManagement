using System.Net;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientHandoffPackControllerTests
{
    [CRMAcceptanceFact]
    public async Task Delete_RemovesClientHandoffPack()
    {
        SeededClientHandoffPackContext seededContext = await SeedDatabase();

        HttpStatusCode actualDeleteStatusCode = await DeleteAsync($"{BaseUrl}/{seededContext.ClientHandoffPack.Id}");
        HttpStatusCode actualReadStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{seededContext.ClientHandoffPack.Id}");

        actualDeleteStatusCode.Should().Be(HttpStatusCode.NoContent);
        actualReadStatusCode.Should().Be(HttpStatusCode.NotFound);

        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientOpportunity>(seededContext.ClientOpportunity.Id);
        await DeleteEntitiesAsync<cCoder.ClientRelationshipManagement.Models.Entities.Client>(seededContext.Client.Id);
    }
}
