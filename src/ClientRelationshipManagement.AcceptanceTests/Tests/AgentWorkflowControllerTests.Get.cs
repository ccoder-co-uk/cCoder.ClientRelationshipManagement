using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using ClientRelationshipManagement.Web.Services.Processes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class AgentWorkflowControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_SentEmailReconciliation_ReturnsTrackedSentMailWithoutMailboxCandidatesWhenGraphIsNotConfigured()
    {
        (Guid relationshipId, Guid opportunityId, Guid contactId) = await SeedOpportunityWorkspaceAsync();
        Guid emailId = await SeedSentOpportunityEmailAsync(relationshipId, opportunityId, contactId);
        string token = await Fixture.IssueAgentTokenAsync();
        using HttpRequestMessage request = new(HttpMethod.Get, "/Api/AgentWorkflow/Mailbox/Sent/Reconciliation");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage response = await Client.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        using JsonDocument document = JsonDocument.Parse(content);
        JsonElement trackedEmail = document.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == emailId);
        trackedEmail.GetProperty("company").GetString().Should().NotBeNullOrWhiteSpace();
        trackedEmail.GetProperty("candidates").GetArrayLength().Should().Be(0);
    }

    [CRMAcceptanceFact]
    public async Task Post_CancelUnverifiedEmail_PreservesAnAuditedCancelledRecordWhenMailboxHasNoEvidence()
    {
        (Guid relationshipId, Guid opportunityId, Guid contactId) = await SeedOpportunityWorkspaceAsync();
        Guid emailId = await SeedSentOpportunityEmailAsync(relationshipId, opportunityId, contactId);
        string token = await Fixture.IssueAgentTokenAsync();
        using HttpRequestMessage request = new(HttpMethod.Post, $"/Api/AgentWorkflow/Emails/{emailId}/CancelUnverified");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { reason = "No matching message exists in Sent Items." });

        using HttpResponseMessage response = await Client.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        Email email = await QueryInAdminContextAsync(db => db.Emails.AsNoTracking().SingleAsync(item => item.Id == emailId));
        email.State.Should().Be(EmailState.Cancelled);
        email.SentOn.Should().BeNull();
        email.LastError.Should().Contain("No matching message exists in Sent Items.");
    }

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
        Guid seededTaskId = await QueryInAdminContextAsync(db => db.ProcessTasks
            .Where(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending)
            .Select(item => item.Id)
            .SingleAsync());
        string token = await Fixture.IssueAgentTokenAsync();

        using HttpRequestMessage request = new(HttpMethod.Get, $"/Api/AgentWorkflow/Tasks/Due?processTaskId={seededTaskId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage response = await Client.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);

        using JsonDocument jsonDocument = JsonDocument.Parse(content);
        jsonDocument.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        jsonDocument.RootElement.GetArrayLength().Should().BeGreaterThan(0);
        JsonElement firstTask = jsonDocument.RootElement[0];
        firstTask.GetProperty("stepObjective").GetString().Should().NotBeNullOrWhiteSpace();
        firstTask.GetProperty("producedFacts").GetString().Should().NotBeNullOrWhiteSpace();
        firstTask.GetProperty("viabilityImpact").GetString().Should().NotBeNullOrWhiteSpace();
        firstTask.GetProperty("companyHistory").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [CRMAcceptanceFact]
    public async Task Post_CompleteTask_RejectsAnOutcomeNotDeclaredByTheCurrentStep()
    {
        (_, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true).AsTask());
        ProcessTask task = await QueryInAdminContextAsync(db => db.ProcessTasks
            .FirstAsync(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending));
        string token = await Fixture.IssueAgentTokenAsync();

        using HttpRequestMessage request = new(HttpMethod.Post, $"/Api/AgentWorkflow/Tasks/{task.Id}/Complete");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            outcomeKey = "skip-to-client",
            completionNote = "Attempted undeclared transition."
        });

        using HttpResponseMessage response = await Client.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, content);
        content.Should().Contain("not valid for the current workflow step");
        ProcessTask unchanged = await QueryInAdminContextAsync(db => db.ProcessTasks.FirstAsync(item => item.Id == task.Id));
        unchanged.State.Should().Be(ProcessTaskState.Pending);
        unchanged.CompletionOutcomeKey.Should().BeNullOrWhiteSpace();
    }

    [CRMAcceptanceFact]
    public async Task Get_ProcessMetrics_ReturnsOrderedStepBottleneckEvidence()
    {
        string token = await Fixture.IssueAgentTokenAsync();
        using HttpRequestMessage request = new(HttpMethod.Get, "/Api/AgentWorkflow/Processes/Metrics");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage response = await Client.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        using JsonDocument document = JsonDocument.Parse(content);
        JsonElement leadProcess = document.RootElement.EnumerateArray()
            .First(item => item.GetProperty("scopeType").GetString() == "Lead");
        JsonElement steps = leadProcess.GetProperty("steps");
        steps.GetArrayLength().Should().BeGreaterThan(0);
        steps[0].TryGetProperty("overdueCount", out _).Should().BeTrue();
        steps[0].TryGetProperty("completedWithoutEvidenceCount", out _).Should().BeTrue();
        steps[0].TryGetProperty("averageTurnaroundMinutes", out _).Should().BeTrue();
    }

    [CRMAcceptanceFact]
    public async Task Get_DueTasks_ReturnsTheCallingWorkersClaimedTaskById()
    {
        (_, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true).AsTask());
        ProcessTask task = await QueryInAdminContextAsync(db => db.ProcessTasks
            .FirstAsync(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending));
        await ExecuteInAdminContextAsync(async db =>
        {
            ProcessTask tracked = await db.ProcessTasks.FirstAsync(item => item.Id == task.Id);
            tracked.AgentClaimId = Guid.NewGuid();
            tracked.AgentClaimedBy = Fixture.Settings.UserId;
            tracked.AgentClaimedOn = DateTimeOffset.UtcNow;
            tracked.AgentClaimExpiresOn = DateTimeOffset.UtcNow.AddMinutes(5);
            await db.SaveChangesAsync();
        });

        string token = await Fixture.IssueAgentTokenAsync();
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"/Api/AgentWorkflow/Tasks/Due?limit=1&processTaskId={task.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await Client.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        using JsonDocument document = JsonDocument.Parse(content);
        document.RootElement.GetArrayLength().Should().Be(1);
        document.RootElement[0].GetProperty("processTaskId").GetGuid().Should().Be(task.Id);
    }

    [CRMAcceptanceFact]
    public async Task Get_EmailEvidence_ReportsWhenNoOutboundEmailCanBeAssessed()
    {
        (_, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true).AsTask());
        Guid taskId = await QueryInAdminContextAsync(db => db.ProcessTasks
            .Where(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending)
            .Select(item => item.Id)
            .FirstAsync());
        string token = await Fixture.IssueAgentTokenAsync();

        using HttpRequestMessage request = new(HttpMethod.Get, $"/Api/AgentWorkflow/Tasks/{taskId}/EmailEvidence");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await Client.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        using JsonDocument document = JsonDocument.Parse(content);
        document.RootElement.GetProperty("hasMatchingEvidence").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("noEvidenceConfirmed").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("status").GetString().Should().Contain("No sent outbound email");
    }

    [CRMAcceptanceFact]
    public async Task Get_DueTasks_ExcludesHumanApprovals_And_ContactTasksAwaitingApproval()
    {
        (_, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true).AsTask());

        ProcessTask task = await QueryInAdminContextAsync(db =>
            db.ProcessTasks.FirstAsync(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending));

        await ExecuteInAdminContextAsync(async db =>
        {
            ProcessTask trackedTask = await db.ProcessTasks.FirstAsync(item => item.Id == task.Id);
            trackedTask.ActionType = ProcessActionType.Approval;
            await db.SaveChangesAsync();
        });

        string token = await Fixture.IssueAgentTokenAsync();
        using HttpRequestMessage approvalRequest = new(HttpMethod.Get, "/Api/AgentWorkflow/Tasks/Due?limit=100");
        approvalRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage approvalResponse = await Client.SendAsync(approvalRequest);
        string approvalContent = await approvalResponse.Content.ReadAsStringAsync();

        approvalResponse.StatusCode.Should().Be(HttpStatusCode.OK, approvalContent);
        approvalContent.Should().NotContain(task.Id.ToString());

        await ExecuteInAdminContextAsync(async db =>
        {
            ProcessTask trackedTask = await db.ProcessTasks.FirstAsync(item => item.Id == task.Id);
            trackedTask.ActionType = ProcessActionType.Call;
            db.AgentMessages.Add(new AgentMessage
            {
                Id = Guid.NewGuid(),
                ProcessTaskId = task.Id,
                Kind = AgentMessageKind.ApprovalRequest,
                State = AgentMessageState.Pending,
                CorrelationKey = $"approval:contact:{task.Id}",
                Title = "Approve contact",
                Body = "Contact plan ready for approval.",
                AgentName = "Task Agent",
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId
            });
            await db.SaveChangesAsync();
        });

        using HttpRequestMessage contactRequest = new(HttpMethod.Get, "/Api/AgentWorkflow/Tasks/Due?limit=100");
        contactRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage contactResponse = await Client.SendAsync(contactRequest);
        string contactContent = await contactResponse.Content.ReadAsStringAsync();

        contactResponse.StatusCode.Should().Be(HttpStatusCode.OK, contactContent);
        contactContent.Should().NotContain(task.Id.ToString());
    }

    [CRMAcceptanceFact]
    public async Task RelatedDraftEmails_RecoversStepFromRenderedSubjectWithoutTaskOrOpportunityProvenance()
    {
        (Guid relationshipId, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        ProcessTask sourceTask = await MoveOpportunityToEmailStepAsync(opportunityId);
        Guid emailId = sourceTask.EmailId!.Value;
        Guid messageId = Guid.NewGuid();
        await ExecuteInAdminContextAsync(async db =>
        {
            ProcessTask task = await db.ProcessTasks.SingleAsync(item => item.Id == sourceTask.Id);
            task.EmailId = null;
            Email email = await db.Emails.SingleAsync(item => item.Id == emailId);
            email.OpportunityId = null;
            email.State = EmailState.Rejected;
            db.AgentMessages.Add(new AgentMessage
            {
                Id = messageId,
                TenantId = AcceptanceSettings.TenantId,
                TenantCompanyRelationshipId = relationshipId,
                EmailId = emailId,
                Kind = AgentMessageKind.FeedbackRequest,
                State = AgentMessageState.Pending,
                Title = "Review rejected email",
                Body = "The generated email exposed internal drafting instructions.",
                AgentName = "Approval Agent",
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId
            });
            await db.SaveChangesAsync();
        });

        string token = await Fixture.IssueAgentTokenAsync();
        using HttpRequestMessage request = new(HttpMethod.Get,
            $"/Api/AgentWorkflow/Messages/{messageId}/RelatedDraftEmails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await Client.SendAsync(request);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        using JsonDocument document = JsonDocument.Parse(content);
        document.RootElement.GetProperty("processName").GetString().Should().Be("Opportunity Conversion");
        document.RootElement.GetProperty("processStepKey").GetString().Should().Be("intro-email");
    }

    [CRMAcceptanceFact]
    public async Task RelatedDraftEmails_BackfillsLegacySourceProvenance_AndRefreshesOnlyUnsentDraftsFromTheSameStep()
    {
        (Guid sourceRelationshipId, Guid sourceOpportunityId, _) = await SeedOpportunityWorkspaceAsync();
        (_, Guid relatedOpportunityId, _) = await SeedOpportunityWorkspaceAsync();
        ProcessTask sourceTask = await MoveOpportunityToEmailStepAsync(sourceOpportunityId);
        ProcessTask relatedTask = await MoveOpportunityToEmailStepAsync(relatedOpportunityId);
        sourceTask.EmailId.Should().HaveValue();
        relatedTask.EmailId.Should().HaveValue();
        Guid sourceEmailId = sourceTask.EmailId.Value;
        Guid relatedEmailId = relatedTask.EmailId.Value;
        Guid messageId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await ExecuteInAdminContextAsync(async db =>
        {
            Email sourceEmail = await db.Emails.SingleAsync(item => item.Id == sourceEmailId);
            sourceEmail.State = EmailState.Rejected;

            Email relatedEmail = await db.Emails.SingleAsync(item => item.Id == relatedEmailId);
            relatedEmail.Subject = "Generic outreach";
            relatedEmail.BodyText = "Avoid leading with: internal drafting guidance.";
            relatedEmail.BodyHtml = relatedEmail.BodyText;
            relatedEmail.State = EmailState.Approved;
            relatedEmail.ApprovedBy = Fixture.Settings.UserId;
            relatedEmail.ApprovedOn = now;

            ProcessTask trackedRelatedTask = await db.ProcessTasks.SingleAsync(item => item.Id == relatedTask.Id);
            trackedRelatedTask.RenderedEmailSubject = relatedEmail.Subject;
            trackedRelatedTask.RenderedEmailBody = relatedEmail.BodyText;

            db.AgentMessages.Add(new AgentMessage
            {
                Id = messageId,
                TenantId = AcceptanceSettings.TenantId,
                TenantCompanyRelationshipId = sourceRelationshipId,
                OpportunityId = sourceOpportunityId,
                EmailId = sourceEmailId,
                // Deliberately omit task, step, and process provenance to exercise the legacy backfill path.
                Kind = AgentMessageKind.FeedbackRequest,
                State = AgentMessageState.Pending,
                CorrelationKey = $"acceptance:related-drafts:{messageId}",
                Title = "Review rejected email",
                Body = "Apply the approved correction to other unsent drafts from this source step.",
                AgentName = "Approval Agent",
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });
            await db.SaveChangesAsync();
        });

        string token = await Fixture.IssueAgentTokenAsync();
        using HttpRequestMessage inspectRequest = new(
            HttpMethod.Get,
            $"/Api/AgentWorkflow/Messages/{messageId}/RelatedDraftEmails");
        inspectRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage inspectResponse = await Client.SendAsync(inspectRequest);
        string inspectContent = await inspectResponse.Content.ReadAsStringAsync();

        inspectResponse.StatusCode.Should().Be(HttpStatusCode.OK, inspectContent);
        using (JsonDocument inspectDocument = JsonDocument.Parse(inspectContent))
        {
            inspectDocument.RootElement.GetProperty("processStepKey").GetString().Should().Be("intro-email");
            Guid[] eligibleEmailIds = inspectDocument.RootElement.GetProperty("drafts")
                .EnumerateArray()
                .Select(item => item.GetProperty("emailId").GetGuid())
                .ToArray();
            eligibleEmailIds.Should().Contain(relatedEmailId);
            eligibleEmailIds.Should().NotContain(sourceEmailId);
        }

        AgentMessage backfilledMessage = await QueryInAdminContextAsync(db => db.AgentMessages
            .AsNoTracking()
            .SingleAsync(item => item.Id == messageId));
        backfilledMessage.ProcessTaskId.Should().Be(sourceTask.Id);
        backfilledMessage.ProcessStepId.Should().NotBeNull();
        backfilledMessage.ProcessDefinitionId.Should().NotBeNull();

        using HttpRequestMessage refreshRequest = new(
            HttpMethod.Post,
            $"/Api/AgentWorkflow/Messages/{messageId}/RefreshRelatedDraftEmails");
        refreshRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage refreshResponse = await Client.SendAsync(refreshRequest);
        string refreshContent = await refreshResponse.Content.ReadAsStringAsync();

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK, refreshContent);
        using (JsonDocument refreshDocument = JsonDocument.Parse(refreshContent))
        {
            refreshDocument.RootElement.GetProperty("updatedCount").GetInt32().Should().BeGreaterThan(0);
            refreshDocument.RootElement.GetProperty("updatedEmailIds")
                .EnumerateArray()
                .Select(item => item.GetGuid())
                .Should().Contain(relatedEmailId);
        }

        (Email Source, Email Related, ProcessTask RelatedTask, string[] AuditEntries) result =
            await QueryInAdminContextAsync(async db =>
            {
                Email source = await db.Emails.AsNoTracking().SingleAsync(item => item.Id == sourceEmailId);
                Email related = await db.Emails.AsNoTracking().SingleAsync(item => item.Id == relatedEmailId);
                ProcessTask task = await db.ProcessTasks.AsNoTracking().SingleAsync(item => item.Id == relatedTask.Id);
                string[] entries = await db.AgentMessageEntries.AsNoTracking()
                    .Where(item => item.AgentMessageId == messageId && item.Role == "System")
                    .Select(item => item.Body)
                    .ToArrayAsync();
                return (source, related, task, entries);
            });

        result.Source.State.Should().Be(EmailState.Rejected);
        result.Related.State.Should().Be(EmailState.Draft);
        result.Related.ApprovedBy.Should().BeNull();
        result.Related.ApprovedOn.Should().BeNull();
        result.Related.Subject.Should().Be(result.RelatedTask.RenderedEmailSubject);
        result.Related.BodyText.Should().Be(result.RelatedTask.RenderedEmailBody);
        result.Related.BodyText.Should().NotContain("Avoid leading with:");
        result.AuditEntries.Should().ContainSingle(entry => entry.Contains("remain Draft", StringComparison.Ordinal));
    }

    [CRMAcceptanceFact]
    public async Task RelatedDraftEmails_RequiresAProcessProposalWhenTheApprovedCorrectionDiffersFromTheLiveTemplate()
    {
        (Guid sourceRelationshipId, Guid sourceOpportunityId, _) = await SeedOpportunityWorkspaceAsync();
        (_, Guid relatedOpportunityId, _) = await SeedOpportunityWorkspaceAsync();
        ProcessTask sourceTask = await MoveOpportunityToEmailStepAsync(sourceOpportunityId);
        ProcessTask relatedTask = await MoveOpportunityToEmailStepAsync(relatedOpportunityId);
        Guid sourceEmailId = sourceTask.EmailId!.Value;
        Guid relatedEmailId = relatedTask.EmailId!.Value;
        Guid messageId = Guid.NewGuid();
        Guid correctionEmailId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string originalRelatedBody = null;

        await ExecuteInAdminContextAsync(async db =>
        {
            Email sourceEmail = await db.Emails.SingleAsync(item => item.Id == sourceEmailId);
            sourceEmail.State = EmailState.Rejected;

            Email relatedEmail = await db.Emails.SingleAsync(item => item.Id == relatedEmailId);
            originalRelatedBody = relatedEmail.BodyText ?? relatedEmail.BodyHtml;

            db.Emails.Add(new Email
            {
                Id = correctionEmailId,
                TenantCompanyRelationshipId = sourceEmail.TenantCompanyRelationshipId,
                OpportunityId = sourceEmail.OpportunityId,
                CompanyContactId = sourceEmail.CompanyContactId,
                SenderUserId = sourceEmail.SenderUserId,
                FromDisplayName = sourceEmail.FromDisplayName,
                FromEmailAddress = sourceEmail.FromEmailAddress,
                ToAddresses = sourceEmail.ToAddresses,
                Subject = sourceEmail.Subject,
                BodyText = "Hello Stuart,\n\nThis is the human-approved recipient-ready correction.\n\nKind regards,\nPaul Ward",
                BodyHtml = "Hello Stuart,\n\nThis is the human-approved recipient-ready correction.\n\nKind regards,\nPaul Ward",
                IsBodyHtml = false,
                State = EmailState.Sent,
                ApprovedBy = Fixture.Settings.UserId,
                ApprovedOn = now,
                SentOn = now,
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });
            db.AgentMessages.AddRange(
                new AgentMessage
                {
                    Id = messageId,
                    TenantId = AcceptanceSettings.TenantId,
                    TenantCompanyRelationshipId = sourceRelationshipId,
                    OpportunityId = sourceOpportunityId,
                    EmailId = sourceEmailId,
                    Kind = AgentMessageKind.FeedbackRequest,
                    State = AgentMessageState.Pending,
                    CorrelationKey = $"acceptance:approved-correction:{messageId}",
                    Title = "Review rejected email",
                    Body = "Apply the accepted correction systemically.",
                    AgentName = "Approval Agent",
                    CreatedBy = Fixture.Settings.UserId,
                    LastUpdatedBy = Fixture.Settings.UserId,
                    CreatedOn = now,
                    LastUpdated = now
                },
                new AgentMessage
                {
                    Id = Guid.NewGuid(),
                    TenantId = AcceptanceSettings.TenantId,
                    TenantCompanyRelationshipId = sourceRelationshipId,
                    OpportunityId = sourceOpportunityId,
                    EmailId = correctionEmailId,
                    Kind = AgentMessageKind.ApprovalRequest,
                    State = AgentMessageState.Completed,
                    CorrelationKey = $"approval:replacement-email:{messageId}",
                    Title = "Approved corrected replacement email",
                    Body = "Human-approved correction.",
                    AgentName = "Approval Agent",
                    CreatedBy = Fixture.Settings.UserId,
                    LastUpdatedBy = Fixture.Settings.UserId,
                    CreatedOn = now,
                    LastUpdated = now
                });
            await db.SaveChangesAsync();

        });

        string token = await Fixture.IssueAgentTokenAsync();
        using HttpRequestMessage inspectRequest = new(
            HttpMethod.Get,
            $"/Api/AgentWorkflow/Messages/{messageId}/RelatedDraftEmails");
        inspectRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage inspectResponse = await Client.SendAsync(inspectRequest);
        string inspectContent = await inspectResponse.Content.ReadAsStringAsync();

        inspectResponse.StatusCode.Should().Be(HttpStatusCode.OK, inspectContent);
        using (JsonDocument document = JsonDocument.Parse(inspectContent))
        {
            document.RootElement.GetProperty("approvedCorrection").GetProperty("emailId")
                .GetGuid().Should().Be(correctionEmailId);
            document.RootElement.GetProperty("approvedCorrection").GetProperty("state")
                .GetInt32().Should().Be((int)EmailState.Sent);
            document.RootElement.GetProperty("liveTemplateMatchesApprovedCorrection")
                .GetBoolean().Should().BeFalse();
        }

        using HttpRequestMessage refreshRequest = new(
            HttpMethod.Post,
            $"/Api/AgentWorkflow/Messages/{messageId}/RefreshRelatedDraftEmails");
        refreshRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage refreshResponse = await Client.SendAsync(refreshRequest);
        string refreshContent = await refreshResponse.Content.ReadAsStringAsync();

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Conflict, refreshContent);
        refreshContent.Should().Contain("process proposal");
        Email unchangedRelated = await QueryInAdminContextAsync(db => db.Emails
            .AsNoTracking()
            .SingleAsync(item => item.Id == relatedEmailId));
        (unchangedRelated.BodyText ?? unchangedRelated.BodyHtml).Should().Be(originalRelatedBody);
    }

    [CRMAcceptanceFact]
    public async Task CompleteTask_RepairsALegacyMissingCurrentTaskPointer_OnlyForTheUniqueCurrentStepTask()
    {
        (_, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(
            opportunityId: opportunityId,
            forceCreate: true).AsTask());
        ProcessTask task = await QueryInAdminContextAsync(db => db.ProcessTasks
            .AsNoTracking()
            .Include(item => item.ProcessInstance)
            .SingleAsync(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending));

        await ExecuteInAdminContextAsync(async db =>
        {
            ProcessInstance instance = await db.ProcessInstances.SingleAsync(item => item.Id == task.ProcessInstanceId);
            instance.CurrentProcessTaskId = null;
            await db.SaveChangesAsync();
        });

        ProcessTask completed = null;
        await ExecuteWorkflowAsync(async service =>
        {
            completed = await service.CompleteTaskAsync(new ProcessTaskCompletionCommand
            {
                ProcessTaskId = task.Id,
                OutcomeKey = "ready",
                CompletionNote = "Legacy current-task pointer repaired safely."
            });
        });

        completed.Should().NotBeNull();
        completed.State.Should().Be(ProcessTaskState.Completed);
        ProcessInstance repaired = await QueryInAdminContextAsync(db => db.ProcessInstances
            .AsNoTracking()
            .SingleAsync(item => item.Id == task.ProcessInstanceId));
        ProcessTask next = await QueryInAdminContextAsync(db => db.ProcessTasks
            .AsNoTracking()
            .SingleAsync(item => item.ProcessInstanceId == task.ProcessInstanceId
                && item.State == ProcessTaskState.Pending));
        repaired.CurrentProcessTaskId.Should().Be(next.Id);
        repaired.CurrentProcessStepId.Should().Be(next.ProcessStepId);
    }
}
