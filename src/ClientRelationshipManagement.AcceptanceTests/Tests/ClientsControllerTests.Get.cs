using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class ClientsControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_CreateRedirects_And_IndexEditRender()
    {
        (Guid relationshipId, _, _) = await SeedOpportunityWorkspaceAsync();

        using HttpResponseMessage getCreate = await Client.GetAsync("/Clients/Create");
        getCreate.StatusCode.Should().Be(System.Net.HttpStatusCode.Redirect);

        string indexHtml = await GetStringAsync("/Clients");
        string editHtml = await GetStringAsync($"/Clients/Edit/{relationshipId}");

        indexHtml.Should().Contain("Clients Page");
        editHtml.Should().Contain("Client Details");
    }

    [CRMAcceptanceFact]
    public async Task Get_Index_UsesWorkflowTaskForNextActionAndShowsSyncedStage()
    {
        (Guid relationshipId, Guid opportunityId, Guid companyContactId) = await SeedOpportunityWorkspaceAsync(SalesPipelineStage.OutreachReady);
        await SeedSentOpportunityEmailAsync(relationshipId, opportunityId, companyContactId);

        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true).AsTask());

        string indexHtml = await GetStringAsync("/Clients");

        indexHtml.Should().Contain("Review the response");
        indexHtml.Should().Contain("Outreach Sent");
    }

    [CRMAcceptanceFact]
    public async Task Get_Edit_ReconcilesImportedSentEmailState_And_ReplacesStalePendingTask()
    {
        (Guid relationshipId, Guid opportunityId, Guid companyContactId) = await SeedOpportunityWorkspaceAsync(SalesPipelineStage.OutreachReady);

        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true).AsTask());
        await SeedSentOpportunityEmailAsync(relationshipId, opportunityId, companyContactId, (EmailState)3);

        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId).AsTask());

        string editHtml = await GetStringAsync($"/Clients/Edit/{relationshipId}");
        editHtml.Should().Contain("Review the response");

        Opportunity opportunity = await QueryInAdminContextAsync(db =>
            db.Opportunities.FirstAsync(item => item.Id == opportunityId));
        TenantCompanyRelationship relationship = await QueryInAdminContextAsync(db =>
            db.TenantCompanyRelationships.FirstAsync(item => item.Id == relationshipId));
        List<ProcessTask> tasks = await QueryInAdminContextAsync(db =>
            db.ProcessTasks
                .Include(item => item.ProcessStep)
                .Where(item => item.OpportunityId == opportunityId)
                .OrderBy(item => item.CreatedOn)
                .ToListAsync());

        opportunity.Stage.Should().Be(SalesPipelineStage.OutreachSent);
        relationship.CurrentStage.Should().Be(SalesPipelineStage.OutreachSent);
        relationship.Status.Should().Be(RelationshipStatus.ActiveOpportunity);
        tasks.Should().ContainSingle(item => item.State == ProcessTaskState.Pending && item.ProcessStep.Key == "review-response");
        tasks.Should().Contain(item => item.State == ProcessTaskState.Cancelled && item.ProcessStep.Key == "intro-email");
    }
}
