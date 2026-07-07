using System.Net;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class CompanyControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_ReturnsSeededCompanies()
    {
        SeededCompanyContext seededContext = await SeedDatabase();

        IReadOnlyList<cCoder.ClientRelationshipManagement.Models.Entities.Company> actualCompanies =
            await GetListAsync<cCoder.ClientRelationshipManagement.Models.Entities.Company>(BaseUrl);

        actualCompanies.Select(company => company.Id).Should().Contain(seededContext.Company.Id);

        await Teardown(seededContext);
    }

    [CRMAcceptanceFact]
    public async Task GetById_ReturnsSeededCompany()
    {
        SeededCompanyContext seededContext = await SeedDatabase();

        cCoder.ClientRelationshipManagement.Models.Entities.Company? actualCompany =
            await GetAsync<cCoder.ClientRelationshipManagement.Models.Entities.Company>($"{BaseUrl}/{seededContext.Company.Id}");

        actualCompany.Should().NotBeNull();
        actualCompany!.Id.Should().Be(seededContext.Company.Id);
        actualCompany.Name.Should().Be(seededContext.Company.Name);

        await Teardown(seededContext);
    }

    [CRMAcceptanceFact]
    public async Task GetById_WhenMissing_ReturnsNotFound()
    {
        HttpStatusCode actualStatusCode = await GetStatusCodeAsync($"{BaseUrl}/{Guid.NewGuid()}");

        actualStatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
