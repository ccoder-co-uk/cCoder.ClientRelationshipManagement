using System.Net;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientContactControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_ReturnsSeededClientContacts()
    {
        SeededClientContactContext seededContext = await SeedDatabase();

        IReadOnlyList<cCoder.ClientRelationshipManagement.Models.Entities.ClientContact> actualContacts =
            await GetListAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientContact>(BaseUrl);

        actualContacts.Select(contact => contact.Id).Should().Contain(seededContext.ClientContact.Id);

        await Teardown(seededContext);
    }

    [CRMAcceptanceFact]
    public async Task GetById_ReturnsSeededClientContact()
    {
        SeededClientContactContext seededContext = await SeedDatabase();

        cCoder.ClientRelationshipManagement.Models.Entities.ClientContact? actualContact =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientContact>($"{BaseUrl}/{seededContext.ClientContact.Id}");

        actualContact.Should().NotBeNull();
        actualContact!.Id.Should().Be(seededContext.ClientContact.Id);
        actualContact.Name.Should().Be(seededContext.ClientContact.Name);

        await Teardown(seededContext);
    }

    [CRMAcceptanceFact]
    public async Task GetById_WhenMissing_ReturnsNotFound()
    {
        HttpStatusCode actualStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{Guid.NewGuid()}");

        actualStatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
