using System.Net;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using ClientRelationshipManagement.Web.Services.Processes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class HomeControllerTests
{
    [CRMAcceptanceFact]
    public async Task Workflow_LowFitLeadIsDeferredWithoutSuppression_AndCanBeReevaluated()
    {
        (Guid leadId, _) = await SeedLeadAsync();
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(leadId: leadId, forceCreate: true).AsTask());

        string[] outcomes = ["identity-checked", "activity-described", "contact-researched", "scale-assessed", "quality-assessed", "fit-assessed", "deferred"];
        foreach (string outcome in outcomes)
        {
            Guid taskId = await QueryInAdminContextAsync(db => db.ProcessTasks
                .Where(item => item.LeadId == leadId && item.State == ProcessTaskState.Pending)
                .Select(item => item.Id)
                .SingleAsync());
            await ExecuteWorkflowAsync(service => service.CompleteTaskAsync(new ProcessTaskCompletionCommand
            {
                ProcessTaskId = taskId,
                OutcomeKey = outcome,
                CompletionNote = outcome == "fit-assessed" ? "Fit score: 40." : $"Completed {outcome}."
            }).AsTask());
        }

        Lead deferred = await QueryInAdminContextAsync(db => db.Leads.Include(item => item.Company).SingleAsync(item => item.Id == leadId));
        deferred.Status.Should().Be(LeadStatus.Deferred);
        deferred.Company.IsProspectingSuppressed.Should().BeFalse();

        int requeued = 0;
        await ExecuteWorkflowAsync(async service =>
        {
            requeued = await service.ReevaluateDeferredLeadsAsync(AcceptanceSettings.TenantId);
        });

        requeued.Should().BeGreaterThan(0);
        Lead reactivated = await QueryInAdminContextAsync(db => db.Leads.SingleAsync(item => item.Id == leadId));
        reactivated.Status.Should().Be(LeadStatus.Imported);
        bool hasPendingTask = await QueryInAdminContextAsync(db => db.ProcessTasks.AnyAsync(item => item.LeadId == leadId && item.State == ProcessTaskState.Pending));
        hasPendingTask.Should().BeTrue();
    }

    [CRMAcceptanceFact]
    public async Task Post_SetAutoApproveProcessEmails_PersistsUserSetting()
    {
        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync(
            "/",
            "/Home/SetAutoApproveProcessEmails",
            new Dictionary<string, string> { ["enabled"] = "true" });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        AgentAutomationSetting setting = await QueryInAdminContextAsync(db =>
            db.AgentAutomationSettings.FirstAsync(item => item.UserId == Fixture.Settings.UserId));
        setting.AutoApproveProcessEmails.Should().BeTrue();
    }

    [CRMAcceptanceFact]
    public async Task Post_SaveDraftEmail_ApproveDraftEmail_And_ConfirmDraftEmailSent_AdvanceWorkflow()
    {
        (_, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        ProcessTask emailTask = await MoveOpportunityToEmailStepAsync(opportunityId);

        Email draftEmail = await QueryInAdminContextAsync(db =>
            db.Emails.FirstAsync(item => item.Id == emailTask.EmailId!.Value));

        using HttpResponseMessage saveDraftResponse = await Client.PostAsync("/Home/SaveDraftEmail", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClientId"] = draftEmail.TenantCompanyRelationshipId.ToString(),
            ["Id"] = emailTask.Id.ToString(),
            ["SourceType"] = "process",
            ["EmailId"] = draftEmail.Id.ToString(),
            ["ClientMaterialId"] = draftEmail.MaterialId!.Value.ToString(),
            ["ClientOpportunityId"] = opportunityId.ToString(),
            ["Direction"] = ActivityDirection.Outbound.ToString(),
            ["ToAddresses"] = "updated@example.com",
            ["Subject"] = "Updated outreach subject",
            ["Body"] = "Updated outreach body"
        }));

        saveDraftResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage approveResponse = await Client.PostAsync("/Home/ApproveDraftEmail", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClientId"] = draftEmail.TenantCompanyRelationshipId.ToString(),
            ["EmailId"] = draftEmail.Id.ToString()
        }));

        approveResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage confirmSentResponse = await Client.PostAsync("/Home/ConfirmDraftEmailSent", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClientId"] = draftEmail.TenantCompanyRelationshipId.ToString(),
            ["Id"] = emailTask.Id.ToString(),
            ["SourceType"] = "process",
            ["EmailId"] = draftEmail.Id.ToString()
        }));

        confirmSentResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        Email sentEmail = await QueryInAdminContextAsync(db => db.Emails.FirstAsync(item => item.Id == draftEmail.Id));
        ProcessTask completedTask = await QueryInAdminContextAsync(db => db.ProcessTasks.FirstAsync(item => item.Id == emailTask.Id));

        sentEmail.Subject.Should().Be("Updated outreach subject");
        sentEmail.State.Should().Be(EmailState.Sent);
        completedTask.State.Should().Be(ProcessTaskState.Completed);
    }

    [CRMAcceptanceFact]
    public async Task Post_CompleteTodo_AdvancesManualTask()
    {
        (_, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true).AsTask());

        ProcessTask firstTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks.FirstAsync(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending));

        using HttpResponseMessage response = await Client.PostAsync("/Home/CompleteTodo", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = firstTask.Id.ToString(),
            ["SourceType"] = "process",
            ["CompletionNote"] = "Completed initial review",
            ["OutcomeKey"] = "ready"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        ProcessTask updatedTask = await QueryInAdminContextAsync(db => db.ProcessTasks.FirstAsync(item => item.Id == firstTask.Id));
        updatedTask.State.Should().Be(ProcessTaskState.Completed);
    }

    [CRMAcceptanceFact]
    public async Task EnsureCoverage_OpportunityWithoutContact_DoesNotStartOpportunityProcess()
    {
        Guid companyId = Guid.NewGuid();
        Guid relationshipId = Guid.NewGuid();
        Guid opportunityId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await ExecuteInAdminContextAsync(async db =>
        {
            db.Companies.Add(new Company
            {
                Id = companyId,
                SourceSystem = "Acceptance",
                IsVerified = true,
                OfficialName = "Route Test Co",
                CompanyNumber = "ROUTE-001",
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });

            db.TenantCompanyRelationships.Add(new TenantCompanyRelationship
            {
                Id = relationshipId,
                TenantId = AcceptanceSettings.TenantId,
                CompanyId = companyId,
                AccountOwnerUserId = Fixture.Settings.UserId,
                AccountOwnerDisplayName = "CRM Acceptance User",
                Status = RelationshipStatus.Prospect,
                CurrentStage = SalesPipelineStage.Researched,
                Priority = RelationshipPriority.Medium,
                LeadSource = "Acceptance",
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });

            db.Opportunities.Add(new Opportunity
            {
                Id = opportunityId,
                TenantCompanyRelationshipId = relationshipId,
                Type = OpportunityType.General,
                Stage = SalesPipelineStage.Researched,
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });

            await db.SaveChangesAsync();
        });

        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true).AsTask());

        Opportunity updatedOpportunity = await QueryInAdminContextAsync(db =>
            db.Opportunities.FirstAsync(item => item.Id == opportunityId));
        int processTaskCount = await QueryInAdminContextAsync(db =>
            db.ProcessTasks.CountAsync(item => item.OpportunityId == opportunityId));

        updatedOpportunity.Stage.Should().Be(SalesPipelineStage.Nurture);
        processTaskCount.Should().Be(0);
    }

    [CRMAcceptanceFact]
    public async Task Post_CompleteTodo_WonPath_CreatesClientAccount()
    {
        (Guid relationshipId, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true).AsTask());

        ProcessTask routeTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks.FirstAsync(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending));

        await Client.PostAsync("/Home/CompleteTodo", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = routeTask.Id.ToString(),
            ["SourceType"] = "process",
            ["CompletionNote"] = "Route confirmed.",
            ["OutcomeKey"] = "ready"
        }));

        ProcessTask introEmailTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks.Include(item => item.Email)
                .FirstAsync(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending));

        await Client.PostAsync("/Home/ApproveDraftEmail", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["EmailId"] = introEmailTask.EmailId!.Value.ToString()
        }));

        await Client.PostAsync("/Home/ConfirmDraftEmailSent", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["Id"] = introEmailTask.Id.ToString(),
            ["SourceType"] = "process",
            ["EmailId"] = introEmailTask.EmailId!.Value.ToString()
        }));

        ProcessTask reviewTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks.FirstAsync(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending));

        await Client.PostAsync("/Home/CompleteTodo", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = reviewTask.Id.ToString(),
            ["SourceType"] = "process",
            ["CompletionNote"] = "Positive reply received and the contact requested a demo.",
            ["OutcomeKey"] = "demo-interest"
        }));

        ProcessTask summaryTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks.Include(item => item.ProcessStep)
                .FirstAsync(item => item.OpportunityId == opportunityId
                    && item.State == ProcessTaskState.Pending
                    && item.ProcessStep.Key == "opportunity-summary"));

        await Client.PostAsync("/Home/CompleteTodo", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = summaryTask.Id.ToString(),
            ["SourceType"] = "process",
            ["CompletionNote"] = "Opportunity summary: The contact requested a demo.\nPain or need: Needs a structured outreach path.\nValue hypothesis: A focused programme could accelerate qualified conversations.\nDemo interest evidence: Positive reply requested a demo.\nEstimated annual value: unknown.\nConfidence: high.",
            ["OutcomeKey"] = "summary-ready"
        }));

        ProcessTask handoffTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks.Include(item => item.Email)
                .FirstAsync(item => item.OpportunityId == opportunityId
                    && item.State == ProcessTaskState.Pending
                    && item.ProcessStep.Key == "handoff-account-owner"));

        handoffTask.Email.Should().NotBeNull();
        handoffTask.Email!.ToAddresses.Should().Be("crm.acceptance@example.com");
        handoffTask.Email.Subject.Should().Contain("Demo-ready opportunity");
        handoffTask.Email.BodyText.Should().Contain("Needs a structured outreach path");
        handoffTask.Email.BodyText.Should().Contain("interested in at least a demo");

        await Client.PostAsync("/Home/ApproveDraftEmail", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["EmailId"] = handoffTask.EmailId!.Value.ToString()
        }));

        await Client.PostAsync("/Home/ConfirmDraftEmailSent", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["Id"] = handoffTask.Id.ToString(),
            ["SourceType"] = "process",
            ["EmailId"] = handoffTask.EmailId!.Value.ToString()
        }));

        ProcessTask accountOwnerDecisionTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks.Include(item => item.ProcessStep)
                .FirstAsync(item => item.OpportunityId == opportunityId
                    && item.State == ProcessTaskState.Pending
                    && item.ProcessStep.Key == "account-owner-decision"));

        accountOwnerDecisionTask.ActionType.Should().Be(ProcessActionType.Approval);

        using HttpResponseMessage finalResponse = await Client.PostAsync("/Home/CompleteTodo", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = accountOwnerDecisionTask.Id.ToString(),
            ["SourceType"] = "process",
            ["CompletionNote"] = "Demo completed and contract negotiations agreed by the account owner.",
            ["OutcomeKey"] = "move-forward"
        }));

        finalResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        ClientAccount clientAccount = await QueryInAdminContextAsync(db =>
            db.ClientAccounts.FirstAsync(item => item.WonOpportunityId == opportunityId));
        ProcessInstance completedInstance = await QueryInAdminContextAsync(db =>
            db.ProcessInstances.FirstAsync(item => item.OpportunityId == opportunityId));

        clientAccount.Status.Should().Be(ClientAccountStatus.Onboarding);
        completedInstance.State.Should().Be(ProcessInstanceState.Completed);
        completedInstance.CompletionOutcomeKey.Should().Be("move-forward");
    }

    [CRMAcceptanceFact]
    public async Task Post_CompleteTodo_LostPath_ClosesOpportunityWithoutCreatingClient()
    {
        (Guid relationshipId, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true).AsTask());

        ProcessTask routeTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks.FirstAsync(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending));

        await Client.PostAsync("/Home/CompleteTodo", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = routeTask.Id.ToString(),
            ["SourceType"] = "process",
            ["CompletionNote"] = "Route confirmed.",
            ["OutcomeKey"] = "ready"
        }));

        ProcessTask introEmailTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks.Include(item => item.Email)
                .FirstAsync(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending));

        await Client.PostAsync("/Home/ApproveDraftEmail", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["EmailId"] = introEmailTask.EmailId!.Value.ToString()
        }));

        await Client.PostAsync("/Home/ConfirmDraftEmailSent", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["Id"] = introEmailTask.Id.ToString(),
            ["SourceType"] = "process",
            ["EmailId"] = introEmailTask.EmailId!.Value.ToString()
        }));

        ProcessTask reviewTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks.FirstAsync(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending));

        using HttpResponseMessage finalResponse = await Client.PostAsync("/Home/CompleteTodo", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = reviewTask.Id.ToString(),
            ["SourceType"] = "process",
            ["CompletionNote"] = "No fit confirmed after outreach review.",
            ["OutcomeKey"] = "not-interested"
        }));

        finalResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        Opportunity updatedOpportunity = await QueryInAdminContextAsync(db =>
            db.Opportunities.FirstAsync(item => item.Id == opportunityId));
        TenantCompanyRelationship updatedRelationship = await QueryInAdminContextAsync(db =>
            db.TenantCompanyRelationships.FirstAsync(item => item.Id == relationshipId));
        ProcessInstance completedInstance = await QueryInAdminContextAsync(db =>
            db.ProcessInstances.FirstAsync(item => item.OpportunityId == opportunityId));
        ClientAccount maybeClientAccount = await QueryInAdminContextAsync(db =>
            db.ClientAccounts.FirstOrDefaultAsync(item => item.WonOpportunityId == opportunityId));

        updatedOpportunity.Stage.Should().Be(SalesPipelineStage.Lost);
        updatedRelationship.Status.Should().Be(RelationshipStatus.Disqualified);
        completedInstance.State.Should().Be(ProcessInstanceState.Completed);
        completedInstance.CompletionOutcomeKey.Should().Be("not-interested");
        maybeClientAccount.Should().BeNull();
    }
}
