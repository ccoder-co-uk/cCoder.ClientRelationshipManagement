using System.Net;
using System.Net.Http.Json;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientControllerTests
{
    [CRMAcceptanceFact]
    public async Task Post_WhenPrivilegeIsMissing_ReturnsUnauthorized()
    {
        AcceptanceSettings deniedSettings = new()
        {
            CrmConnectionString = Fixture.Settings.CrmConnectionString,
            CrmAdminConnectionString = Fixture.Settings.CrmAdminConnectionString,
            SsoConnectionString = Fixture.Settings.SsoConnectionString,
            DecryptionKey = Fixture.Settings.DecryptionKey,
            UserId = "crm-denied-user",
            GrantCrmPrivileges = false,
        };

        await using var deniedFactory = new CRMAcceptanceFactory(deniedSettings);
        HttpClient deniedClient = deniedFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        HttpResponseMessage response = await deniedClient.PostAsJsonAsync(
            BaseUrl,
            NewClient());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
