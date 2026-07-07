using System.Net;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientOpportunityControllerTests
{
    [CRMAcceptanceFact]
    public async Task Put_UpdatesClientOpportunity()
    {
        SeededClientOpportunityContext seededContext = await SeedDatabase();
        seededContext.ClientOpportunity.PainSummary = Unique("updated-pain");
        seededContext.ClientOpportunity.NextAction = Unique("updated-next-action");

        HttpStatusCode actualStatusCode =
            await PutAsync($"{BaseUrl}/{seededContext.ClientOpportunity.Id}", ToPayload(seededContext.ClientOpportunity));
        cCoder.ClientRelationshipManagement.Models.Entities.ClientOpportunity? actualOpportunity =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientOpportunity>($"{BaseUrl}/{seededContext.ClientOpportunity.Id}");

        actualStatusCode.Should().Be(HttpStatusCode.OK);
        actualOpportunity.Should().NotBeNull();
        actualOpportunity!.PainSummary.Should().Be(seededContext.ClientOpportunity.PainSummary);
        actualOpportunity.NextAction.Should().Be(seededContext.ClientOpportunity.NextAction);

        await Teardown(seededContext);
    }
}
