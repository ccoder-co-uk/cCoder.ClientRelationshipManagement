using System.Net;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientControllerTests
{
    [CRMAcceptanceFact]
    public async Task Put_UpdatesClient()
    {
        SeededClientContext seededContext = await SeedDatabase();
        seededContext.Client.AccountOwner = Unique("updated-owner");
        seededContext.Client.NextAction = Unique("updated-next-action");

        HttpStatusCode actualStatusCode =
            await PutAsync($"{BaseUrl}/{seededContext.Client.Id}", seededContext.Client);
        cCoder.ClientRelationshipManagement.Models.Entities.Client? actualClient =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.Client>($"{BaseUrl}/{seededContext.Client.Id}");

        actualStatusCode.Should().Be(HttpStatusCode.OK);
        actualClient.Should().NotBeNull();
        actualClient!.AccountOwner.Should().Be(seededContext.Client.AccountOwner);
        actualClient.NextAction.Should().Be(seededContext.Client.NextAction);

        await Teardown(seededContext);
    }
}
