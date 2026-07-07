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

        html.Should().Contain("Opportunities Page");
        html.Should().Contain("Primary Contact");
        html.Should().Contain("View client");
    }
}
