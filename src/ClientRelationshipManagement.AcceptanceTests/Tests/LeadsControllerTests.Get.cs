using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class LeadsControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_CompanyHistory_ShowsTenantScopedLifecycleEvidence()
    {
        (Guid leadId, _) = await SeedLeadAsync();
        Guid companyId = await QueryInAdminContextAsync(db =>
            db.Leads.Where(item => item.Id == leadId).Select(item => item.CompanyId).SingleAsync());

        await ExecuteInAdminContextAsync(async db =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            db.CompanyHistory.Add(new CompanyHistoryItem
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                TenantId = AcceptanceSettings.TenantId,
                OccurredOn = now,
                Lane = "Lead",
                EventType = "TaskCompleted",
                Summary = "Company scale assessed",
                FactKey = "company.scale",
                FactValue = "Scale band: enterprise",
                Confidence = "high",
                SourceType = "ProcessTask",
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });
            await db.SaveChangesAsync();
        });

        string html = await GetStringAsync($"/Companies/{companyId}/History");

        html.Should().Contain("Company scale assessed");
        html.Should().Contain("company.scale");
        html.Should().Contain("Scale band: enterprise");
    }

    [CRMAcceptanceFact]
    public async Task Get_Index_And_Edit_RenderSeededLead()
    {
        (Guid leadId, _) = await SeedLeadAsync();

        string indexHtml = await GetStringAsync("/Leads");
        string editHtml = await GetStringAsync($"/Leads/Edit/{leadId}");

        indexHtml.Should().Contain("Leads");
        indexHtml.Should().Contain("Lead Queue");
        indexHtml.Should().Contain("New Lead");
        indexHtml.Should().Contain("Bulk Import");
        indexHtml.Should().Contain("class=\"tree-grid__toggle\"");
        editHtml.Should().Contain("Lead Details");
    }

    [CRMAcceptanceFact]
    public async Task Get_Details_SurfacesQualificationReasonAndArtifactFeed()
    {
        (Guid leadId, _) = await SeedLeadAsync();
        await ExecuteInAdminContextAsync(async db =>
        {
            Lead lead = await db.Leads.Include(item => item.Company).SingleAsync(item => item.Id == leadId);
            lead.Status = cCoder.ClientRelationshipManagement.Platform.Models.Enums.LeadStatus.Rejected;
            lead.QualificationNotes = "Decision: rejected. Fit score 20 is below the required threshold of 60.";
            lead.Company.IsProspectingSuppressed = true;
            lead.Company.ProspectingSuppressedReason = "Commercial fit below threshold.";
            await db.SaveChangesAsync();
        });

        string html = await GetStringAsync($"/Leads/{leadId}/Details");

        html.Should().Contain("Decision &amp; evidence");
        html.Should().Contain("Fit score 20 is below the required threshold of 60.");
        html.Should().Contain("Commercial fit below threshold.");
        html.Should().Contain("Artifact feed");
    }

    [CRMAcceptanceFact]
    public async Task Get_Index_RendersDashboardLeadScopesAndTaskFilters()
    {
        string candidates = await GetStringAsync("/Leads?scope=candidates");
        string activeTasks = await GetStringAsync("/Leads?tasks=due-today");

        candidates.Should().Contain("Company Pool");
        candidates.Should().Contain("highest-ranked 100");
        activeTasks.Should().Contain("Lead Queue &#x2014; Due Today");
        activeTasks.Should().Contain("name=\"tasks\" value=\"due-today\"");
    }
}
