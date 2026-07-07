using System.Net;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class HomeControllerTests
{
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
    public async Task Post_CompleteTodo_ForConfirmRoute_CreatesPrimaryContactAndDraftRecipient()
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

        ProcessTask firstTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks.FirstAsync(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending));

        using HttpResponseMessage response = await Client.PostAsync("/Home/CompleteTodo", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = firstTask.Id.ToString(),
            ["SourceType"] = "process",
            ["CompletionNote"] = "Use John Doe at john.doe@ccoder.co.uk as the initial contact. Opening angle: supplier payment visibility for software services.",
            ["OutcomeKey"] = "ready"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        Opportunity updatedOpportunity = await QueryInAdminContextAsync(db =>
            db.Opportunities.FirstAsync(item => item.Id == opportunityId));
        TenantCompanyRelationship updatedRelationship = await QueryInAdminContextAsync(db =>
            db.TenantCompanyRelationships.FirstAsync(item => item.Id == relationshipId));
        RelationshipContact createdRelationshipContact = await QueryInAdminContextAsync(db =>
            db.RelationshipContacts
                .Include(item => item.CompanyContact)
                .FirstAsync(item => item.TenantCompanyRelationshipId == relationshipId));
        ProcessTask emailTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks
                .Include(item => item.Email)
                .FirstAsync(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending));

        updatedOpportunity.PrimaryRelationshipContactId.Should().Be(createdRelationshipContact.Id);
        updatedRelationship.PreferredOpeningAngle.Should().Be("supplier payment visibility for software services.");
        createdRelationshipContact.CompanyContact.Name.Should().Be("John Doe");
        createdRelationshipContact.CompanyContact.EmailAddress.Should().Be("john.doe@ccoder.co.uk");
        createdRelationshipContact.RelationshipRoute.Should().Be("supplier payment visibility for software services.");
        emailTask.Email.Should().NotBeNull();
        emailTask.Email!.ToAddresses.Should().Be("john.doe@ccoder.co.uk");
        emailTask.Email.BodyText.Should().Contain("John Doe");
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
            ["CompletionNote"] = "Positive reply received.",
            ["OutcomeKey"] = "positive-reply"
        }));

        ProcessTask proposalTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks.Include(item => item.Email)
                .FirstAsync(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending && item.RenderedTitle.Contains("proposal")));

        await Client.PostAsync("/Home/ApproveDraftEmail", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["EmailId"] = proposalTask.EmailId!.Value.ToString()
        }));

        await Client.PostAsync("/Home/ConfirmDraftEmailSent", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["Id"] = proposalTask.Id.ToString(),
            ["SourceType"] = "process",
            ["EmailId"] = proposalTask.EmailId!.Value.ToString()
        }));

        ProcessTask negotiateTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks.FirstAsync(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending && item.RenderedTitle.Contains("Negotiate")));

        await Client.PostAsync("/Home/CompleteTodo", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = negotiateTask.Id.ToString(),
            ["SourceType"] = "process",
            ["CompletionNote"] = "Commercials agreed.",
            ["OutcomeKey"] = "contract-ready"
        }));

        ProcessTask contractTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks.Include(item => item.Email)
                .FirstAsync(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending && item.RenderedTitle.Contains("contract")));

        await Client.PostAsync("/Home/ApproveDraftEmail", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["EmailId"] = contractTask.EmailId!.Value.ToString()
        }));

        await Client.PostAsync("/Home/ConfirmDraftEmailSent", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["Id"] = contractTask.Id.ToString(),
            ["SourceType"] = "process",
            ["EmailId"] = contractTask.EmailId!.Value.ToString()
        }));

        ProcessTask signatureTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks.FirstAsync(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending && item.RenderedTitle.Contains("signed")));

        using HttpResponseMessage finalResponse = await Client.PostAsync("/Home/CompleteTodo", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = signatureTask.Id.ToString(),
            ["SourceType"] = "process",
            ["CompletionNote"] = "Contract signed.",
            ["OutcomeKey"] = "won"
        }));

        finalResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        ClientAccount clientAccount = await QueryInAdminContextAsync(db =>
            db.ClientAccounts.FirstAsync(item => item.WonOpportunityId == opportunityId));
        ProcessInstance completedInstance = await QueryInAdminContextAsync(db =>
            db.ProcessInstances.FirstAsync(item => item.OpportunityId == opportunityId));

        clientAccount.Status.Should().Be(ClientAccountStatus.Onboarding);
        completedInstance.State.Should().Be(ProcessInstanceState.Completed);
        completedInstance.CompletionOutcomeKey.Should().Be("won");
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
            ["OutcomeKey"] = "lost"
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
        completedInstance.CompletionOutcomeKey.Should().Be("lost");
        maybeClientAccount.Should().BeNull();
    }
}
