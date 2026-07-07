using cCoder.ClientRelationshipManagement.Models.Entities;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class EmailControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_ReturnsSeededEmails()
    {
        SeededEmailContext seededContext = await SeedDatabase();

        IReadOnlyList<Email> actualEmails = await GetListAsync<Email>(BaseUrl);

        actualEmails.Select(email => email.Id).Should().Contain(seededContext.Email.Id);

        await Teardown(seededContext);
    }

    [CRMAcceptanceFact]
    public async Task GetById_ReturnsSeededEmail()
    {
        SeededEmailContext seededContext = await SeedDatabase();

        Email? actualEmail = await GetAsync<Email>($"{BaseUrl}/{seededContext.Email.Id}");

        actualEmail.Should().NotBeNull();
        actualEmail!.Id.Should().Be(seededContext.Email.Id);
        actualEmail.Subject.Should().Be(seededContext.Email.Subject);

        await Teardown(seededContext);
    }
}
