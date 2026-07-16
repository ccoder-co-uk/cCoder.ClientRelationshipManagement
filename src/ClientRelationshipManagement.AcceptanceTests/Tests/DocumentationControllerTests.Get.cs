using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class DocumentationControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_Documentation_RendersRootAndNestedPages()
    {
        string root = await GetStringAsync("/Documentation");
        string nested = await GetStringAsync("/Documentation/Pages/Home");

        root.Should().Contain("Documentation");
        nested.Should().Contain("<h1>Home</h1>");
        root.Should().NotContain(">Home Page</a>");
        root.Should().NotContain(">Leads Page</a>");
        root.Should().NotContain(">Opportunities Page</a>");
        root.Should().NotContain(">Clients Page</a>");
    }

    [CRMAcceptanceFact]
    public async Task Get_Documentation_MirrorsTheAlphabeticalAdminNavigationHierarchy()
    {
        string root = await GetStringAsync("/Documentation");
        string admin = await GetStringAsync("/Documentation/Pages/Admin");

        root.Should().Contain("href=\"/Documentation/Pages/Admin\"");
        root.Should().Contain("href=\"/Documentation/Pages/Admin/Agent-Messages\"");
        root.Should().Contain("href=\"/Documentation/Pages/Admin/Agent-Runs\"");
        root.Should().Contain("href=\"/Documentation/Pages/Admin/Emails\"");
        root.Should().Contain("href=\"/Documentation/Pages/Admin/Imports\"");
        root.Should().Contain("href=\"/Documentation/Pages/Admin/Process\"");
        root.Should().Contain("href=\"/Documentation/Pages/Admin/Process-Proposals\"");
        root.IndexOf("/Documentation/Pages/Admin/Agent-Messages\"", StringComparison.Ordinal)
            .Should().BeLessThan(root.IndexOf("/Documentation/Pages/Admin/Agent-Runs\"", StringComparison.Ordinal));
        root.IndexOf("/Documentation/Pages/Admin/Agent-Runs\"", StringComparison.Ordinal)
            .Should().BeLessThan(root.IndexOf("/Documentation/Pages/Admin/Emails\"", StringComparison.Ordinal));
        root.IndexOf("/Documentation/Pages/Admin/Emails\"", StringComparison.Ordinal)
            .Should().BeLessThan(root.IndexOf("/Documentation/Pages/Admin/Imports\"", StringComparison.Ordinal));
        root.IndexOf("/Documentation/Pages/Admin/Imports\"", StringComparison.Ordinal)
            .Should().BeLessThan(root.IndexOf("/Documentation/Pages/Admin/Process\"", StringComparison.Ordinal));
        root.IndexOf("/Documentation/Pages/Admin/Process\"", StringComparison.Ordinal)
            .Should().BeLessThan(root.IndexOf("/Documentation/Pages/Admin/Process-Proposals\"", StringComparison.Ordinal));
        admin.Should().Contain("AI routing");
    }
}
