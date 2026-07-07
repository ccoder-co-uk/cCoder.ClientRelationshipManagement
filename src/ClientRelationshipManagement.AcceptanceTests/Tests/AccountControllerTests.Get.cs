using System.Net;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class AccountControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_Login_ReturnsLoginPageWhenUnauthenticated()
    {
        AcceptanceSettings settings = CloneSettings(bypassAuthentication: false);
        await using CRMAcceptanceFactory factory = new(settings);
        HttpClient client = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

        string html = await GetStringAsync(client, "/Account/Login");
        html.Should().Contain("Login");
    }
}
