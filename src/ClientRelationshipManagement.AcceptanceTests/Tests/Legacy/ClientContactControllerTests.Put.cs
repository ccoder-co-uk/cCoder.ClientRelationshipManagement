using System.Net;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientContactControllerTests
{
    [CRMAcceptanceFact]
    public async Task Put_UpdatesClientContact()
    {
        SeededClientContactContext seededContext = await SeedDatabase();
        seededContext.ClientContact.Name = Unique("updated-contact");
        seededContext.ClientContact.EmailAddress = $"{Unique("updated-contact")}@example.com";

        HttpStatusCode actualStatusCode =
            await PutAsync($"{BaseUrl}/{seededContext.ClientContact.Id}", ToPayload(seededContext.ClientContact));
        cCoder.ClientRelationshipManagement.Models.Entities.ClientContact? actualContact =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.ClientContact>($"{BaseUrl}/{seededContext.ClientContact.Id}");

        actualStatusCode.Should().Be(HttpStatusCode.OK);
        actualContact.Should().NotBeNull();
        actualContact!.Name.Should().Be(seededContext.ClientContact.Name);
        actualContact.EmailAddress.Should().Be(seededContext.ClientContact.EmailAddress);

        await Teardown(seededContext);
    }
}
