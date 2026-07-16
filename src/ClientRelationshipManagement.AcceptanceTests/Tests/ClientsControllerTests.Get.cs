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

        indexHtml.Should().Contain("Client accounts");
        editHtml.Should().Contain("Client Details");
    }

    [CRMAcceptanceFact]
    public async Task Get_Index_AcceptsClientAccountAndTaskFilters()
    {
        string html = await GetStringAsync("/Clients?scope=accounts&tasks=overdue");

        html.Should().Contain("name=\"scope\" value=\"accounts\"");
        html.Should().Contain("name=\"tasks\" value=\"overdue\"");
    }

    [CRMAcceptanceFact]
    public async Task Get_Index_UsesWorkflowTaskForNextActionAndShowsSyncedStage()
    {
        (Guid relationshipId, Guid opportunityId, Guid companyContactId) = await SeedOpportunityWorkspaceAsync(SalesPipelineStage.OutreachReady);
        await SeedSentOpportunityEmailAsync(relationshipId, opportunityId, companyContactId);

        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true).AsTask());

        string opportunityHtml = await GetStringAsync("/Opportunities");
        string clientsHtml = await GetStringAsync("/Clients");
        string companyName = await QueryInAdminContextAsync(db => db.Opportunities
            .Where(item => item.Id == opportunityId)
            .Select(item => item.TenantCompanyRelationship.Company.OfficialName)
            .SingleAsync());

        opportunityHtml.Should().Contain("Review the response");
        opportunityHtml.Should().Contain("Outreach Sent");
        clientsHtml.Should().NotContain(companyName);
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
