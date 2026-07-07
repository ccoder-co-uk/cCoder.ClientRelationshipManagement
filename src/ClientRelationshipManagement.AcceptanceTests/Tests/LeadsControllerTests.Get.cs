using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class LeadsControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_Index_And_Edit_RenderSeededLead()
    {
        (Guid leadId, _) = await SeedLeadAsync();

        string indexHtml = await GetStringAsync("/Leads");
        string editHtml = await GetStringAsync($"/Leads/Edit/{leadId}");

        indexHtml.Should().Contain("Leads");
        indexHtml.Should().Contain("Lead Queue");
        indexHtml.Should().Contain("New Lead");
        indexHtml.Should().Contain("Bulk Import");
        editHtml.Should().Contain("Lead Details");
    }
}
