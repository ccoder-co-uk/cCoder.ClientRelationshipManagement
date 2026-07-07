using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class DocumentationControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_Documentation_RendersRootAndNestedPages()
    {
        string root = await GetStringAsync("/Documentation");
        string nested = await GetStringAsync("/Documentation/Pages/Home-Page");

        root.Should().Contain("Documentation");
        nested.Should().Contain("Home Page");
    }
}
