using System.Net;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class EmailsControllerTests
{
    [CRMAcceptanceFact]
    public async Task CancelledWorkflowDraft_IsHiddenAndCannotBeApproved()
    {
        (Guid relationshipId, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        ProcessTask emailTask = await MoveOpportunityToEmailStepAsync(opportunityId);
        Guid emailId = emailTask.EmailId!.Value;
        string obsoleteSubject = Unique("Cancelled workflow draft");

        await ExecuteInAdminContextAsync(async db =>
        {
            ProcessTask task = await db.ProcessTasks.SingleAsync(item => item.Id == emailTask.Id);
            task.State = ProcessTaskState.Cancelled;
            Email email = await db.Emails.SingleAsync(item => item.Id == emailId);
            email.Subject = obsoleteSubject;
            email.State = EmailState.Draft;
            await db.SaveChangesAsync();
        });

        string html = await GetStringAsync($"/Admin/Emails?id={emailId}");
        html.Should().NotContain(obsoleteSubject);

        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync(
            "/Admin/Emails",
            "/Admin/Emails/Approve",
            new Dictionary<string, string>
            {
                ["ClientId"] = relationshipId.ToString(),
                ["EmailId"] = emailId.ToString()
            });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Email unchanged = await QueryInAdminContextAsync(db => db.Emails.AsNoTracking()
            .SingleAsync(item => item.Id == emailId));
        unchanged.State.Should().Be(EmailState.Draft);
    }

    [CRMAcceptanceFact]
    public async Task Post_Reject_ArchivesEmailAndStartsApprovalConversationWithReason()
    {
        (Guid relationshipId, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        ProcessTask emailTask = await MoveOpportunityToEmailStepAsync(opportunityId);
        Email queuedEmail = await QueryInAdminContextAsync(db => db.Emails.FirstAsync(item => item.Id == emailTask.EmailId!.Value));

        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync("/Admin/Emails", "/Admin/Emails/Reject", new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["EmailId"] = queuedEmail.Id.ToString(),
            ["Reason"] = "The message makes an unsupported promise about delivery time."
        });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Email rejected = await QueryInAdminContextAsync(db => db.Emails.FirstAsync(item => item.Id == queuedEmail.Id));
        AgentMessage conversation = await QueryInAdminContextAsync(db => db.AgentMessages
            .Include(item => item.Entries)
            .FirstAsync(item => item.EmailId == queuedEmail.Id && item.CorrelationKey == $"email-rejection:{queuedEmail.Id}"));

        rejected.State.Should().Be(EmailState.Rejected);
        conversation.State.Should().Be(AgentMessageState.Pending);
        conversation.ProcessTaskId.Should().Be(emailTask.Id);
        conversation.ProcessStepId.Should().Be(emailTask.ProcessStepId);
        conversation.Entries.Should().Contain(item => item.Role == "User" && item.Body.Contains("unsupported promise"));
    }

    [CRMAcceptanceFact]
    public async Task Post_ReviewAndApprove_SavesHumanEditsBeforeApproval()
    {
        (Guid relationshipId, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        ProcessTask emailTask = await MoveOpportunityToEmailStepAsync(opportunityId);
        Email queuedEmail = await QueryInAdminContextAsync(db => db.Emails.FirstAsync(item => item.Id == emailTask.EmailId!.Value));

        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync("/Admin/Emails", "/Admin/Emails/ReviewAndApprove", new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["EmailId"] = queuedEmail.Id.ToString(),
            ["Subject"] = "Reviewed subject",
            ["Body"] = "Reviewed and corrected email content."
        });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Email approved = await QueryInAdminContextAsync(db => db.Emails.FirstAsync(item => item.Id == queuedEmail.Id));
        approved.State.Should().Be(EmailState.Approved);
        approved.Subject.Should().Be("Reviewed subject");
        approved.BodyText.Should().Be("Reviewed and corrected email content.");
    }

    [CRMAcceptanceFact]
    public async Task DispatchDueEmailsAsync_ForApprovedDraft_SendsEmail_And_AdvancesWorkflow()
    {
        (Guid relationshipId, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        ProcessTask emailTask = await MoveOpportunityToEmailStepAsync(opportunityId);
        Email queuedEmail = await QueryInAdminContextAsync(db => db.Emails.FirstAsync(item => item.Id == emailTask.EmailId!.Value));

        using HttpResponseMessage approveQueueResponse = await PostFormWithAntiforgeryAsync("/Admin/Emails", "/Admin/Emails/Approve", new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["EmailId"] = queuedEmail.Id.ToString()
        });

        approveQueueResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        int dispatchedCount = await ExecuteEmailDispatchAsync(service => service.DispatchDueEmailsAsync().AsTask());

        Email updatedEmail = await QueryInAdminContextAsync(db => db.Emails.FirstAsync(item => item.Id == queuedEmail.Id));
        ProcessTask completedTask = await QueryInAdminContextAsync(db => db.ProcessTasks.FirstAsync(item => item.Id == emailTask.Id));
        ProcessTask followUpTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks
                .Where(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending)
                .OrderBy(item => item.DueOn)
                .FirstAsync());

        dispatchedCount.Should().Be(1);
        updatedEmail.State.Should().Be(EmailState.Sent);
        updatedEmail.ExternalMessageId.Should().StartWith("acceptance-");
        completedTask.State.Should().Be(ProcessTaskState.Completed);
        followUpTask.Id.Should().NotBe(emailTask.Id);
        followUpTask.RenderedTitle.Should().Contain("Review");
    }

    [CRMAcceptanceFact]
    public async Task Post_Approve_And_MarkSent_UpdateQueueState()
    {
        (Guid relationshipId, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        ProcessTask emailTask = await MoveOpportunityToEmailStepAsync(opportunityId);
        Email queuedEmail = await QueryInAdminContextAsync(db => db.Emails.FirstAsync(item => item.Id == emailTask.EmailId!.Value));

        using HttpResponseMessage approveQueueResponse = await PostFormWithAntiforgeryAsync("/Admin/Emails", "/Admin/Emails/Approve", new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["EmailId"] = queuedEmail.Id.ToString()
        });

        using HttpResponseMessage markQueueSentResponse = await PostFormWithAntiforgeryAsync("/Admin/Emails", "/Admin/Emails/MarkSent", new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["EmailId"] = queuedEmail.Id.ToString()
        });

        approveQueueResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        markQueueSentResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        Email updatedEmail = await QueryInAdminContextAsync(db => db.Emails.FirstAsync(item => item.Id == queuedEmail.Id));
        ProcessTask updatedTask = await QueryInAdminContextAsync(db => db.ProcessTasks.FirstAsync(item => item.Id == emailTask.Id));

        updatedEmail.State.Should().Be(EmailState.Sent);
        updatedTask.State.Should().Be(ProcessTaskState.Completed);
    }
}
