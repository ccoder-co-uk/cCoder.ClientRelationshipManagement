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
    private async Task CompletePendingLeadOutcomesAsync(Guid leadId, params string[] outcomes)
    {
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
                CompletionNote = LeadCompletionNote(outcome)
            }).AsTask());
        }
    }

    private static string LeadCompletionNote(string outcome) => outcome switch
    {
        "status-current" => "Current status: active. Official source URL: https://find-and-update.company-information.service.gov.uk/company/01234567. Evidence: exact entity is active.",
        "resources-gathered" => "Resource pack gathered. Pages inspected: https://example.com/.",
        "fit-assessed" => "Activity: test company.\nPitchable: no\nPitch reason: no test need.\nOpening angle: none.\nPages used: https://example.com/.",
        "contacts-extracted" => "Reachability: none\nPages inspected: none",
        "relationships-extracted" => "Related companies: none found in first-party evidence.",
        "related-companies-tipped" => "Related company candidates reconciled with the pool.",
        _ => $"Completed {outcome}."
    };

    [CRMAcceptanceFact]
    public async Task Workflow_CompletionRefreshesAProcessAdvancedByAnotherAgentContext()
    {
        (Guid leadId, _) = await SeedLeadAsync();

        await ExecuteWorkflowAsync(async staleService =>
        {
            await staleService.EnsureCoverageAsync(leadId: leadId, forceCreate: true);
            Guid identityTaskId = await QueryInAdminContextAsync(db => db.ProcessTasks
                .Where(item => item.LeadId == leadId && item.State == ProcessTaskState.Pending)
                .Select(item => item.Id)
                .SingleAsync());
            await staleService.CompleteTaskAsync(new ProcessTaskCompletionCommand
            {
                ProcessTaskId = identityTaskId,
                OutcomeKey = "identity-checked",
                CompletionNote = "Identity matched."
            });

            Guid statusTaskId = await QueryInAdminContextAsync(db => db.ProcessTasks
                .Where(item => item.LeadId == leadId && item.State == ProcessTaskState.Pending)
                .Select(item => item.Id)
                .SingleAsync());
            await ExecuteWorkflowAsync(service => service.CompleteTaskAsync(new ProcessTaskCompletionCommand
            {
                ProcessTaskId = statusTaskId,
                OutcomeKey = "status-current",
                CompletionNote = "Current status: active. Official source URL: https://find-and-update.company-information.service.gov.uk/company/01234567. Evidence: exact entity is active."
            }).AsTask());

            await CompletePendingLeadOutcomesAsync(leadId, "resources-gathered", "fit-assessed");

            Guid researchTaskId = await QueryInAdminContextAsync(db => db.ProcessTasks
                .Where(item => item.LeadId == leadId
                    && item.State == ProcessTaskState.Pending
                    && item.ProcessStep.Key == "contact-research")
                .Select(item => item.Id)
                .SingleAsync());
            await staleService.CompleteTaskAsync(new ProcessTaskCompletionCommand
            {
                ProcessTaskId = researchTaskId,
                OutcomeKey = "contacts-extracted",
                CompletionNote = "Reachability: direct\nPages inspected: https://example.com/contact"
            });
        });

        string pendingStep = await QueryInAdminContextAsync(db => db.ProcessTasks
            .Where(item => item.LeadId == leadId && item.State == ProcessTaskState.Pending)
            .Select(item => item.ProcessStep.Key)
            .SingleAsync());
        pendingStep.Should().Be("extract-related-companies");
    }

    [CRMAcceptanceFact]
    public async Task Workflow_SeedRepairAlignsMigratedStatusTaskWithActiveProcessStep()
    {
        (Guid leadId, _) = await SeedLeadAsync();
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(leadId: leadId, forceCreate: true).AsTask());

        Guid identityTaskId = await QueryInAdminContextAsync(db => db.ProcessTasks
            .Where(item => item.LeadId == leadId && item.State == ProcessTaskState.Pending)
            .Select(item => item.Id)
            .SingleAsync());
        await ExecuteWorkflowAsync(service => service.CompleteTaskAsync(new ProcessTaskCompletionCommand
        {
            ProcessTaskId = identityTaskId,
            OutcomeKey = "identity-checked",
            CompletionNote = "Identity matched."
        }).AsTask());

        Guid statusTaskId = await QueryInAdminContextAsync(db => db.ProcessTasks
            .Where(item => item.LeadId == leadId
                && item.State == ProcessTaskState.Pending
                && item.ProcessStep.Key == "current-status-research")
            .Select(item => item.Id)
            .SingleAsync());

        await ExecuteInAdminContextAsync(async db =>
        {
            ProcessTask statusTask = await db.ProcessTasks
                .Include(item => item.ProcessInstance)
                .Include(item => item.ProcessStep)
                .SingleAsync(item => item.Id == statusTaskId);
            Guid contactStepId = await db.ProcessSteps
                .Where(item => item.ProcessDefinitionId == statusTask.ProcessStep.ProcessDefinitionId
                    && item.Key == "contact-research")
                .Select(item => item.Id)
                .SingleAsync();
            statusTask.ProcessInstance.CurrentProcessStepId = contactStepId;
            await db.SaveChangesAsync();
        });

        await ExecuteWorkflowAsync(service => service.EnsureSeedProcessesAsync().AsTask());

        (Guid CurrentStepId, Guid TaskStepId) repaired = await QueryInAdminContextAsync(async db =>
        {
            ProcessTask task = await db.ProcessTasks
                .AsNoTracking()
                .Include(item => item.ProcessInstance)
                .SingleAsync(item => item.Id == statusTaskId);
            return (task.ProcessInstance.CurrentProcessStepId!.Value, task.ProcessStepId);
        });
        repaired.CurrentStepId.Should().Be(repaired.TaskStepId);

        (string IdentityTarget, string StatusTarget, string ResourcesTarget, string FitTarget,
            string ContactTarget, string RelatedTarget, string TipTarget, int ActiveStepCount,
            bool RetiredStepsInactive) routes =
            await QueryInAdminContextAsync(async db =>
            {
                Guid definitionId = await db.ProcessTasks
                    .Where(item => item.Id == statusTaskId)
                    .Select(item => item.ProcessStep.ProcessDefinitionId)
                    .SingleAsync();
                var routeRows = await db.ProcessTransitions
                    .Where(item => item.ProcessStep.ProcessDefinitionId == definitionId
                        && (item.ProcessStep.Key == "lead-research"
                            || item.ProcessStep.Key == "current-status-research"
                            || item.ProcessStep.Key == "gather-company-resources"
                            || item.ProcessStep.Key == "assess-scf-fit"
                            || item.ProcessStep.Key == "contact-research"
                            || item.ProcessStep.Key == "extract-related-companies"
                            || item.ProcessStep.Key == "tip-related-companies"))
                    .Select(item => new
                    {
                        From = item.ProcessStep.Key,
                        To = item.NextProcessStep == null ? null : item.NextProcessStep.Key,
                        item.OutcomeKey,
                        item.IsTerminal,
                        item.Effect
                    })
                    .ToListAsync();
                return (
                    routeRows.Single(item => item.From == "lead-research").To!,
                    routeRows.Single(item => item.From == "current-status-research" && item.OutcomeKey == "status-current").To!,
                    routeRows.Single(item => item.From == "gather-company-resources").To!,
                    routeRows.Single(item => item.From == "assess-scf-fit").To!,
                    routeRows.Single(item => item.From == "contact-research").To!,
                    routeRows.Single(item => item.From == "extract-related-companies").To!,
                    routeRows.Single(item => item.From == "tip-related-companies").To!,
                    await db.ProcessSteps.CountAsync(item => item.ProcessDefinitionId == definitionId && item.IsActive),
                    await db.ProcessSteps
                        .Where(item => item.ProcessDefinitionId == definitionId
                            && (item.Key == "company-activity" || item.Key == "company-scale"
                                || item.Key == "verify-company" || item.Key == "commercial-fit"))
                        .AllAsync(item => !item.IsActive));
            });
        routes.IdentityTarget.Should().Be("current-status-research");
        routes.StatusTarget.Should().Be("gather-company-resources");
        routes.ResourcesTarget.Should().Be("assess-scf-fit");
        routes.FitTarget.Should().Be("contact-research");
        routes.ContactTarget.Should().Be("extract-related-companies");
        routes.RelatedTarget.Should().Be("tip-related-companies");
        routes.TipTarget.Should().Be("qualify-lead");
        routes.ActiveStepCount.Should().Be(8);
        routes.RetiredStepsInactive.Should().BeTrue();
    }

    [CRMAcceptanceFact]
    public async Task Workflow_ContactResearchRejectsReachabilityWithoutPersistedEmail()
    {
        (Guid leadId, Guid contactId) = await SeedLeadAsync();
        await ExecuteInAdminContextAsync(async db =>
        {
            LeadContact contact = await db.LeadContacts.SingleAsync(item => item.Id == contactId);
            db.LeadContacts.Remove(contact);
            Lead lead = await db.Leads.SingleAsync(item => item.Id == leadId);
            lead.RawContactPhoneNumber = "020 7946 0018";
            await db.SaveChangesAsync();
        });
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(leadId: leadId, forceCreate: true).AsTask());

        await CompletePendingLeadOutcomesAsync(
            leadId,
            "identity-checked",
            "status-current",
            "resources-gathered",
            "fit-assessed");

        ProcessTask contactTask = await QueryInAdminContextAsync(db => db.ProcessTasks
            .Include(item => item.ProcessStep)
            .SingleAsync(item => item.LeadId == leadId
                && item.State == ProcessTaskState.Pending
                && item.ProcessStep.Key == "contact-research"));
        contactTask.RenderedInstructions.Should().Contain("published email");
        contactTask.RenderedInstructions.Should().Contain("indirect route");

        Func<Task> completePhoneOnly = () => ExecuteWorkflowAsync(service => service.CompleteTaskAsync(
            new ProcessTaskCompletionCommand
            {
                ProcessTaskId = contactTask.Id,
                OutcomeKey = "contacts-extracted",
                CompletionNote = "Reachability: indirect\nPages inspected: https://example.com/contact"
            }).AsTask());

        await completePhoneOnly.Should().ThrowAsync<WorkflowRuleViolationException>()
            .WithMessage("*published email*persisted*");
        ProcessTask unchanged = await QueryInAdminContextAsync(db => db.ProcessTasks
            .AsNoTracking()
            .SingleAsync(item => item.Id == contactTask.Id));
        unchanged.State.Should().Be(ProcessTaskState.Pending);
    }

    [CRMAcceptanceFact]
    public async Task Workflow_PositiveReachabilityRequiresAnOpenedPageOnThePagesInspectedLine()
    {
        (Guid leadId, _) = await SeedLeadAsync();
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(leadId: leadId, forceCreate: true).AsTask());

        await CompletePendingLeadOutcomesAsync(
            leadId,
            "identity-checked",
            "status-current",
            "resources-gathered",
            "fit-assessed");

        Guid contactTaskId = await QueryInAdminContextAsync(db => db.ProcessTasks
            .Where(item => item.LeadId == leadId
                && item.State == ProcessTaskState.Pending
                && item.ProcessStep.Key == "contact-research")
            .Select(item => item.Id)
            .SingleAsync());
        Func<Task> completeWithoutOpenedPage = () => ExecuteWorkflowAsync(service => service.CompleteTaskAsync(
            new ProcessTaskCompletionCommand
            {
                ProcessTaskId = contactTaskId,
                OutcomeKey = "contacts-extracted",
                CompletionNote = "Reachability: direct\nPages inspected: none"
            }).AsTask());

        await completeWithoutOpenedPage.Should().ThrowAsync<WorkflowRuleViolationException>()
            .WithMessage("*positive reachability*Pages inspected*");

        Func<Task> completeWithoutRequiredLabels = () => ExecuteWorkflowAsync(service => service.CompleteTaskAsync(
            new ProcessTaskCompletionCommand
            {
                ProcessTaskId = contactTaskId,
                OutcomeKey = "contacts-extracted",
                CompletionNote = "Pages inspected: none"
            }).AsTask());

        await completeWithoutRequiredLabels.Should().ThrowAsync<WorkflowRuleViolationException>()
            .WithMessage("*Reachability*");

        await ExecuteWorkflowAsync(service => service.CompleteTaskAsync(new ProcessTaskCompletionCommand
        {
            ProcessTaskId = contactTaskId,
            OutcomeKey = "contacts-extracted",
            CompletionNote = "Reachability: none\nPages inspected: none"
        }).AsTask());

        ProcessTask completed = await QueryInAdminContextAsync(db => db.ProcessTasks
            .AsNoTracking()
            .SingleAsync(item => item.Id == contactTaskId));
        completed.State.Should().Be(ProcessTaskState.Completed);
    }

    [CRMAcceptanceFact]
    public async Task Workflow_NonPitchableLeadIsDeferredWithoutSuppression_AndCanBeReevaluated()
    {
        (Guid leadId, _) = await SeedLeadAsync();
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(leadId: leadId, forceCreate: true).AsTask());

        await CompletePendingLeadOutcomesAsync(
            leadId,
            "identity-checked",
            "status-current",
            "resources-gathered",
            "fit-assessed",
            "contacts-extracted",
            "relationships-extracted",
            "related-companies-tipped",
            "deferred");

        Lead deferred = await QueryInAdminContextAsync(db => db.Leads.Include(item => item.Company).SingleAsync(item => item.Id == leadId));
        deferred.Status.Should().Be(LeadStatus.Deferred);
        deferred.Company.IsProspectingSuppressed.Should().BeFalse();

        await ExecuteWorkflowAsync(service => service.EnsureSeedProcessesAsync().AsTask());
        Lead stillDeferred = await QueryInAdminContextAsync(db => db.Leads.SingleAsync(item => item.Id == leadId));
        stillDeferred.Status.Should().Be(LeadStatus.Deferred, "the current process intentionally retains non-pitchable companies for later reconsideration");

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
    public async Task Workflow_DeferredLeadThatClaimedAContactWithoutStructuredPersistence_IsRequeuedForContactResearch()
    {
        (Guid leadId, Guid contactId) = await SeedLeadAsync();
        await ExecuteInAdminContextAsync(async db =>
        {
            LeadContact existingContact = await db.LeadContacts.SingleAsync(item => item.Id == contactId);
            db.LeadContacts.Remove(existingContact);
            await db.SaveChangesAsync();
        });
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(leadId: leadId, forceCreate: true).AsTask());

        await CompletePendingLeadOutcomesAsync(
            leadId,
            "identity-checked",
            "status-current",
            "resources-gathered",
            "fit-assessed",
            "contacts-extracted",
            "relationships-extracted",
            "related-companies-tipped",
            "deferred");

        await ExecuteInAdminContextAsync(async db =>
        {
            ProcessTask legacyInvalidTask = await db.ProcessTasks
                .Include(item => item.ProcessStep)
                .SingleAsync(item => item.LeadId == leadId && item.ProcessStep.Key == "contact-research");
            legacyInvalidTask.CompletionNotes = "Contact found: yes. Contact name: Legacy Person. Contact email: legacy@example.com.";
            await db.SaveChangesAsync();
        });

        await ExecuteWorkflowAsync(service => service.EnsureSeedProcessesAsync().AsTask());

        Lead requeued = await QueryInAdminContextAsync(db => db.Leads.SingleAsync(item => item.Id == leadId));
        requeued.Status.Should().Be(LeadStatus.Imported);
        bool hasPendingTask = await QueryInAdminContextAsync(db => db.ProcessTasks
            .AnyAsync(item => item.LeadId == leadId && item.State == ProcessTaskState.Pending));
        hasPendingTask.Should().BeTrue();
    }

    [CRMAcceptanceFact]
    public async Task Workflow_DeferredLeadWithObsoleteNoContactEvidence_IsRequeuedForFocusedLeadershipResearch()
    {
        (Guid leadId, Guid contactId) = await SeedLeadAsync();
        await ExecuteInAdminContextAsync(async db =>
        {
            LeadContact existingContact = await db.LeadContacts.SingleAsync(item => item.Id == contactId);
            db.LeadContacts.Remove(existingContact);
            await db.SaveChangesAsync();
        });
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(leadId: leadId, forceCreate: true).AsTask());

        await CompletePendingLeadOutcomesAsync(
            leadId,
            "identity-checked",
            "status-current",
            "resources-gathered",
            "fit-assessed",
            "contacts-extracted",
            "relationships-extracted",
            "related-companies-tipped",
            "deferred");

        await ExecuteInAdminContextAsync(async db =>
        {
            ProcessTask legacyNoContactTask = await db.ProcessTasks
                .Include(item => item.ProcessStep)
                .SingleAsync(item => item.LeadId == leadId && item.ProcessStep.Key == "contact-research");
            legacyNoContactTask.CompletionNotes = "Contact found: no. Contact name: none. Contact email: none. Pages inspected: https://example.com/contact.";
            await db.SaveChangesAsync();
        });

        await ExecuteWorkflowAsync(service => service.EnsureSeedProcessesAsync().AsTask());

        Lead requeued = await QueryInAdminContextAsync(db => db.Leads.SingleAsync(item => item.Id == leadId));
        requeued.Status.Should().Be(LeadStatus.Imported);
        bool hasPendingTask = await QueryInAdminContextAsync(db => db.ProcessTasks
            .AnyAsync(item => item.LeadId == leadId && item.State == ProcessTaskState.Pending));
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
