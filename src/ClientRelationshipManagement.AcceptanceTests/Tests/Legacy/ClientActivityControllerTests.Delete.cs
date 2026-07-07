using System.Net;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientActivityControllerTests
{
    [CRMAcceptanceFact]
    public async Task Delete_RemovesClientActivity()
    {
        SeededClientActivityContext seededContext = await SeedDatabase();

        HttpStatusCode actualDeleteStatusCode = await DeleteAsync($"{BaseUrl}/{seededContext.ClientActivity.Id}");
        HttpStatusCode actualReadStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{seededContext.ClientActivity.Id}");

        actualDeleteStatusCode.Should().Be(HttpStatusCode.NoContent);
        actualReadStatusCode.Should().Be(HttpStatusCode.NotFound);

        await DeleteEntitiesAsync<Client>(seededContext.Client.Id);
    }
}
