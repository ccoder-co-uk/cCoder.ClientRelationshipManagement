using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

[Collection(CRMAcceptanceCollection.Name)]
public sealed class DocumentationPageTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    [CRMAcceptanceFact]
    public async Task Get_Root_ReturnsDocumentationWorkspace()
    {
        string response = await GetStringAsync("/Documentation");

        response.Should().Contain("CRM Library");
        response.Should().Contain("Initial structure");
        response.Should().Contain("Pages");
        response.Should().Contain("API");
    }

    [CRMAcceptanceFact]
    public async Task Get_ClientListPage_ReturnsOperationalGuidance()
    {
        string response = await GetStringAsync("/Documentation/Pages/Clients-Page");

        response.Should().Contain("Clients Page");
        response.Should().Contain("filtering and sorting");
    }
}
