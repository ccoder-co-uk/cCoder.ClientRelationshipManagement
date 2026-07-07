using System.Net;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientActivityControllerTests
{
    [CRMAcceptanceFact]
    public async Task Put_UpdatesClientActivity()
    {
        SeededClientActivityContext seededContext = await SeedDatabase();
        seededContext.ClientActivity.Summary = Unique("updated-summary");
        seededContext.ClientActivity.Outcome = Unique("updated-outcome");

        HttpStatusCode actualStatusCode =
            await PutAsync($"{BaseUrl}/{seededContext.ClientActivity.Id}", ToPayload(seededContext.ClientActivity));
        cCoder.ClientRelationshipManagement.Models.Entities.ClientActivity? actualActivity =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientActivity>($"{BaseUrl}/{seededContext.ClientActivity.Id}");

        actualStatusCode.Should().Be(HttpStatusCode.OK);
        actualActivity.Should().NotBeNull();
        actualActivity!.Summary.Should().Be(seededContext.ClientActivity.Summary);
        actualActivity.Outcome.Should().Be(seededContext.ClientActivity.Outcome);

        await Teardown(seededContext);
    }
}
