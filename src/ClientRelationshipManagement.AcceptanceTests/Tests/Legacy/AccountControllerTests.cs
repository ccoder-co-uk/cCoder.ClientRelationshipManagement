using System.Net;
using System.Net.Http.Json;
using cCoder.Security.Objects.DTOs;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

[Collection(CRMAcceptanceCollection.Name)]
public sealed class AccountControllerTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    [CRMAcceptanceFact]
    public async Task Get_Login_ReturnsLoginPageWhenUnauthenticated()
    {
        await using CRMAcceptanceFactory factory = CreateSessionAuthFactory();
        using HttpClient client = CreateCookieClient(factory);

        HttpResponseMessage response = await client.GetAsync("/Account/Login");
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("CRM Login");
        content.Should().Contain("Username");
    }

    [CRMAcceptanceFact]
    public async Task Post_LoginForm_AllowsDashboardAccess()
    {
        await using CRMAcceptanceFactory factory = CreateSessionAuthFactory();
        await factory.EnsureSessionUserCanLoginAsync();
        using HttpClient client = CreateCookieClient(factory);

        using HttpResponseMessage response = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["User"] = Fixture.Settings.SessionUserEmail,
                ["Pass"] = Fixture.Settings.SessionUserPassword,
                ["ReturnUrl"] = "/",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.OriginalString.Should().Be("/");

        string dashboard = await client.GetStringAsync("/");
        dashboard.Should().Contain("Action Board");
    }

    [CRMAcceptanceFact]
    public async Task Post_ApiLogin_AllowsAuthenticatedHomeRequest()
    {
        await using CRMAcceptanceFactory factory = CreateSessionAuthFactory();
        await factory.EnsureSessionUserCanLoginAsync();
        using HttpClient client = CreateCookieClient(factory);

        HttpResponseMessage loginResponse = await client.PostAsJsonAsync(
            "/Api/Account/Login",
            new Auth
            {
                User = Fixture.Settings.SessionUserEmail,
                Pass = Fixture.Settings.SessionUserPassword,
            });

        string loginContent = await loginResponse.Content.ReadAsStringAsync();
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, loginContent);

        HttpResponseMessage response = await client.GetAsync("/");
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        content.Should().Contain("Action Board");
    }

    static HttpClient CreateCookieClient(CRMAcceptanceFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost"),
        });

    CRMAcceptanceFactory CreateSessionAuthFactory()
    {
        AcceptanceSettings sessionSettings = new()
        {
            CrmConnectionString = Fixture.Settings.CrmConnectionString,
            CrmAdminConnectionString = Fixture.Settings.CrmAdminConnectionString,
            SsoConnectionString = Fixture.Settings.SsoConnectionString,
            DecryptionKey = Fixture.Settings.DecryptionKey,
            UserId = Fixture.Settings.UserId,
            GrantCrmPrivileges = Fixture.Settings.GrantCrmPrivileges,
            BypassAuthentication = false,
            SessionUserEmail = Fixture.Settings.SessionUserEmail,
            SessionUserPassword = Fixture.Settings.SessionUserPassword,
        };

        return new CRMAcceptanceFactory(sessionSettings);
    }
}
