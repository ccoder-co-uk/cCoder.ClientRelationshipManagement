using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

[Collection(CRMAcceptanceCollection.Name)]
public sealed class CompaniesControllerTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    [CRMAcceptanceFact]
    public async Task Get_Index_RendersPagedSearchableCompanyExplorer()
    {
        (Guid leadId, _) = await SeedLeadAsync();
        var company = await QueryInAdminContextAsync(db => db.Leads
            .Where(item => item.Id == leadId)
            .Select(item => new { item.CompanyId, item.Company.OfficialName })
            .SingleAsync());

        string html = await GetStringAsync($"/Companies?search={Uri.EscapeDataString(company.OfficialName)}&pageSize=25");

        html.Should().Contain("Explore imported and captured company master data");
        html.Should().Contain(company.OfficialName);
        html.Should().Contain($"data-company-expand=\"{company.CompanyId}\"");
        html.Should().Contain("Apply filters");
        html.Should().Contain("Page 1");
    }

    [CRMAcceptanceFact]
    public async Task Get_Details_RendersLazyChildCollectionTabs()
    {
        (Guid leadId, _) = await SeedLeadAsync();
        Guid companyId = await QueryInAdminContextAsync(db => db.Leads
            .Where(item => item.Id == leadId)
            .Select(item => item.CompanyId)
            .SingleAsync());

        string html = await GetStringAsync($"/Companies/{companyId}/Details");

        html.Should().Contain("Overview");
        html.Should().Contain("Contacts");
        html.Should().Contain("Commercial");
        html.Should().Contain("Scheduled work");
        html.Should().Contain("History");
        html.Should().Contain("Lead");
    }
}
