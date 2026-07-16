using System.Net;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using ClientRelationshipManagement.Web.Services.Agents;
using ClientRelationshipManagement.Web.Services.Processes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ProcessControllerTests
{
    [CRMAcceptanceFact]
    public async Task ActivateDraft_MovesActiveWorkOntoTheLiveGraph_CancelsObsoleteTasks_AndRecreatesCoverage()
    {
        Guid sourceId = Guid.NewGuid();
        Guid sourceStepId = Guid.NewGuid();
        Guid instanceId = Guid.NewGuid();
        Guid taskId = Guid.NewGuid();
        string tenantId = Unique("migration-tenant");
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await ExecuteInAdminContextAsync(async db =>
        {
            db.ProcessDefinitions.Add(new ProcessDefinition
            {
                Id = sourceId,
                FamilyId = sourceId,
                TenantId = tenantId,
                ScopeType = ProcessScopeType.Lead,
                VersionNumber = 1,
                LifecycleState = ProcessDefinitionLifecycleState.Active,
                Name = "Migration source",
                IsActive = true,
                IsDefault = true,
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });
            db.ProcessSteps.Add(new ProcessStep
            {
                Id = sourceStepId,
                ProcessDefinitionId = sourceId,
                Key = "stable-step",
                Name = "Stable step",
                Sequence = 10,
                IsEntryPoint = true,
                IsActive = true,
                ActionType = ProcessActionType.Review,
                TaskTitleTemplate = "Old task",
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });
            db.ProcessInstances.Add(new ProcessInstance
            {
                Id = instanceId,
                ProcessDefinitionId = sourceId,
                CurrentProcessStepId = sourceStepId,
                State = ProcessInstanceState.Active,
                StartedOn = now,
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });
            db.ProcessTasks.Add(new ProcessTask
            {
                Id = taskId,
                ProcessInstanceId = instanceId,
                ProcessStepId = sourceStepId,
                ActionType = ProcessActionType.Review,
                State = ProcessTaskState.Pending,
                DueOn = now,
                RenderedTitle = "Old task",
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });
            await db.SaveChangesAsync();
        });

        ProcessDefinition activated;
        using (IServiceScope scope = Fixture.Factory.Services.CreateScope())
        {
            IProcessDraftService service = scope.ServiceProvider.GetRequiredService<IProcessDraftService>();
            ProcessDefinition draft = await service.CreateDraftAsync(
                sourceId,
                Fixture.Settings.UserId,
                "Acceptance Agent",
                "Update the active graph",
                null,
                null,
                [],
                CancellationToken.None);
            activated = await service.ActivateDraftAsync(
                draft.Id,
                Fixture.Settings.UserId,
                "Approved by acceptance test",
                CancellationToken.None);
            IWorkflowAutomationService workflow = scope.ServiceProvider.GetRequiredService<IWorkflowAutomationService>();
            await workflow.EnsureDefinitionCoverageAsync(activated.Id, CancellationToken.None);
        }

        (ProcessInstance Instance, ProcessTask Task, ProcessTask NewTask, ProcessDefinition Source, ProcessStep LiveStep) result =
            await QueryInAdminContextAsync(async db =>
            {
                ProcessInstance instance = await db.ProcessInstances.AsNoTracking().SingleAsync(item => item.Id == instanceId);
                ProcessTask task = await db.ProcessTasks.AsNoTracking().SingleAsync(item => item.Id == taskId);
                ProcessDefinition source = await db.ProcessDefinitions.AsNoTracking().SingleAsync(item => item.Id == sourceId);
                ProcessStep liveStep = await db.ProcessSteps.AsNoTracking().SingleAsync(item =>
                    item.ProcessDefinitionId == activated.Id && item.Key == "stable-step");
                ProcessTask newTask = await db.ProcessTasks.AsNoTracking().SingleAsync(item =>
                    item.ProcessInstanceId == instanceId && item.Id != taskId && item.State == ProcessTaskState.Pending);
                return (instance, task, newTask, source, liveStep);
            });

        result.Instance.ProcessDefinitionId.Should().Be(activated.Id);
        result.Instance.CurrentProcessStepId.Should().Be(result.LiveStep.Id);
        result.Instance.CurrentProcessTaskId.Should().Be(result.NewTask.Id);
        result.NewTask.ProcessStepId.Should().Be(result.LiveStep.Id);
        result.NewTask.RenderedTitle.Should().Be("Old task");
        result.Task.State.Should().Be(ProcessTaskState.Cancelled);
        result.Task.CompletionOutcomeKey.Should().Be("process-version-migrated");
        result.Source.LifecycleState.Should().Be(ProcessDefinitionLifecycleState.Archived);
    }

    [CRMAcceptanceFact]
    public async Task Post_SaveStep_WhenDueOffsetChanges_ReschedulesExistingPendingActionsFromCreationTime()
    {
        Guid definitionId = Guid.NewGuid();
        Guid stepId = Guid.NewGuid();
        Guid instanceId = Guid.NewGuid();
        Guid taskId = Guid.NewGuid();
        DateTimeOffset createdOn = DateTimeOffset.UtcNow.AddHours(-4);

        await ExecuteInAdminContextAsync(async db =>
        {
            db.ProcessDefinitions.Add(new ProcessDefinition
            {
                Id = definitionId,
                TenantId = AcceptanceSettings.TenantId,
                ScopeType = ProcessScopeType.Opportunity,
                VersionNumber = 1,
                LifecycleState = ProcessDefinitionLifecycleState.Draft,
                Name = Unique("Rescheduling Process"),
                IsActive = false,
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = createdOn,
                LastUpdated = createdOn
            });
            db.ProcessSteps.Add(new ProcessStep
            {
                Id = stepId,
                ProcessDefinitionId = definitionId,
                Key = "reschedule-step",
                Name = "Reschedule Step",
                Sequence = 10,
                IsEntryPoint = true,
                IsActive = true,
                ActionType = ProcessActionType.Review,
                DueAfterDays = 1,
                TaskTitleTemplate = "Test rescheduling",
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = createdOn,
                LastUpdated = createdOn
            });
            db.ProcessInstances.Add(new ProcessInstance
            {
                Id = instanceId,
                ProcessDefinitionId = definitionId,
                CurrentProcessStepId = stepId,
                State = ProcessInstanceState.Active,
                StartedOn = createdOn,
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = createdOn,
                LastUpdated = createdOn
            });
            db.ProcessTasks.Add(new ProcessTask
            {
                Id = taskId,
                ProcessInstanceId = instanceId,
                ProcessStepId = stepId,
                ActionType = ProcessActionType.Review,
                State = ProcessTaskState.Pending,
                DueOn = createdOn.AddDays(1),
                RenderedTitle = "Test rescheduling",
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = createdOn,
                LastUpdated = createdOn
            });
            await db.SaveChangesAsync();
        });

        ProcessStep step = await QueryInAdminContextAsync(db => db.ProcessSteps
            .AsNoTracking()
            .SingleAsync(item => item.Id == stepId));

        Dictionary<string, string> values = new()
        {
            ["Id"] = step.Id.ToString(),
            ["ProcessDefinitionId"] = step.ProcessDefinitionId.ToString(),
            ["Key"] = step.Key,
            ["Name"] = step.Name,
            ["Objective"] = step.Objective ?? string.Empty,
            ["RequiredFacts"] = step.RequiredFacts ?? string.Empty,
            ["ProducedFacts"] = step.ProducedFacts ?? string.Empty,
            ["ViabilityImpact"] = step.ViabilityImpact ?? string.Empty,
            ["Sequence"] = step.Sequence.ToString(),
            ["IsEntryPoint"] = step.IsEntryPoint.ToString(),
            ["IsActive"] = step.IsActive.ToString(),
            ["ActionType"] = step.ActionType.ToString(),
            ["DueAfterDays"] = "2",
            ["DueAfterHours"] = "3",
            ["TaskTitleTemplate"] = step.TaskTitleTemplate,
            ["TaskInstructionsTemplate"] = step.TaskInstructionsTemplate ?? string.Empty,
            ["EmailRecipientTarget"] = step.EmailRecipientTarget.ToString(),
            ["EmailSubjectTemplate"] = step.EmailSubjectTemplate ?? string.Empty,
            ["EmailBodyTemplate"] = step.EmailBodyTemplate ?? string.Empty,
            ["CallScriptTemplate"] = step.CallScriptTemplate ?? string.Empty,
            ["QuestionSetTemplate"] = step.QuestionSetTemplate ?? string.Empty
        };

        if (step.RelationshipStatusOnActivate.HasValue)
            values["RelationshipStatusOnActivate"] = step.RelationshipStatusOnActivate.Value.ToString();
        if (step.SalesStageOnActivate.HasValue)
            values["SalesStageOnActivate"] = step.SalesStageOnActivate.Value.ToString();
        if (step.ClientAccountStatusOnActivate.HasValue)
            values["ClientAccountStatusOnActivate"] = step.ClientAccountStatusOnActivate.Value.ToString();

        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync(
            $"/Admin/Process/Edit/{step.ProcessDefinitionId}",
            "/Admin/Process/SaveStep",
            values);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        ProcessTask rescheduledTask = await QueryInAdminContextAsync(db => db.ProcessTasks
            .AsNoTracking()
            .SingleAsync(item => item.Id == taskId));
        rescheduledTask.DueOn.Should().Be(createdOn.AddDays(2).AddHours(3));
    }

    [CRMAcceptanceFact]
    public async Task Post_SaveDefinition_SaveStep_SaveTransition_DeleteTransition_DeleteStep_And_DeleteDefinition_Work()
    {
        using HttpResponseMessage definitionResponse = await PostFormWithAntiforgeryAsync("/Admin/Process", "/Admin/Process/SaveDefinition", new Dictionary<string, string>
        {
            ["TenantId"] = AcceptanceSettings.TenantId,
            ["ScopeType"] = ProcessScopeType.Opportunity.ToString(),
            ["Name"] = "Acceptance Process",
            ["Description"] = "Created by acceptance test",
            ["IsDefault"] = "false",
            ["IsActive"] = "false"
        });

        definitionResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        ProcessDefinition definition = await QueryInAdminContextAsync(db =>
            db.ProcessDefinitions.OrderByDescending(item => item.CreatedOn).FirstAsync(item => item.Name == "Acceptance Process"));

        using HttpResponseMessage stepResponse = await PostFormWithAntiforgeryAsync($"/Admin/Process/Edit/{definition.Id}", "/Admin/Process/SaveStep", new Dictionary<string, string>
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

        using HttpResponseMessage transitionResponse = await PostFormWithAntiforgeryAsync($"/Admin/Process/Edit/{definition.Id}", "/Admin/Process/SaveTransition", new Dictionary<string, string>
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

        using HttpResponseMessage deleteTransitionResponse = await PostFormWithAntiforgeryAsync($"/Admin/Process/Edit/{definition.Id}", "/Admin/Process/DeleteTransition", new Dictionary<string, string>
        {
            ["id"] = transition.Id.ToString()
        });

        deleteTransitionResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage deleteStepResponse = await PostFormWithAntiforgeryAsync($"/Admin/Process/Edit/{definition.Id}", "/Admin/Process/DeleteStep", new Dictionary<string, string>
        {
            ["id"] = step.Id.ToString()
        });

        deleteStepResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage deleteDefinitionResponse = await PostFormWithAntiforgeryAsync("/Admin/Process", "/Admin/Process/DeleteDefinition", new Dictionary<string, string>
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
