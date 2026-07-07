using System.Net;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class HomeControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_Index_ReturnsDashboard()
    {
        string response = await GetStringAsync("/");

        response.Should().Contain("Action Board");
        response.Should().Contain("Total Clients");
        response.Should().Contain("/Clients");
        response.Should().Contain("/Documentation");
        response.Should().Contain("Signed In As");
        response.Should().Contain("CRM Acceptance User");
        response.Should().Contain("/Opportunities");
    }

    [CRMAcceptanceFact]
    public async Task Get_Index_ShowsClientDetailsLink_InScheduledActions()
    {
        (_, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true).AsTask());

        string response = await GetStringAsync("/");

        response.Should().Contain("View Client Details");
        response.Should().NotContain("View workspace");
    }

    [CRMAcceptanceFact]
    public async Task Get_Index_WhenUnauthenticated_RedirectsToLogin()
    {
        AcceptanceSettings unauthenticatedSettings = CloneSettings(bypassAuthentication: false);

        await using var unauthenticatedFactory = new CRMAcceptanceFactory(unauthenticatedSettings);
        using HttpClient unauthenticatedClient = unauthenticatedFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using HttpResponseMessage response = await unauthenticatedClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.OriginalString.Should().Be("/Account/Login?returnUrl=%2F");
    }
}
