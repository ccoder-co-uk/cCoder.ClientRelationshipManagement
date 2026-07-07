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
    public async Task DispatchDueEmailsAsync_ForApprovedDraft_SendsEmail_And_AdvancesWorkflow()
    {
        (Guid relationshipId, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        ProcessTask emailTask = await MoveOpportunityToEmailStepAsync(opportunityId);
        Email queuedEmail = await QueryInAdminContextAsync(db => db.Emails.FirstAsync(item => item.Id == emailTask.EmailId!.Value));

        using HttpResponseMessage approveQueueResponse = await PostFormWithAntiforgeryAsync("/Emails", "/Emails/Approve", new Dictionary<string, string>
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

        using HttpResponseMessage approveQueueResponse = await PostFormWithAntiforgeryAsync("/Emails", "/Emails/Approve", new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["EmailId"] = queuedEmail.Id.ToString()
        });

        using HttpResponseMessage markQueueSentResponse = await PostFormWithAntiforgeryAsync("/Emails", "/Emails/MarkSent", new Dictionary<string, string>
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
