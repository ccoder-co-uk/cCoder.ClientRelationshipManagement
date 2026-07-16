using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class HomeControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_Index_ReturnsDashboard()
    {
        string response = await GetStringAsync("/");

        response.Should().Contain("Action Board");
        response.Should().Contain("Email auto-approval");
        response.Should().Contain("Client Accounts");
        response.Should().Contain("/Clients");
        response.Should().Contain("/Documentation");
        response.Should().Contain("Signed In As");
        response.Should().Contain("CRM Acceptance User");
        response.Should().Contain("/Opportunities");
        response.Should().Contain("data-dashboard-refresh-status");
        response.Should().Contain("Live updates every 10 seconds");
        response.Should().Contain("window.setInterval(refreshStats, 10_000)");
        response.Should().Contain("refreshStats();");
        response.Should().Contain("visibilitychange");
        response.Should().Contain("event.persisted");
        response.Should().Contain("/Home/Stats");
        response.Should().Contain("lane-stats--lead");
        response.Should().Contain("lane-stats--opportunity");
        response.Should().Contain("lane-stats--client");
        response.Should().Contain("data-agent-health=\"lead\"");
        response.Should().Contain("data-agent-health=\"opportunity\"");
        response.Should().Contain("data-agent-health=\"client\"");
        response.Should().Contain("href=\"/Admin/Runs\"");
        response.Should().Contain("syncAgentHealth(\"lead\"");
        response.Should().Contain("href=\"/Leads?scope=active\"");
        response.Should().Contain("href=\"/Leads?scope=candidates\"");
        response.Should().Contain("href=\"/Leads?scope=suppressed\"");
        response.Should().Contain("href=\"/Leads?tasks=due-today\"");
        response.Should().Contain("href=\"/Opportunities?scope=active\"");
        response.Should().Contain("href=\"/Opportunities?tasks=overdue\"");
        response.Should().Contain("href=\"/Clients?scope=accounts\"");
        response.Should().Contain("href=\"/Clients?tasks=open\"");
        response.Should().NotContain("<article class=\"metric-pill");
    }

    [CRMAcceptanceFact]
    public async Task Get_Stats_ReturnsLiveDashboardCountersWithoutCaching()
    {
        using HttpResponseMessage response = await Client.GetAsync("/Home/Stats");
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();

        using JsonDocument document = JsonDocument.Parse(content);
        JsonElement root = document.RootElement;
        root.GetProperty("totalClients").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        root.GetProperty("totalOpenActions").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        root.GetProperty("queueVersion").GetInt64().Should().BeGreaterThanOrEqualTo(0);
        root.GetProperty("leadActions").GetProperty("open").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        root.GetProperty("opportunityActions").GetProperty("dueToday").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        root.GetProperty("clientActions").GetProperty("overdue").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        root.GetProperty("leadAgentHealth").GetProperty("status").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("opportunityAgentHealth").GetProperty("sampleSize").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        root.GetProperty("clientAgentHealth").GetProperty("failed").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        root.GetProperty("clientStates").GetArrayLength().Should().BeGreaterThan(0);
        root.GetProperty("updatedOn").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [CRMAcceptanceFact]
    public async Task Get_Index_AndStats_ShowTheExactTaskClaimedByAnAgent()
    {
        (_, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true).AsTask());
        var task = await QueryInAdminContextAsync(db => db.ProcessTasks
            .FirstAsync(item => item.OpportunityId == opportunityId
                && item.State == cCoder.ClientRelationshipManagement.Platform.Models.Enums.ProcessTaskState.Pending));

        await ExecuteInAdminContextAsync(async db =>
        {
            var tracked = await db.ProcessTasks.FirstAsync(item => item.Id == task.Id);
            tracked.AgentClaimId = Guid.NewGuid();
            tracked.AgentClaimedBy = Fixture.Settings.UserId;
            tracked.AgentClaimedOn = DateTimeOffset.UtcNow;
            tracked.AgentClaimExpiresOn = DateTimeOffset.UtcNow.AddMinutes(5);
            await db.SaveChangesAsync();
        });

        string dashboard = await GetStringAsync("/");
        dashboard.Should().Contain($"data-task-id=\"{task.Id}\"");
        dashboard.Should().Contain("is-agent-active");
        dashboard.Should().Contain("aria-busy=\"true\"");
        dashboard.Should().Contain("AI agent working");

        using HttpResponseMessage response = await Client.GetAsync("/Home/Stats");
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("activeTaskIds")
            .EnumerateArray()
            .Select(item => item.GetGuid())
            .Should().Contain(task.Id);
    }

    [CRMAcceptanceFact]
    public async Task Get_Stats_ChangesQueueVersionWhenAVisibleTaskCompletes()
    {
        (_, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true).AsTask());

        long beforeVersion = await GetQueueVersionAsync();
        var task = await QueryInAdminContextAsync(db => db.ProcessTasks
            .Include(item => item.ProcessStep)
                .ThenInclude(step => step.OutgoingTransitions)
            .FirstAsync(item => item.OpportunityId == opportunityId && item.State == cCoder.ClientRelationshipManagement.Platform.Models.Enums.ProcessTaskState.Pending));
        string outcomeKey = task.ProcessStep.OutgoingTransitions
            .OrderByDescending(item => item.IsDefaultOutcome)
            .First()
            .OutcomeKey;

        await ExecuteWorkflowAsync(service => service.CompleteTaskAsync(
            new ClientRelationshipManagement.Web.Services.Processes.ProcessTaskCompletionCommand
            {
                ProcessTaskId = task.Id,
                OutcomeKey = outcomeKey,
                CompletionNote = "Acceptance test completion."
            }).AsTask());

        long afterVersion = await GetQueueVersionAsync();
        afterVersion.Should().BeGreaterThan(beforeVersion);
    }

    async Task<long> GetQueueVersionAsync()
    {
        using HttpResponseMessage response = await Client.GetAsync("/Home/Stats");
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("queueVersion").GetInt64();
    }

    [CRMAcceptanceFact]
    public async Task Get_Index_PresentsTheNextRunnableTaskFirst()
    {
        (_, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true).AsTask());
        string token = await Fixture.IssueAgentTokenAsync();
        using HttpRequestMessage request = new(HttpMethod.Get, "/Api/AgentWorkflow/Tasks/Due?limit=1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using JsonDocument taskDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Guid nextAgentTaskId = taskDocument.RootElement[0].GetProperty("processTaskId").GetGuid();

        string dashboard = await GetStringAsync("/");
        Match firstCard = Regex.Match(
            dashboard,
            "<article[^>]*class=\"todo-card[^\"]*\"[^>]*data-task-id=\"([^\"]+)\"[^>]*>",
            RegexOptions.CultureInvariant);

        firstCard.Success.Should().BeTrue();
        Guid.Parse(firstCard.Groups[1].Value).Should().Be(nextAgentTaskId);
    }

    [CRMAcceptanceFact]
    public async Task Get_Index_ShowsClientDetailsLink_InScheduledActions()
    {
        (_, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true).AsTask());

        string response = await GetStringAsync("/");

        response.Should().Contain("View Client Details");
        response.Should().Contain("todo-card--opportunity");
        response.Should().NotContain("<span class=\"todo-type\">Process</span>");
        response.Should().NotContain("<p class=\"todo-context\">Opportunity</p>");
        response.IndexOf("<div class=\"todo-card__content\">", StringComparison.Ordinal)
            .Should().BeLessThan(response.IndexOf("<div class=\"todo-meta\">", StringComparison.Ordinal));
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
