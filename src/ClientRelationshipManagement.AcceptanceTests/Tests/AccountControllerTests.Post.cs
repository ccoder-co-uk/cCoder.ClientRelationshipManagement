using System.Net;
using System.Net.Http.Json;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class AccountControllerTests
{
    [CRMAcceptanceFact]
    public async Task Post_Login_Logout_And_ApiAuth_AllWork()
    {
        AcceptanceSettings settings = CloneSettings(bypassAuthentication: false);
        await using CRMAcceptanceFactory factory = new(settings);
        await factory.EnsureSessionUserCanLoginAsync();

        HttpClient client = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

        using HttpResponseMessage loginResponse = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["User"] = settings.SessionUserEmail,
            ["Pass"] = settings.SessionUserPassword,
            ["ReturnUrl"] = "/"
        }));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        loginResponse.Headers.Location!.OriginalString.Should().Be("/");

        using HttpResponseMessage logoutResponse = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>()));
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        logoutResponse.Headers.Location!.OriginalString.Should().Contain("/Account/Login");

        using HttpResponseMessage apiLoginResponse = await client.PostAsJsonAsync("/Api/Account/Login", new
        {
            User = settings.SessionUserEmail,
            Pass = settings.SessionUserPassword
        });

        apiLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using HttpResponseMessage apiLogoutResponse = await client.PostAsync("/Api/Account/Logout", JsonContent.Create(new { }));
        apiLogoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
