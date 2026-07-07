using System.Net;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientActivityControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_ReturnsSeededClientActivities()
    {
        SeededClientActivityContext seededContext = await SeedDatabase();

        IReadOnlyList<cCoder.ClientRelationshipManagement.Models.Entities.ClientActivity> actualActivities =
            await GetListAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientActivity>(BaseUrl);

        actualActivities.Select(activity => activity.Id).Should().Contain(seededContext.ClientActivity.Id);

        await Teardown(seededContext);
    }

    [CRMAcceptanceFact]
    public async Task GetById_ReturnsSeededClientActivity()
    {
        SeededClientActivityContext seededContext = await SeedDatabase();

        cCoder.ClientRelationshipManagement.Models.Entities.ClientActivity? actualActivity =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientActivity>($"{BaseUrl}/{seededContext.ClientActivity.Id}");

        actualActivity.Should().NotBeNull();
        actualActivity!.Id.Should().Be(seededContext.ClientActivity.Id);
        actualActivity.Summary.Should().Be(seededContext.ClientActivity.Summary);

        await Teardown(seededContext);
    }

    [CRMAcceptanceFact]
    public async Task GetById_WhenMissing_ReturnsNotFound()
    {
        HttpStatusCode actualStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{Guid.NewGuid()}");

        actualStatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
