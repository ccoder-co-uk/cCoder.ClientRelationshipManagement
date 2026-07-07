using System.Net;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class AddressControllerTests
{
    [CRMAcceptanceFact]
    public async Task Put_UpdatesAddress()
    {
        SeededAddressContext seededContext = await SeedDatabase();
        seededContext.Address.Line1 = Unique("updated-line1");
        seededContext.Address.TownOrCity = "Portsmouth";

        HttpStatusCode actualStatusCode =
            await PutAsync($"{BaseUrl}/{seededContext.Address.Id}", ToPayload(seededContext.Address));
        cCoder.ClientRelationshipManagement.Models.Entities.Address? actualAddress =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.Address>($"{BaseUrl}/{seededContext.Address.Id}");

        actualStatusCode.Should().Be(HttpStatusCode.OK);
        actualAddress.Should().NotBeNull();
        actualAddress!.Line1.Should().Be(seededContext.Address.Line1);
        actualAddress.TownOrCity.Should().Be(seededContext.Address.TownOrCity);

        await Teardown(seededContext);
    }
}
