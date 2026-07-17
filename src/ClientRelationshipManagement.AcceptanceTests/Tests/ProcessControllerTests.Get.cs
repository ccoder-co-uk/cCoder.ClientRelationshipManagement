using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ProcessControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_Index_And_Edit_ShowProcessDesigner()
    {
        string indexResponse = await GetStringAsync("/Admin/Process");

        ProcessDefinition definition = await QueryInAdminContextAsync(db =>
            db.ProcessDefinitions
                .Where(item => item.TenantId == AcceptanceSettings.TenantId
                    && item.ScopeType == ProcessScopeType.Opportunity)
                .OrderByDescending(item => item.IsDefault)
                .ThenBy(item => item.Name)
                .FirstAsync());

        string editResponse = await GetStringAsync($"/Admin/Process/Edit/{definition.Id}");

        indexResponse.Should().Contain("Process List");
        indexResponse.Should().Contain("Lead Generation");
        indexResponse.Should().Contain("Opportunity Conversion");
        indexResponse.Should().Contain("Client Maintenance");
        editResponse.Should().Contain("Process Details");
        editResponse.Should().Contain("Step List");
        editResponse.Should().Contain("Supported Tokens");
        editResponse.Should().Contain("4 tasks");
        editResponse.Should().Contain("Validate complete sendable email");
        editResponse.Should().Contain("CRM.ValidateRecipientEmail");
    }

    [CRMAcceptanceFact]
    public async Task Get_Designer_ComposesValidatedLifecycleAndStepHealth()
    {
        string html = await GetStringAsync("/Admin/Process/Designer");

        html.Should().Contain("Company Lifecycle");
        html.Should().Contain("Lead Generation");
        html.Should().Contain("Opportunity Conversion");
        html.Should().Contain("Client Maintenance");
        html.Should().Contain("Assess Company Scale");
        html.Should().Contain("Build Opportunity Summary");
        html.Should().Contain("Record Client Baseline");
        html.Should().Contain("Process contracts are complete");
        html.Should().Contain("data-step-list");
        html.Should().Contain("ConnectSteps");
    }

    [CRMAcceptanceFact]
    public async Task Get_WorkflowModel_ShowsTheLiveEndToEndLifecycleWithInPageDrillDown()
    {
        string html = await GetStringAsync("/Admin/Process/WorkflowModel");

        html.Should().Contain("Full Company Process Model");
        html.Should().NotContain("Live process definition");
        html.Should().Contain("Company tipped in");
        html.Should().Contain("Opportunity created");
        html.Should().Contain("Client account created");
        html.Should().Contain("workflowConnectors");
        html.Should().Contain("data-inspector-template");
        html.Should().Contain("workflowInspectorContext");
        html.Should().Contain("data-context=");
        html.Should().Contain("Permitted next routes");
        html.Should().Contain("Lead Generation");
        html.Should().Contain("Opportunity Conversion");
        html.Should().Contain("Client Maintenance");
        html.Should().Contain("companies have a visible place");
        html.Should().Contain("data-workflow-auto-refresh");
        html.Should().Contain("data-live-key=\"company-coverage\"");
        html.Should().Contain("waiting in the pool");
        html.Should().Contain("data-edge-track");
        html.Should().Contain("data-node-row=\"0\"");
        html.Should().Contain("data-node-row=\"1\"");
        html.Should().Contain("chooseConnectorSides");
        html.Should().Contain("side-channel");
        html.Should().Contain("parallel-return");
        html.Should().Contain("workflow-node__iteration");
        html.Should().Contain("Repeating step");
    }
}
