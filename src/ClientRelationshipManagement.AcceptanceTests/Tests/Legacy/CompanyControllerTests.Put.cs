using System.Net;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class CompanyControllerTests
{
    [CRMAcceptanceFact]
    public async Task Put_UpdatesCompany()
    {
        SeededCompanyContext seededContext = await SeedDatabase();
        seededContext.Company.Name = Unique("updated-company");
        seededContext.Company.ContactEmailAddress = $"{Unique("updated-company")}@example.com";

        HttpStatusCode actualStatusCode =
            await PutAsync($"{BaseUrl}/{seededContext.Company.Id}", ToPayload(seededContext.Company));
        cCoder.ClientRelationshipManagement.Models.Entities.Company? actualCompany =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.Company>($"{BaseUrl}/{seededContext.Company.Id}");

        actualStatusCode.Should().Be(HttpStatusCode.OK);
        actualCompany.Should().NotBeNull();
        actualCompany!.Name.Should().Be(seededContext.Company.Name);
        actualCompany.ContactEmailAddress.Should().Be(seededContext.Company.ContactEmailAddress);

        await Teardown(seededContext);
    }
}
