using System.Net;
using System.Text;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class LeadsControllerTests
{
    [CRMAcceptanceFact]
    public async Task Post_Create_CreatesLeadAndWorkflowTask()
    {
        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync("/Leads", "/Leads/Create", new Dictionary<string, string>
        {
            ["TenantId"] = AcceptanceSettings.TenantId,
            ["SourceSystem"] = "Manual",
            ["RawCompanyName"] = "Created Lead Co",
            ["RawWebsiteUrl"] = "https://created-lead.example.com",
            ["ContactName"] = "Lead Contact",
            ["ContactEmailAddress"] = "lead.contact@example.com"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        Lead createdLead = await QueryInAdminContextAsync(db =>
            db.Leads.OrderByDescending(item => item.CreatedOn).FirstAsync(item => item.RawCompanyName == "Created Lead Co"));

        ProcessTask createdTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks.FirstAsync(item => item.LeadId == createdLead.Id && item.State == ProcessTaskState.Pending));

        createdTask.RenderedTitle.Should().NotBeNullOrWhiteSpace();
    }

    [CRMAcceptanceFact]
    public async Task Post_Import_RedirectsToHostedImportWorkflow()
    {
        using HttpResponseMessage response = await PostMultipartWithAntiforgeryAsync("/Leads", "/Leads/Import", content =>
        {
            content.Add(new StringContent(AcceptanceSettings.TenantId), "tenantId");
            content.Add(new StringContent("Bulk Import"), "sourceSystem");
            content.Add(
                new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("RawCompanyName,ContactName,ContactEmailAddress\nBulk Lead,Bulk Contact,bulk@example.com\n"))),
                "file",
                "leads.csv");
        });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.Should().Be("/Imports");
    }

    [CRMAcceptanceFact]
    public async Task Post_Edit_UpdatesLeadAndContact()
    {
        (Guid leadId, Guid contactId) = await SeedLeadAsync();

        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync($"/Leads/Edit/{leadId}", "/Leads/Edit", new Dictionary<string, string>
        {
            ["Id"] = leadId.ToString(),
            ["TenantId"] = AcceptanceSettings.TenantId,
            ["SourceSystem"] = "Updated Source",
            ["Status"] = LeadStatus.Researching.ToString(),
            ["RawCompanyName"] = "Updated Lead Co",
            ["QualificationNotes"] = "Checked and progressed",
            ["ContactName"] = "Updated Contact",
            ["ContactEmailAddress"] = "updated.contact@example.com"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        Lead lead = await QueryInAdminContextAsync(db => db.Leads.FirstAsync(item => item.Id == leadId));
        LeadContact contact = await QueryInAdminContextAsync(db => db.LeadContacts.FirstAsync(item => item.Id == contactId));

        lead.RawCompanyName.Should().Be("Updated Lead Co");
        lead.Status.Should().Be(LeadStatus.Researching);
        contact.Name.Should().Be("Updated Contact");
        contact.EmailAddress.Should().Be("updated.contact@example.com");
    }
}
