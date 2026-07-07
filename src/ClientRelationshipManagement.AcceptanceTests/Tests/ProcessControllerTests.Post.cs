using System.Net;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ProcessControllerTests
{
    [CRMAcceptanceFact]
    public async Task Post_SaveDefinition_SaveStep_SaveTransition_DeleteTransition_DeleteStep_And_DeleteDefinition_Work()
    {
        using HttpResponseMessage definitionResponse = await PostFormWithAntiforgeryAsync("/Process", "/Process/SaveDefinition", new Dictionary<string, string>
        {
            ["TenantId"] = AcceptanceSettings.TenantId,
            ["ScopeType"] = ProcessScopeType.Opportunity.ToString(),
            ["Name"] = "Acceptance Process",
            ["Description"] = "Created by acceptance test",
            ["IsDefault"] = "false",
            ["IsActive"] = "true"
        });

        definitionResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        ProcessDefinition definition = await QueryInAdminContextAsync(db =>
            db.ProcessDefinitions.OrderByDescending(item => item.CreatedOn).FirstAsync(item => item.Name == "Acceptance Process"));

        using HttpResponseMessage stepResponse = await PostFormWithAntiforgeryAsync($"/Process/Edit/{definition.Id}", "/Process/SaveStep", new Dictionary<string, string>
        {
            ["ProcessDefinitionId"] = definition.Id.ToString(),
            ["Key"] = "acceptance-step",
            ["Name"] = "Acceptance Step",
            ["Sequence"] = "10",
            ["ActionType"] = ProcessActionType.Review.ToString(),
            ["DueAfterDays"] = "0",
            ["DueAfterHours"] = "0",
            ["TaskTitleTemplate"] = "Review acceptance step",
            ["IsEntryPoint"] = "true",
            ["IsActive"] = "true"
        });

        stepResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        ProcessStep step = await QueryInAdminContextAsync(db =>
            db.ProcessSteps.FirstAsync(item => item.ProcessDefinitionId == definition.Id && item.Key == "acceptance-step"));

        using HttpResponseMessage transitionResponse = await PostFormWithAntiforgeryAsync($"/Process/Edit/{definition.Id}", "/Process/SaveTransition", new Dictionary<string, string>
        {
            ["ProcessStepId"] = step.Id.ToString(),
            ["OutcomeKey"] = "done",
            ["OutcomeLabel"] = "Done",
            ["Effect"] = ProcessTransitionEffect.None.ToString(),
            ["IsDefaultOutcome"] = "true",
            ["IsTerminal"] = "true"
        });

        transitionResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        ProcessTransition transition = await QueryInAdminContextAsync(db =>
            db.ProcessTransitions.FirstAsync(item => item.ProcessStepId == step.Id && item.OutcomeKey == "done"));

        using HttpResponseMessage deleteTransitionResponse = await PostFormWithAntiforgeryAsync($"/Process/Edit/{definition.Id}", "/Process/DeleteTransition", new Dictionary<string, string>
        {
            ["id"] = transition.Id.ToString()
        });

        deleteTransitionResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage deleteStepResponse = await PostFormWithAntiforgeryAsync($"/Process/Edit/{definition.Id}", "/Process/DeleteStep", new Dictionary<string, string>
        {
            ["id"] = step.Id.ToString()
        });

        deleteStepResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage deleteDefinitionResponse = await PostFormWithAntiforgeryAsync("/Process", "/Process/DeleteDefinition", new Dictionary<string, string>
        {
            ["id"] = definition.Id.ToString()
        });

        deleteDefinitionResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        bool definitionExists = await QueryInAdminContextAsync(db => db.ProcessDefinitions.AnyAsync(item => item.Id == definition.Id));
        bool stepExists = await QueryInAdminContextAsync(db => db.ProcessSteps.AnyAsync(item => item.Id == step.Id));
        bool transitionExists = await QueryInAdminContextAsync(db => db.ProcessTransitions.AnyAsync(item => item.Id == transition.Id));

        definitionExists.Should().BeFalse();
        stepExists.Should().BeFalse();
        transitionExists.Should().BeFalse();
    }
}
