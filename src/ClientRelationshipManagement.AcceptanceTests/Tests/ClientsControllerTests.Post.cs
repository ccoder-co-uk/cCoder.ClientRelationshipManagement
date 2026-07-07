using System.Net;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientsControllerTests
{
    [CRMAcceptanceFact]
    public async Task Post_CreateRedirectsToLeads()
    {
        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync("/Leads", "/Clients/Create", new Dictionary<string, string>());
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("/Leads");
    }

    [CRMAcceptanceFact]
    public async Task Post_Edit_RecordActivity_And_AddOpportunity_UpdateData()
    {
        (Guid relationshipId, _, _) = await SeedOpportunityWorkspaceAsync();

        using HttpResponseMessage editResponse = await PostFormWithAntiforgeryAsync($"/Clients/Edit/{relationshipId}", "/Clients/Edit", new Dictionary<string, string>
        {
            ["Id"] = relationshipId.ToString(),
            ["CompanyName"] = "Updated Company",
            ["AccountOwner"] = "Updated Owner",
            ["Status"] = RelationshipStatus.ActiveOpportunity.ToString(),
            ["CurrentStage"] = SalesPipelineStage.OutreachReady.ToString(),
            ["Priority"] = RelationshipPriority.High.ToString()
        });

        editResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage activityResponse = await PostFormWithAntiforgeryAsync($"/Clients/Edit/{relationshipId}", "/Clients/RecordActivity", new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["Type"] = ActivityType.Note.ToString(),
            ["Direction"] = ActivityDirection.Internal.ToString(),
            ["Summary"] = "Activity summary",
            ["Outcome"] = "Activity outcome"
        });

        activityResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage opportunityResponse = await PostFormWithAntiforgeryAsync($"/Clients/Edit/{relationshipId}", "/Clients/AddOpportunity", new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["Type"] = OpportunityType.General.ToString(),
            ["Stage"] = SalesPipelineStage.Researched.ToString(),
            ["PainSummary"] = "New opportunity pain"
        });

        opportunityResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        TenantCompanyRelationship relationship = await QueryInAdminContextAsync(db =>
            db.TenantCompanyRelationships.Include(item => item.Company).FirstAsync(item => item.Id == relationshipId));

        relationship.Company.OfficialName.Should().Be("Updated Company");
        relationship.AccountOwnerDisplayName.Should().Be("Updated Owner");
        relationship.Status.Should().Be(RelationshipStatus.ActiveOpportunity);

        bool activityExists = await QueryInAdminContextAsync(db =>
            db.Activities.AnyAsync(item => item.TenantCompanyRelationshipId == relationshipId && item.Summary == "Activity summary"));
        bool opportunityExists = await QueryInAdminContextAsync(db =>
            db.Opportunities.AnyAsync(item => item.TenantCompanyRelationshipId == relationshipId && item.PainSummary == "New opportunity pain"));

        activityExists.Should().BeTrue();
        opportunityExists.Should().BeTrue();
    }

    [CRMAcceptanceFact]
    public async Task Post_SaveEmailDraft_ApproveEmail_And_MarkEmailSent_UpdateDraftLifecycle()
    {
        (Guid relationshipId, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        ProcessTask emailTask = await MoveOpportunityToEmailStepAsync(opportunityId);
        emailTask.EmailId.Should().NotBeNull();
        emailTask.Email.Should().NotBeNull();

        using HttpResponseMessage saveDraftResponse = await PostFormWithAntiforgeryAsync($"/Clients/Edit/{relationshipId}", "/Clients/SaveEmailDraft", new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["EmailId"] = emailTask.EmailId!.Value.ToString(),
            ["ClientMaterialId"] = emailTask.Email!.MaterialId!.Value.ToString(),
            ["ClientOpportunityId"] = opportunityId.ToString(),
            ["Subject"] = "Draft subject",
            ["Body"] = "Draft body",
            ["ToAddresses"] = "client@example.com"
        });

        saveDraftResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        Email email = await QueryInAdminContextAsync(db => db.Emails.FirstAsync(item => item.Id == emailTask.EmailId!.Value));

        using HttpResponseMessage approveClientResponse = await PostFormWithAntiforgeryAsync($"/Clients/Edit/{relationshipId}", "/Clients/ApproveEmail", new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["EmailId"] = email.Id.ToString()
        });

        approveClientResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage markClientSentResponse = await PostFormWithAntiforgeryAsync($"/Clients/Edit/{relationshipId}", "/Clients/MarkEmailSent", new Dictionary<string, string>
        {
            ["ClientId"] = relationshipId.ToString(),
            ["EmailId"] = email.Id.ToString()
        });

        markClientSentResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        ProcessTask updatedClientTask = await QueryInAdminContextAsync(db => db.ProcessTasks.FirstAsync(item => item.Id == emailTask.Id));
        updatedClientTask.State.Should().Be(ProcessTaskState.Completed);
    }
}
