using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

[Collection(CRMAcceptanceCollection.Name)]
public sealed class CanonicalEntityCrudTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    [CRMAcceptanceFact]
    public async Task Metadata_IsPubliclyDiscoverableWithoutExposingEntityData()
    {
        using HttpResponseMessage response = await Client.GetAsync("/Api/ClientRelationshipManagement/$metadata");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [CRMAcceptanceFact]
    public async Task EntityData_RequiresBearerAuthorization()
    {
        AcceptanceSettings settings = CloneSettings(bypassAuthentication: false);
        await using CRMAcceptanceFactory factory = new(settings);
        using HttpClient client = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using HttpResponseMessage response = await client.GetAsync("/Api/ClientRelationshipManagement/Leads");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [CRMAcceptanceFact]
    public async Task Metadata_DescribesEveryTypedEntitySet()
    {
        using HttpRequestMessage request = await AuthorizedAsync(HttpMethod.Get, "/Api/ClientRelationshipManagement/$metadata");
        using HttpResponseMessage response = await Client.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        content.Should().Contain("EntitySet Name=\"Leads\"");
        content.Should().Contain("EntitySet Name=\"Companies\"");
        content.Should().Contain("EntitySet Name=\"ProcessDefinitions\"");
        content.Should().Contain("EntitySet Name=\"MailboxMessageRecords\"");
    }

    [CRMAcceptanceFact]
    public async Task Patch_TenantScopedRecord_UpdatesDataAndAuditThroughCanonicalContext()
    {
        (Guid leadId, _) = await SeedLeadAsync();
        using HttpRequestMessage request = await AuthorizedAsync(
            HttpMethod.Patch, $"/Api/ClientRelationshipManagement/Leads({leadId})");
        request.Content = JsonContent.Create(new { QualificationNotes = "Confirmed through canonical agent feedback." });
        using HttpResponseMessage response = await Client.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, content);

        var lead = await QueryInAdminContextAsync(db => db.Leads.AsNoTracking().SingleAsync(item => item.Id == leadId));
        lead.QualificationNotes.Should().Be("Confirmed through canonical agent feedback.");
        lead.LastUpdatedBy.Should().Be(Fixture.Settings.UserId);
    }

    async Task<HttpRequestMessage> AuthorizedAsync(HttpMethod method, string path)
    {
        string token = await Fixture.IssueAgentTokenAsync();
        HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
