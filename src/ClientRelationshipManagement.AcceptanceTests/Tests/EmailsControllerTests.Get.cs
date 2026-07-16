using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class EmailsControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_Index_RendersEmailQueue()
    {
        string html = await GetStringAsync("/Admin/Emails");
        html.Should().Contain("Emails");
    }
}
