using System.Net;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_ReturnsSeededClients()
    {
        SeededClientContext seededContext = await SeedDatabase();

        IReadOnlyList<cCoder.ClientRelationshipManagement.Models.Entities.Client> actualClients =
            await GetListAsync<cCoder.ClientRelationshipManagement.Models.Entities.Client>(BaseUrl);

        actualClients.Select(client => client.Id).Should().Contain(seededContext.Client.Id);

        await Teardown(seededContext);
    }

    [CRMAcceptanceFact]
    public async Task GetById_ReturnsSeededClient()
    {
        SeededClientContext seededContext = await SeedDatabase();

        cCoder.ClientRelationshipManagement.Models.Entities.Client? actualClient =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.Client>($"{BaseUrl}/{seededContext.Client.Id}");

        actualClient.Should().NotBeNull();
        actualClient!.Id.Should().Be(seededContext.Client.Id);
        actualClient.TenantId.Should().Be(seededContext.Client.TenantId);

        await Teardown(seededContext);
    }

    [CRMAcceptanceFact]
    public async Task GetById_WhenMissing_ReturnsNotFound()
    {
        HttpStatusCode actualStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{Guid.NewGuid()}");

        actualStatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
