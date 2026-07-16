using System.Net;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class AdminControllerTests
{
    [CRMAcceptanceFact]
    public async Task Post_SelectAiProfile_PersistsRuntimeRoute()
    {
        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync(
            "/Admin",
            "/Admin/SelectAiProfile",
            new Dictionary<string, string> { ["profileKey"] = "local-ollama" });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        AgentAutomationSetting setting = await QueryInAdminContextAsync(db =>
            db.AgentAutomationSettings.FirstAsync(item => item.UserId == Fixture.Settings.UserId));
        setting.SelectedAiProfileKey.Should().Be("local-ollama");
    }

    [CRMAcceptanceFact]
    public async Task Post_ConfigureAiLane_PersistsProviderAndConcurrency()
    {
        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync(
            "/Admin",
            "/Admin/ConfigureAiLane",
            new Dictionary<string, string>
            {
                ["lane"] = "Opportunity",
                ["profileKey"] = "open-ai",
                ["concurrency"] = "4"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        AgentAutomationSetting setting = await QueryInAdminContextAsync(db =>
            db.AgentAutomationSettings.FirstAsync(item => item.UserId == Fixture.Settings.UserId));
        setting.OpportunityAiProfileKey.Should().Be("open-ai");
        setting.OpportunityAgentConcurrency.Should().Be(4);
    }

    [CRMAcceptanceFact]
    public async Task Post_ConfigureAiLane_NoneMakesLaneHumanManaged()
    {
        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync(
            "/Admin",
            "/Admin/ConfigureAiLane",
            new Dictionary<string, string>
            {
                ["lane"] = "Client",
                ["profileKey"] = "none",
                ["concurrency"] = "9"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        AgentAutomationSetting setting = await QueryInAdminContextAsync(db =>
            db.AgentAutomationSettings.FirstAsync(item => item.UserId == Fixture.Settings.UserId));
        setting.ClientAiProfileKey.Should().Be("none");
    }

    [CRMAcceptanceFact]
    public async Task Post_ConfigureAiRouting_PersistsAllRoutesTogether()
    {
        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync(
            "/Admin",
            "/Admin/ConfigureAiRouting",
            new Dictionary<string, string>
            {
                ["ApprovalProfileKey"] = "codex",
                ["ApprovalModel"] = "gpt-5.6-luna",
                ["ApprovalConcurrency"] = "2",
                ["LeadProfileKey"] = "local-ollama",
                ["LeadModel"] = "qwen3.5:4b",
                ["LeadConcurrency"] = "1",
                ["OpportunityProfileKey"] = "open-ai",
                ["OpportunityModel"] = "gpt-5.6-luna",
                ["OpportunityConcurrency"] = "6",
                ["ClientProfileKey"] = "none",
                ["ClientModel"] = "",
                ["ClientConcurrency"] = "1"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        AgentAutomationSetting setting = await QueryInAdminContextAsync(db =>
            db.AgentAutomationSettings.FirstAsync(item => item.UserId == Fixture.Settings.UserId));
        setting.SelectedAiProfileKey.Should().Be("codex");
        setting.SelectedAiModel.Should().Be("gpt-5.6-luna");
        setting.ApprovalAgentConcurrency.Should().Be(2);
        setting.LeadAiProfileKey.Should().Be("local-ollama");
        setting.LeadAiModel.Should().Be("qwen3.5:4b");
        setting.LeadAgentConcurrency.Should().Be(1);
        setting.OpportunityAiProfileKey.Should().Be("open-ai");
        setting.OpportunityAiModel.Should().Be("gpt-5.6-luna");
        setting.OpportunityAgentConcurrency.Should().Be(6);
        setting.ClientAiProfileKey.Should().Be("none");
    }
}
