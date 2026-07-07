using System.Net;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class AddressControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_ReturnsSeededAddresses()
    {
        SeededAddressContext seededContext = await SeedDatabase();

        IReadOnlyList<cCoder.ClientRelationshipManagement.Models.Entities.Address> actualAddresses =
            await GetListAsync<cCoder.ClientRelationshipManagement.Models.Entities.Address>(BaseUrl);

        actualAddresses.Select(address => address.Id).Should().Contain(seededContext.Address.Id);

        await Teardown(seededContext);
    }

    [CRMAcceptanceFact]
    public async Task GetById_ReturnsSeededAddress()
    {
        SeededAddressContext seededContext = await SeedDatabase();

        cCoder.ClientRelationshipManagement.Models.Entities.Address? actualAddress =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.Address>($"{BaseUrl}/{seededContext.Address.Id}");

        actualAddress.Should().NotBeNull();
        actualAddress!.Id.Should().Be(seededContext.Address.Id);
        actualAddress.Line1.Should().Be(seededContext.Address.Line1);

        await Teardown(seededContext);
    }

    [CRMAcceptanceFact]
    public async Task GetById_WhenMissing_ReturnsNotFound()
    {
        HttpStatusCode actualStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{Guid.NewGuid()}");

        actualStatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
