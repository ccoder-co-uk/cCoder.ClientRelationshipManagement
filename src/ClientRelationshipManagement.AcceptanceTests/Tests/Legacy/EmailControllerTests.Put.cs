using System.Net;
using cCoder.ClientRelationshipManagement.Models.Entities;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class EmailControllerTests
{
    [CRMAcceptanceFact]
    public async Task Put_UpdatesEmail()
    {
        SeededEmailContext seededContext = await SeedDatabase();
        seededContext.Email.Subject = Unique("updated-email-subject");
        seededContext.Email.BodyText = Unique("updated-email-body");

        HttpStatusCode actualStatusCode =
            await PutAsync($"{BaseUrl}/{seededContext.Email.Id}", ToPayload(seededContext.Email));
        Email? actualEmail = await GetAsync<Email>($"{BaseUrl}/{seededContext.Email.Id}");

        actualStatusCode.Should().Be(HttpStatusCode.OK);
        actualEmail.Should().NotBeNull();
        actualEmail!.Subject.Should().Be(seededContext.Email.Subject);
        actualEmail.BodyText.Should().Be(seededContext.Email.BodyText);

        await Teardown(seededContext);
    }
}
