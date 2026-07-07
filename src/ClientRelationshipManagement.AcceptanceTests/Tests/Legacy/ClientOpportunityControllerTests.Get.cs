using System.Net;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientOpportunityControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_ReturnsSeededClientOpportunities()
    {
        SeededClientOpportunityContext seededContext = await SeedDatabase();

        IReadOnlyList<cCoder.ClientRelationshipManagement.Models.Entities.ClientOpportunity> actualOpportunities =
            await GetListAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientOpportunity>(BaseUrl);

        actualOpportunities.Select(opportunity => opportunity.Id).Should().Contain(seededContext.ClientOpportunity.Id);

        await Teardown(seededContext);
    }

    [CRMAcceptanceFact]
    public async Task GetById_ReturnsSeededClientOpportunity()
    {
        SeededClientOpportunityContext seededContext = await SeedDatabase();

        cCoder.ClientRelationshipManagement.Models.Entities.ClientOpportunity? actualOpportunity =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientOpportunity>($"{BaseUrl}/{seededContext.ClientOpportunity.Id}");

        actualOpportunity.Should().NotBeNull();
        actualOpportunity!.Id.Should().Be(seededContext.ClientOpportunity.Id);
        actualOpportunity.PainSummary.Should().Be(seededContext.ClientOpportunity.PainSummary);

        await Teardown(seededContext);
    }

    [CRMAcceptanceFact]
    public async Task GetById_WhenMissing_ReturnsNotFound()
    {
        HttpStatusCode actualStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{Guid.NewGuid()}");

        actualStatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
