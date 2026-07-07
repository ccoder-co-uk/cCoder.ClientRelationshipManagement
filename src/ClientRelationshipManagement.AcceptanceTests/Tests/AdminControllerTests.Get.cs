using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class AdminControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_Index_RendersOperationalAdminDashboard()
    {
        string html = await GetStringAsync("/Admin");

        html.Should().Contain("Operational visibility for agent runs, approvals, and process improvement proposals.");
        html.Should().Contain("Pending Agent Messages");
        html.Should().Contain("Recent Agent Runs");
        html.Should().Contain("Draft Process Proposals");
    }
}
