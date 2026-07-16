using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using ClientRelationshipManagement.Web.Services.Mail;
using ClientRelationshipManagement.Web.Services.Processes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public abstract class CRMControllerAcceptanceTestBase(CRMAcceptanceFixture fixture)
{
    protected CRMAcceptanceFixture Fixture { get; } = fixture;
    protected HttpClient Client { get; } = fixture.Client;

    protected static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    protected async Task<string> GetStringAsync(string url)
    {
        using HttpResponseMessage response = await Client.GetAsync(url);
        string content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        return content;
    }

    protected static async Task<string> GetStringAsync(HttpClient client, string url)
    {
        using HttpResponseMessage response = await client.GetAsync(url);
        string content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        return content;
    }

    protected async Task<HttpResponseMessage> PostFormWithAntiforgeryAsync(
        string formUrl,
        string postUrl,
        IReadOnlyDictionary<string, string> values)
        => await PostFormWithAntiforgeryAsync(Client, formUrl, postUrl, values);

    protected static async Task<HttpResponseMessage> PostFormWithAntiforgeryAsync(
        HttpClient client,
        string formUrl,
        string postUrl,
        IReadOnlyDictionary<string, string> values)
    {
        string html = await GetStringAsync(client, formUrl);
        string token = ExtractAntiforgeryToken(html);

        Dictionary<string, string> payload = new(values)
        {
            ["__RequestVerificationToken"] = token
        };

        return await client.PostAsync(postUrl, new FormUrlEncodedContent(payload));
    }

    protected async Task<HttpResponseMessage> PostMultipartWithAntiforgeryAsync(
        string formUrl,
        string postUrl,
        Action<MultipartFormDataContent> configure)
    {
        string html = await GetStringAsync(formUrl);
        string token = ExtractAntiforgeryToken(html);

        MultipartFormDataContent payload = new();
        payload.Add(new StringContent(token), "__RequestVerificationToken");
        configure(payload);

        return await Client.PostAsync(postUrl, payload);
    }

    protected async Task ExecuteInAdminContextAsync(Func<PlatformDbContext, Task> action)
    {
        using IServiceScope scope = Fixture.Factory.Services.CreateScope();
        using PlatformDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<IPlatformDbContextFactory>().CreateDbContext(useAdminConnection: true);

        await action(dbContext);
    }

    protected async Task<TResult> QueryInAdminContextAsync<TResult>(Func<PlatformDbContext, Task<TResult>> action)
    {
        using IServiceScope scope = Fixture.Factory.Services.CreateScope();
        using PlatformDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<IPlatformDbContextFactory>().CreateDbContext(useAdminConnection: true);

        return await action(dbContext);
    }

    protected async Task ExecuteWorkflowAsync(Func<IWorkflowAutomationService, Task> action)
    {
        using IServiceScope scope = Fixture.Factory.Services.CreateScope();
        IWorkflowAutomationService service = scope.ServiceProvider.GetRequiredService<IWorkflowAutomationService>();
        await action(service);
    }

    protected async Task<TResult> ExecuteEmailDispatchAsync<TResult>(Func<IEmailDispatchProcessor, Task<TResult>> action)
    {
        using IServiceScope scope = Fixture.Factory.Services.CreateScope();
        IEmailDispatchProcessor service = scope.ServiceProvider.GetRequiredService<IEmailDispatchProcessor>();
        return await action(service);
    }

    protected async Task<(Guid LeadId, Guid ContactId)> SeedLeadAsync()
    {
        Guid leadId = Guid.NewGuid();
        Guid contactId = Guid.NewGuid();
        Guid companyId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await ExecuteInAdminContextAsync(async db =>
        {
            db.Companies.Add(new Company
            {
                Id = companyId,
                SourceSystem = "Acceptance",
                OfficialName = Unique("Lead Company"),
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });

            db.Leads.Add(new Lead
            {
                Id = leadId,
                TenantId = AcceptanceSettings.TenantId,
                SourceSystem = "Acceptance",
                Status = LeadStatus.Imported,
                RawCompanyName = Unique("Lead Company"),
                RawWebsiteUrl = "https://lead.example.com",
                CompanyId = companyId,
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });

            db.LeadContacts.Add(new LeadContact
            {
                Id = contactId,
                LeadId = leadId,
                IsPrimary = true,
                Name = Unique("Lead Contact"),
                EmailAddress = $"{Unique("lead-contact")}@example.com",
                PhoneNumber = "01234567890",
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });

            await db.SaveChangesAsync();
        });

        return (leadId, contactId);
    }

    protected async Task<(Guid RelationshipId, Guid OpportunityId, Guid CompanyContactId)> SeedOpportunityWorkspaceAsync(
        SalesPipelineStage opportunityStage = SalesPipelineStage.ContactIdentified)
    {
        Guid companyId = Guid.NewGuid();
        Guid companyContactId = Guid.NewGuid();
        Guid relationshipId = Guid.NewGuid();
        Guid relationshipContactId = Guid.NewGuid();
        Guid opportunityId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await ExecuteInAdminContextAsync(async db =>
        {
            db.Companies.Add(new Company
            {
                Id = companyId,
                SourceSystem = "Acceptance",
                IsVerified = true,
                OfficialName = Unique("Company"),
                TradingName = Unique("Trading"),
                ContactEmailAddress = "company@example.com",
                ContactPhoneNumber = "01234567890",
                WebsiteUrl = "https://company.example.com",
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });

            db.CompanyContacts.Add(new CompanyContact
            {
                Id = companyContactId,
                CompanyId = companyId,
                SourceSystem = "Acceptance",
                IsVerified = true,
                IsPrimary = true,
                Name = Unique("Company Contact"),
                Position = "Procurement Lead",
                EmailAddress = $"{Unique("contact")}@example.com",
                PhoneNumber = "01234567890",
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
                CurrentStage = opportunityStage,
                Priority = RelationshipPriority.Medium,
                LeadSource = "Acceptance",
                InitialRoute = "Warm outreach",
                OpportunitySummary = "Follow up on onboarding services",
                PreferredOpeningAngle = "Introduce cash flow optimisation",
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });

            db.RelationshipContacts.Add(new RelationshipContact
            {
                Id = relationshipContactId,
                TenantCompanyRelationshipId = relationshipId,
                CompanyContactId = companyContactId,
                Status = RelationshipContactStatus.Active,
                IsPrimary = true,
                RelationshipRoute = "Primary",
                Source = "Acceptance",
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });

            db.Opportunities.Add(new Opportunity
            {
                Id = opportunityId,
                TenantCompanyRelationshipId = relationshipId,
                PrimaryRelationshipContactId = relationshipContactId,
                Type = OpportunityType.General,
                Stage = opportunityStage,
                PainSummary = "Needs a structured outreach path.",
                ValueHypothesis = "We can reduce onboarding friction.",
                DecisionProcess = "Review and approval",
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });

            await db.SaveChangesAsync();
        });

        return (relationshipId, opportunityId, companyContactId);
    }

    protected async Task<ProcessTask> MoveOpportunityToEmailStepAsync(Guid opportunityId)
    {
        await ExecuteWorkflowAsync(service => service.EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true).AsTask());

        ProcessTask firstTask = await QueryInAdminContextAsync(db =>
            db.ProcessTasks
                .AsNoTracking()
                .Where(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending)
                .OrderBy(item => item.DueOn)
                .FirstAsync());

        await ExecuteWorkflowAsync(service => service.CompleteTaskAsync(new ProcessTaskCompletionCommand
        {
            ProcessTaskId = firstTask.Id,
            OutcomeKey = "ready",
            CompletionNote = "Route confirmed."
        }).AsTask());

        return await QueryInAdminContextAsync(db =>
            db.ProcessTasks
                .AsNoTracking()
                .Include(item => item.Email)
                .Include(item => item.ProcessStep)
                .Where(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending)
                .OrderBy(item => item.DueOn)
                .FirstAsync());
    }

    protected async Task<Guid> SeedSentOpportunityEmailAsync(
        Guid relationshipId,
        Guid opportunityId,
        Guid companyContactId,
        EmailState emailState = EmailState.Sent)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid materialId = Guid.NewGuid();
        Guid emailId = Guid.NewGuid();

        await ExecuteInAdminContextAsync(async db =>
        {
            db.Materials.Add(new Material
            {
                Id = materialId,
                TenantCompanyRelationshipId = relationshipId,
                OpportunityId = opportunityId,
                CompanyContactId = companyContactId,
                Name = "Imported intro email",
                Type = MaterialType.Email,
                Status = MaterialStatus.Sent,
                Notes = "Initial outreach already sent.",
                SentOn = now.AddDays(-1),
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now.AddDays(-1),
                LastUpdated = now.AddDays(-1)
            });

            db.Emails.Add(new Email
            {
                Id = emailId,
                TenantCompanyRelationshipId = relationshipId,
                OpportunityId = opportunityId,
                MaterialId = materialId,
                CompanyContactId = companyContactId,
                SenderUserId = Fixture.Settings.UserId,
                ToAddresses = "imported@example.com",
                Subject = "Imported outreach",
                BodyHtml = "Initial outreach already sent.",
                BodyText = "Initial outreach already sent.",
                IsBodyHtml = false,
                State = emailState,
                SentOn = now.AddDays(-1),
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now.AddDays(-1),
                LastUpdated = now.AddDays(-1)
            });

            await db.SaveChangesAsync();
        });

        return emailId;
    }

    protected AcceptanceSettings CloneSettings(bool bypassAuthentication) =>
        new()
        {
            CrmConnectionString = Fixture.Settings.CrmConnectionString,
            CrmAdminConnectionString = Fixture.Settings.CrmAdminConnectionString,
            SsoConnectionString = Fixture.Settings.SsoConnectionString,
            DecryptionKey = Fixture.Settings.DecryptionKey,
            UserId = Fixture.Settings.UserId,
            BypassAuthentication = bypassAuthentication,
            GrantCrmPrivileges = Fixture.Settings.GrantCrmPrivileges,
            SessionUserEmail = Fixture.Settings.SessionUserEmail,
            SessionUserPassword = Fixture.Settings.SessionUserPassword
        };

    protected static string ExtractAntiforgeryToken(string html)
    {
        Match match = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        match.Success.Should().BeTrue("the form should include an antiforgery token");
        return match.Groups[1].Value;
    }
}
