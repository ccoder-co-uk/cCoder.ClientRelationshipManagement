using System.Net;
using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Enums;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

[Collection(CRMAcceptanceCollection.Name)]
public sealed class ClientsPageTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    [CRMAcceptanceFact]
    public async Task Get_Index_ReturnsClientGrid()
    {
        Client client = NewClient();
        Company company = NewCompany(client.Id);
        company.Name = "Acme Holdings";

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            await dbContext.SaveChangesAsync();
        });

        string response = await GetStringAsync("/Clients");

        response.Should().Contain("Clients Page");
        response.Should().Contain("Acme Holdings");
        response.Should().Contain("/Clients/Edit/");
        response.Should().Contain("Sort By");
    }

    [CRMAcceptanceFact]
    public async Task Get_Index_CanFilterByStatus()
    {
        Client prospectClient = NewClient();
        prospectClient.Status = RelationshipStatus.Prospect;

        Client dormantClient = NewClient();
        dormantClient.Status = RelationshipStatus.Dormant;

        Company prospectCompany = NewCompany(prospectClient.Id);
        prospectCompany.Name = "Prospect Co";

        Company dormantCompany = NewCompany(dormantClient.Id);
        dormantCompany.Name = "Dormant Co";

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.AddRange(prospectClient, dormantClient);
            dbContext.Companies.AddRange(prospectCompany, dormantCompany);
            await dbContext.SaveChangesAsync();
        });

        string response = await GetStringAsync("/Clients?status=Prospect");

        response.Should().Contain("Prospect Co");
        response.Should().NotContain("Dormant Co");
    }

    [CRMAcceptanceFact]
    public async Task Post_Create_CreatesClientAndCompany()
    {
        await using CRMAcceptanceFactory factory = CreateSessionAuthFactory();
        await factory.EnsureSessionUserCanLoginAsync();
        using HttpClient client = CreateCookieClient(factory);

        using HttpResponseMessage loginResponse = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["User"] = Fixture.Settings.SessionUserEmail,
                ["Pass"] = Fixture.Settings.SessionUserPassword,
                ["ReturnUrl"] = "/Clients/Create",
            }));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync(
            client,
            "/Clients/Create",
            "/Clients/Create",
            new Dictionary<string, string>
            {
                ["CompanyName"] = "Northern Star",
                ["AccountOwner"] = "paul.ward",
                ["LeadSource"] = "Referral",
                ["InitialRoute"] = "Warm intro",
                ["OpportunitySummary"] = "Potential payments review",
                ["PreferredOpeningAngle"] = "Commercial savings",
                ["NextAction"] = "Book discovery",
                ["Status"] = "Prospect",
                ["CurrentStage"] = "Researched",
                ["Priority"] = "High"
            });

        string responseContent = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, responseContent);

        Client? createdClient = await QueryInAdminContextAsync(dbContext =>
            dbContext.Clients.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.OpportunitySummary == "Potential payments review"));

        createdClient.Should().NotBeNull();

        Company? createdCompany = await QueryInAdminContextAsync(dbContext =>
            dbContext.Companies.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.ClientId == createdClient!.Id));

        createdCompany.Should().NotBeNull();
        createdCompany!.Name.Should().Be("Northern Star");
    }

    [CRMAcceptanceFact]
    public async Task Get_Edit_ShowsOpportunityAndActivityContext()
    {
        Client client = NewClient();
        client.NextAction = "Book finance follow-up";
        client.NextActionDueOn = DateTimeOffset.UtcNow.AddDays(1);
        Company company = NewCompany(client.Id);
        company.Name = "Apex Group";

        ClientOpportunity opportunity = NewClientOpportunity(client.Id);
        opportunity.PainSummary = "Finance team needs more visibility";
        opportunity.NextAction = "Send opportunity outline";
        opportunity.NextActionDueOn = DateTimeOffset.UtcNow.AddDays(2);

        ClientActivity activity = NewClientActivity(client.Id, clientOpportunityId: opportunity.Id);
        activity.Type = ClientActivityType.Note;
        activity.Summary = "Discovery call completed";
        activity.Outcome = "Decision maker identified";

        ClientMaterial emailDraft = NewClientMaterial(client.Id);
        emailDraft.Name = "Intro email draft";
        emailDraft.Notes = "First paragraph" + Environment.NewLine + "Second paragraph";
        emailDraft.Status = ClientMaterialStatus.Draft;

        ClientActivity emailActivity = NewClientActivity(
            client.Id,
            clientOpportunityId: opportunity.Id,
            clientMaterialId: emailDraft.Id);
        emailActivity.Type = ClientActivityType.Email;
        emailActivity.Summary = "Intro email draft";
        emailActivity.Outcome = emailDraft.Notes;
        Email draftEmail = NewEmail(client.Id, clientMaterialId: emailDraft.Id);
        draftEmail.Subject = emailDraft.Name;
        draftEmail.BodyText = emailDraft.Notes;
        draftEmail.BodyHtml = emailDraft.Notes;
        draftEmail.State = EmailState.Draft;

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            dbContext.ClientOpportunities.Add(opportunity);
            dbContext.ClientMaterials.Add(emailDraft);
            dbContext.Emails.Add(draftEmail);
            dbContext.ClientActivities.AddRange(activity, emailActivity);
            await dbContext.SaveChangesAsync();
        });

        string response = await GetStringAsync($"/Clients/Edit/{client.Id}");

        response.Should().Contain("Scheduled Actions");
        response.Should().Contain("Client Opportunities");
        response.Should().Contain("Communications");
        response.Should().Contain("Client Activity");
        response.Should().Contain("Client Details");
        response.Should().Contain("Add Opportunity");
        response.Should().Contain("Add Activity");
        response.Should().Contain("Draft Email");
        response.Should().Contain("Approve Send");
        response.Should().Contain("Mark Sent");
        response.Should().Contain("View Content");
        response.Should().NotContain("New Opportunity");
        response.Should().NotContain("New Activity");
        response.Should().Contain("Book finance follow-up");
        response.Should().Contain("Send opportunity outline");
        response.Should().Contain("Intro email draft");
        response.Should().Contain("Second paragraph");
        response.Should().Contain("Finance team needs more visibility");
        response.Should().Contain("Discovery call completed");
        response.Should().Contain("Decision maker identified");
    }

    [CRMAcceptanceFact]
    public async Task Get_Edit_ScheduledActions_CollapsesExactDuplicateClientAndOpportunityTasks()
    {
        Client client = NewClient();
        client.NextAction = "Send first procurement email";
        client.NextActionDueOn = DateTimeOffset.UtcNow.Date.AddDays(1);

        Company company = NewCompany(client.Id);
        company.Name = "Deduped Co";

        ClientOpportunity opportunity = NewClientOpportunity(client.Id);
        opportunity.NextAction = "Send first procurement email";
        opportunity.NextActionDueOn = client.NextActionDueOn;
        opportunity.Type = ClientOpportunityType.SupplierPaymentReview;
        opportunity.Stage = PipelineStage.OutreachReady;

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            dbContext.ClientOpportunities.Add(opportunity);
            await dbContext.SaveChangesAsync();
        });

        string response = await GetStringAsync($"/Clients/Edit/{client.Id}");

        response.Should().Contain("Send first procurement email");
        response.Should().Contain("Supplier Payment Review | Outreach Ready");
        response.Should().NotContain(">Client</span>");
    }

    [CRMAcceptanceFact]
    public async Task Post_RecordActivity_AddsTimelineEntry()
    {
        Client client = NewClient();
        Company company = NewCompany(client.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            await dbContext.SaveChangesAsync();
        });

        await using CRMAcceptanceFactory factory = CreateSessionAuthFactory();
        await factory.EnsureSessionUserCanLoginAsync();
        using HttpClient clientHttp = CreateCookieClient(factory);

        using HttpResponseMessage loginResponse = await clientHttp.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["User"] = Fixture.Settings.SessionUserEmail,
                ["Pass"] = Fixture.Settings.SessionUserPassword,
                ["ReturnUrl"] = $"/Clients/Edit/{client.Id}",
            }));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync(
            clientHttp,
            $"/Clients/Edit/{client.Id}",
            "/Clients/RecordActivity",
            new Dictionary<string, string>
            {
                ["ClientId"] = client.Id.ToString(),
                ["Type"] = ClientActivityType.Note.ToString(),
                ["Direction"] = ClientActivityDirection.Internal.ToString(),
                ["Summary"] = "Commercial note added",
                ["Outcome"] = "Agreed to prepare next proposal version",
                ["NextAction"] = "Draft proposal revision",
            });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        ClientActivity? createdActivity = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientActivities.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.ClientId == client.Id && item.Summary == "Commercial note added"));

        createdActivity.Should().NotBeNull();
        createdActivity!.Outcome.Should().Be("Agreed to prepare next proposal version");
        createdActivity.NextAction.Should().Be("Draft proposal revision");
    }

    [CRMAcceptanceFact]
    public async Task Post_SaveEmailDraft_CreatesDraftMaterialAndLinkedActivity()
    {
        Client client = NewClient();
        Company company = NewCompany(client.Id);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            await dbContext.SaveChangesAsync();
        });

        await using CRMAcceptanceFactory factory = CreateSessionAuthFactory();
        await factory.EnsureSessionUserCanLoginAsync();
        using HttpClient clientHttp = CreateCookieClient(factory);

        using HttpResponseMessage loginResponse = await clientHttp.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["User"] = Fixture.Settings.SessionUserEmail,
                ["Pass"] = Fixture.Settings.SessionUserPassword,
                ["ReturnUrl"] = $"/Clients/Edit/{client.Id}",
            }));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync(
            clientHttp,
            $"/Clients/Edit/{client.Id}",
            "/Clients/SaveEmailDraft",
            new Dictionary<string, string>
            {
                ["ClientId"] = client.Id.ToString(),
                ["Direction"] = ClientActivityDirection.Outbound.ToString(),
                ["Subject"] = "Follow-up email",
                ["Body"] = "Line one" + Environment.NewLine + "Line two",
                ["NextAction"] = "Review before sending",
            });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        ClientMaterial? createdMaterial = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientMaterials.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.ClientId == client.Id && item.Name == "Follow-up email"));

        createdMaterial.Should().NotBeNull();
        createdMaterial!.Type.Should().Be(ClientMaterialType.Email);
        createdMaterial.Status.Should().Be(ClientMaterialStatus.Draft);
        createdMaterial.Notes.Should().Be("Line one" + Environment.NewLine + "Line two");

        ClientActivity? createdActivity = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientActivities.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.ClientMaterialId == createdMaterial.Id));

        createdActivity.Should().NotBeNull();
        createdActivity!.Type.Should().Be(ClientActivityType.Email);
        createdActivity.Summary.Should().Be("Follow-up email");
        createdActivity.Outcome.Should().Be("Line one" + Environment.NewLine + "Line two");

        Email? createdEmail = await QueryInAdminContextAsync(dbContext =>
            dbContext.Emails.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.ClientMaterialId == createdMaterial.Id));

        createdEmail.Should().NotBeNull();
        createdEmail!.Subject.Should().Be("Follow-up email");
        createdEmail.BodyText.Should().Be("Line one" + Environment.NewLine + "Line two");
        createdEmail.State.Should().Be(EmailState.Draft);
    }

    [CRMAcceptanceFact]
    public async Task Post_MarkEmailSent_UpdatesDraftStatus()
    {
        Client client = NewClient();
        Company company = NewCompany(client.Id);
        ClientMaterial material = NewClientMaterial(client.Id);
        material.Status = ClientMaterialStatus.Draft;

        ClientActivity activity = NewClientActivity(client.Id, clientMaterialId: material.Id);
        activity.Type = ClientActivityType.Email;
        Email email = NewEmail(client.Id, clientMaterialId: material.Id);
        email.State = EmailState.Draft;

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            dbContext.ClientMaterials.Add(material);
            dbContext.Emails.Add(email);
            dbContext.ClientActivities.Add(activity);
            await dbContext.SaveChangesAsync();
        });

        await using CRMAcceptanceFactory factory = CreateSessionAuthFactory();
        await factory.EnsureSessionUserCanLoginAsync();
        using HttpClient clientHttp = CreateCookieClient(factory);

        using HttpResponseMessage loginResponse = await clientHttp.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["User"] = Fixture.Settings.SessionUserEmail,
                ["Pass"] = Fixture.Settings.SessionUserPassword,
                ["ReturnUrl"] = $"/Clients/Edit/{client.Id}",
            }));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync(
            clientHttp,
            $"/Clients/Edit/{client.Id}",
            "/Clients/MarkEmailSent",
            new Dictionary<string, string>
            {
                ["ClientId"] = client.Id.ToString(),
                ["EmailId"] = email.Id.ToString(),
            });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        ClientMaterial? updatedMaterial = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientMaterials.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.Id == material.Id));

        updatedMaterial.Should().NotBeNull();
        updatedMaterial!.Status.Should().Be(ClientMaterialStatus.Sent);
        updatedMaterial.SentOn.Should().NotBeNull();

        Email? updatedEmail = await QueryInAdminContextAsync(dbContext =>
            dbContext.Emails.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.Id == email.Id));

        updatedEmail.Should().NotBeNull();
        updatedEmail!.State.Should().Be(EmailState.Sent);
        updatedEmail.SentOn.Should().NotBeNull();
    }

    [CRMAcceptanceFact]
    public async Task Post_MarkEmailSent_ClearsDuplicateScheduledActions()
    {
        Client client = NewClient();
        client.NextAction = "Send first outreach email";
        client.NextActionDueOn = DateTimeOffset.UtcNow.AddDays(1);

        Company company = NewCompany(client.Id);

        ClientOpportunity opportunity = NewClientOpportunity(client.Id);
        opportunity.NextAction = client.NextAction;
        opportunity.NextActionDueOn = client.NextActionDueOn;

        ClientMaterial material = NewClientMaterial(client.Id);
        material.Status = ClientMaterialStatus.Draft;

        ClientActivity activity = NewClientActivity(client.Id, clientOpportunityId: opportunity.Id, clientMaterialId: material.Id);
        activity.Type = ClientActivityType.Email;
        activity.NextAction = client.NextAction;
        activity.NextActionDueOn = client.NextActionDueOn;
        Email email = NewEmail(client.Id, clientMaterialId: material.Id);
        email.State = EmailState.Draft;

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            dbContext.ClientOpportunities.Add(opportunity);
            dbContext.ClientMaterials.Add(material);
            dbContext.Emails.Add(email);
            dbContext.ClientActivities.Add(activity);
            await dbContext.SaveChangesAsync();
        });

        await using CRMAcceptanceFactory factory = CreateSessionAuthFactory();
        await factory.EnsureSessionUserCanLoginAsync();
        using HttpClient clientHttp = CreateCookieClient(factory);

        using HttpResponseMessage loginResponse = await clientHttp.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["User"] = Fixture.Settings.SessionUserEmail,
                ["Pass"] = Fixture.Settings.SessionUserPassword,
                ["ReturnUrl"] = $"/Clients/Edit/{client.Id}",
            }));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync(
            clientHttp,
            $"/Clients/Edit/{client.Id}",
            "/Clients/MarkEmailSent",
            new Dictionary<string, string>
            {
                ["ClientId"] = client.Id.ToString(),
                ["EmailId"] = email.Id.ToString(),
            });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        Client? updatedClient = await QueryInAdminContextAsync(dbContext =>
            dbContext.Clients.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == client.Id));
        ClientOpportunity? updatedOpportunity = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientOpportunities.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == opportunity.Id));
        ClientActivity? updatedActivity = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientActivities.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == activity.Id));

        updatedClient!.NextAction.Should().BeNull();
        updatedClient.NextActionDueOn.Should().BeNull();
        updatedOpportunity!.NextAction.Should().BeNull();
        updatedOpportunity.NextActionDueOn.Should().BeNull();
        updatedActivity!.NextAction.Should().BeNull();
        updatedActivity.NextActionDueOn.Should().BeNull();
    }

    [CRMAcceptanceFact]
    public async Task Post_ApproveEmail_UpdatesDraftStatusToApproved()
    {
        Client client = NewClient();
        Company company = NewCompany(client.Id);
        ClientMaterial material = NewClientMaterial(client.Id);
        material.Status = ClientMaterialStatus.Draft;
        Email email = NewEmail(client.Id, clientMaterialId: material.Id);
        email.State = EmailState.Draft;

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            dbContext.ClientMaterials.Add(material);
            dbContext.Emails.Add(email);
            await dbContext.SaveChangesAsync();
        });

        await using CRMAcceptanceFactory factory = CreateSessionAuthFactory();
        await factory.EnsureSessionUserCanLoginAsync();
        using HttpClient clientHttp = CreateCookieClient(factory);

        using HttpResponseMessage loginResponse = await clientHttp.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["User"] = Fixture.Settings.SessionUserEmail,
                ["Pass"] = Fixture.Settings.SessionUserPassword,
                ["ReturnUrl"] = $"/Clients/Edit/{client.Id}",
            }));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync(
            clientHttp,
            $"/Clients/Edit/{client.Id}",
            "/Clients/ApproveEmail",
            new Dictionary<string, string>
            {
                ["ClientId"] = client.Id.ToString(),
                ["EmailId"] = email.Id.ToString(),
                ["ScheduledSendOn"] = "2026-06-18T10:00",
            });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        Email? approvedEmail = await QueryInAdminContextAsync(dbContext =>
            dbContext.Emails.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == email.Id));
        ClientMaterial? approvedMaterial = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientMaterials.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == material.Id));

        approvedEmail.Should().NotBeNull();
        approvedEmail!.State.Should().Be(EmailState.Approved);
        approvedEmail.ApprovedOn.Should().NotBeNull();
        approvedEmail.ScheduledSendTimeUtc.Should().NotBeNull();

        approvedMaterial.Should().NotBeNull();
        approvedMaterial!.Status.Should().Be(ClientMaterialStatus.Ready);
    }

    static HttpClient CreateCookieClient(CRMAcceptanceFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost"),
        });

    CRMAcceptanceFactory CreateSessionAuthFactory()
    {
        AcceptanceSettings sessionSettings = new()
        {
            CrmConnectionString = Fixture.Settings.CrmConnectionString,
            CrmAdminConnectionString = Fixture.Settings.CrmAdminConnectionString,
            SsoConnectionString = Fixture.Settings.SsoConnectionString,
            DecryptionKey = Fixture.Settings.DecryptionKey,
            UserId = Fixture.Settings.UserId,
            GrantCrmPrivileges = Fixture.Settings.GrantCrmPrivileges,
            BypassAuthentication = false,
            SessionUserEmail = Fixture.Settings.SessionUserEmail,
            SessionUserPassword = Fixture.Settings.SessionUserPassword,
        };

        return new CRMAcceptanceFactory(sessionSettings);
    }
}
