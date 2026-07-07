using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class AgentWorkflowControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_DueTasks_RequiresBearerAuthorization()
    {
        using HttpResponseMessage response = await Client.GetAsync("/Api/AgentWorkflow/Tasks/Due");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [CRMAcceptanceFact]
    public async Task Get_DueTasks_ReturnsPendingWorkflowTasks()
    {
        (_, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true).AsTask());
        string token = await Fixture.IssueAgentTokenAsync();

        using HttpRequestMessage request = new(HttpMethod.Get, "/Api/AgentWorkflow/Tasks/Due");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage response = await Client.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);

        using JsonDocument jsonDocument = JsonDocument.Parse(content);
        jsonDocument.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        jsonDocument.RootElement.GetArrayLength().Should().BeGreaterThan(0);
    }
}
