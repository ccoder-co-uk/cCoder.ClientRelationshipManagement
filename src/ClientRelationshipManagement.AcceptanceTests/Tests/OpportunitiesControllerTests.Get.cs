using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class OpportunitiesControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_Index_RendersOpportunityGrid()
    {
        await SeedOpportunityWorkspaceAsync();

        string html = await GetStringAsync("/Opportunities");

        html.Should().Contain("Opportunity pipeline");
        html.Should().Contain("Primary Contact");
        html.Should().NotContain("View client");
        html.Should().NotContain("Open commercial workspace");
        html.Should().Contain("class=\"tree-grid__toggle\"");
        html.Should().Contain($"/Opportunities/");
    }

    [CRMAcceptanceFact]
    public async Task Get_Details_SurfacesOpportunityAndInheritedProcessEvidence()
    {
        (_, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();

        string html = await GetStringAsync($"/Opportunities/{opportunityId}/Details");

        html.Should().Contain("Opportunity evidence");
        html.Should().Contain("Needs a structured outreach path.");
        html.Should().Contain("We can reduce onboarding friction.");
        html.Should().Contain("Artifact feed");
    }

    [CRMAcceptanceFact]
    public async Task Get_Index_AcceptsLaneStatusAndTaskFilters()
    {
        await SeedOpportunityWorkspaceAsync();

        string html = await GetStringAsync("/Opportunities?status=ActiveOpportunity&tasks=open");

        html.Should().Contain("selected=\"selected\" value=\"ActiveOpportunity\"");
        html.Should().Contain("name=\"tasks\" value=\"open\"");
    }
}
