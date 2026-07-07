using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Enums;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

[Collection(CRMAcceptanceCollection.Name)]
public sealed class HomeControllerTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    [CRMAcceptanceFact]
    public async Task Get_Index_ReturnsDashboard()
    {
        string response = await GetStringAsync("/");

        response.Should().Contain("Action Board");
        response.Should().Contain("Total Clients");
        response.Should().Contain("/Clients");
        response.Should().Contain("/Documentation");
        response.Should().Contain("Signed In As");
        response.Should().Contain("CRM Acceptance User");
        response.Should().NotContain("/Api/Client");
        response.Should().NotContain("/Api/Company");
    }

    [CRMAcceptanceFact]
    public async Task Get_Index_WhenUnauthenticated_RedirectsToLogin()
    {
        AcceptanceSettings unauthenticatedSettings = new()
        {
            CrmConnectionString = Fixture.Settings.CrmConnectionString,
            CrmAdminConnectionString = Fixture.Settings.CrmAdminConnectionString,
            SsoConnectionString = Fixture.Settings.SsoConnectionString,
            DecryptionKey = Fixture.Settings.DecryptionKey,
            UserId = Fixture.Settings.UserId,
            GrantCrmPrivileges = Fixture.Settings.GrantCrmPrivileges,
            BypassAuthentication = false,
        };

        await using var unauthenticatedFactory = new CRMAcceptanceFactory(unauthenticatedSettings);
        using HttpClient unauthenticatedClient = unauthenticatedFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using HttpResponseMessage response = await unauthenticatedClient.GetAsync("/");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.OriginalString.Should().Be("/Account/Login?returnUrl=%2F");
    }

    [CRMAcceptanceFact]
    public async Task Get_Index_ShowsTopLevelStatsAndTopFiveActions()
    {
        Client clientA = NewClient();
        clientA.Status = RelationshipStatus.Prospect;
        clientA.NextAction = "Call finance lead";
        clientA.NextActionDueOn = DateTimeOffset.UtcNow.AddDays(-10);

        Client clientB = NewClient();
        clientB.Status = RelationshipStatus.Client;
        clientB.NextAction = "Review contract";
        clientB.NextActionDueOn = DateTimeOffset.UtcNow.AddDays(-9);

        Client clientC = NewClient();
        clientC.Status = RelationshipStatus.Client;
        clientC.NextAction = "Prepare onboarding notes";
        clientC.NextActionDueOn = DateTimeOffset.UtcNow.AddDays(-8);

        Client clientD = NewClient();
        clientD.Status = RelationshipStatus.Dormant;
        clientD.NextAction = "Restart contact";
        clientD.NextActionDueOn = DateTimeOffset.UtcNow.AddDays(-7);

        Client clientE = NewClient();
        clientE.Status = RelationshipStatus.ActiveOpportunity;
        clientE.NextAction = "Send proposal";
        clientE.NextActionDueOn = DateTimeOffset.UtcNow.AddDays(-6);

        Client clientF = NewClient();
        clientF.Status = RelationshipStatus.Onboarding;
        clientF.NextAction = "Internal kick-off";
        clientF.NextActionDueOn = DateTimeOffset.UtcNow.AddDays(-5);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.AddRange(clientA, clientB, clientC, clientD, clientE, clientF);
            dbContext.Companies.AddRange(
                NewCompany(clientA.Id),
                NewCompany(clientB.Id),
                NewCompany(clientC.Id),
                NewCompany(clientD.Id),
                NewCompany(clientE.Id),
                NewCompany(clientF.Id));
            await dbContext.SaveChangesAsync();
        });

        string response = await GetStringAsync("/");

        response.Should().Contain("Top 5 scheduled actions");
        response.Should().Contain("Total Clients");
        response.Should().Contain("Prospect");
        response.Should().Contain("Active Opportunity");
        response.Should().Contain("Onboarding");
        response.Should().Contain("Call finance lead");
        response.Should().NotContain("Internal kick-off");
        response.Should().Contain("more queued");
        response.Should().Contain("/Clients?status=Prospect");
        response.Should().Contain("View client");
        response.Should().Contain("Record Progress");
    }

    [CRMAcceptanceFact]
    public async Task Get_Index_GeneratesProcessTaskForClientMissingNextAction()
    {
        Client client = NewClient();
        client.CurrentStage = PipelineStage.Researched;
        client.NextAction = null;
        client.NextActionDueOn = null;

        Company company = NewCompany(client.Id);
        company.Name = "Process Driven Co";

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            await dbContext.SaveChangesAsync();
        });

        string response = await GetStringAsync("/");

        response.Should().Contain("Process Driven Co");
        response.Should().Contain("Review the company background");

        Client? updatedClient = await QueryInAdminContextAsync(dbContext =>
            dbContext.Clients.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == client.Id));
        ClientProcessTask? processTask = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientProcessTasks.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.ClientId == client.Id && item.State == ClientProcessTaskState.Pending));

        updatedClient.Should().NotBeNull();
        updatedClient!.NextAction.Should().NotBeNullOrWhiteSpace();
        updatedClient.NextActionDueOn.Should().NotBeNull();
        processTask.Should().NotBeNull();
        processTask!.RenderedTitle.Should().Be(updatedClient.NextAction);
    }

    [CRMAcceptanceFact]
    public async Task Post_CompleteTodo_ProcessTask_AdvancesToNextTask()
    {
        Client client = NewClient();
        client.CurrentStage = PipelineStage.Researched;
        client.NextAction = null;
        client.NextActionDueOn = null;

        Company company = NewCompany(client.Id);
        company.Name = "Flow Progress Co";

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            await dbContext.SaveChangesAsync();
        });

        await GetStringAsync("/");

        ClientProcessTask? startingTask = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientProcessTasks.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.ClientId == client.Id && item.State == ClientProcessTaskState.Pending));

        startingTask.Should().NotBeNull();
        startingTask!.RenderedTitle.Should().Contain("Flow Progress Co");

        using HttpResponseMessage response = await Client.PostAsync(
            "/Home/CompleteTodo",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = startingTask.Id.ToString(),
                ["SourceType"] = "process",
                ["CompletionNote"] = "Research completed and ready to progress.",
            }));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Redirect);

        ClientProcessTask? completedTask = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientProcessTasks.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.Id == startingTask.Id));
        ClientProcessTask? nextTask = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientProcessTasks.IgnoreQueryFilters()
                .Where(item => item.ClientId == client.Id && item.State == ClientProcessTaskState.Pending)
                .OrderBy(item => item.CreatedOn)
                .FirstOrDefaultAsync());
        Client? updatedClient = await QueryInAdminContextAsync(dbContext =>
            dbContext.Clients.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == client.Id));

        completedTask.Should().NotBeNull();
        completedTask!.State.Should().Be(ClientProcessTaskState.Completed);
        completedTask.CompletionNotes.Should().Be("Research completed and ready to progress.");

        nextTask.Should().NotBeNull();
        nextTask!.RenderedTitle.Should().Contain("Identify the best initial contact");
        nextTask.ActionType.Should().Be(ClientProcessActionType.ManualTask);

        updatedClient.Should().NotBeNull();
        updatedClient!.CurrentStage.Should().Be(PipelineStage.ContactIdentified);
        updatedClient.NextAction.Should().Be(nextTask.RenderedTitle);
        updatedClient.NextActionDueOn.Should().Be(nextTask.DueOn);
    }

    [CRMAcceptanceFact]
    public async Task Post_CompleteTodo_ProcessFlow_CreatesDraftEmailWhenReachingEmailStep()
    {
        Client client = NewClient();
        client.CurrentStage = PipelineStage.Researched;
        client.NextAction = null;
        client.NextActionDueOn = null;

        Company company = NewCompany(client.Id);
        company.Name = "Email Flow Co";
        company.ContactEmailAddress = "ops@email-flow.example.com";

        ClientContact contact = NewClientContact(client.Id);
        contact.Name = "Jordan Example";
        contact.EmailAddress = "jordan.example@email-flow.example.com";
        contact.IsPrimary = true;

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            dbContext.ClientContacts.Add(contact);
            await dbContext.SaveChangesAsync();
        });

        await GetStringAsync("/");

        ClientProcessTask? researchTask = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientProcessTasks.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.ClientId == client.Id && item.State == ClientProcessTaskState.Pending));

        researchTask.Should().NotBeNull();

        using HttpResponseMessage completeResearchResponse = await Client.PostAsync(
            "/Home/CompleteTodo",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = researchTask!.Id.ToString(),
                ["SourceType"] = "process",
                ["CompletionNote"] = "Research completed.",
            }));

        completeResearchResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Redirect);

        ClientProcessTask? identifyContactTask = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientProcessTasks.IgnoreQueryFilters()
                .Where(item => item.ClientId == client.Id && item.State == ClientProcessTaskState.Pending)
                .OrderBy(item => item.CreatedOn)
                .FirstOrDefaultAsync());

        identifyContactTask.Should().NotBeNull();

        using HttpResponseMessage completeIdentifyResponse = await Client.PostAsync(
            "/Home/CompleteTodo",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = identifyContactTask!.Id.ToString(),
                ["SourceType"] = "process",
                ["CompletionNote"] = "Primary contact identified.",
            }));

        completeIdentifyResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Redirect);

        ClientProcessTask? emailTask = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientProcessTasks.IgnoreQueryFilters()
                .Where(item => item.ClientId == client.Id && item.State == ClientProcessTaskState.Pending)
                .OrderBy(item => item.CreatedOn)
                .FirstOrDefaultAsync());

        emailTask.Should().NotBeNull();
        emailTask!.ActionType.Should().Be(ClientProcessActionType.Email);
        emailTask.EmailId.Should().NotBeNull();
        emailTask.RenderedEmailSubject.Should().NotBeNullOrWhiteSpace();
        emailTask.RenderedEmailBody.Should().Contain("Jordan Example");

        Email? createdDraft = await QueryInAdminContextAsync(dbContext =>
            dbContext.Emails.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.Id == emailTask.EmailId!.Value));

        createdDraft.Should().NotBeNull();
        createdDraft!.State.Should().Be(EmailState.Draft);
        createdDraft.ToAddresses.Should().Be("jordan.example@email-flow.example.com");
        createdDraft.Subject.Should().Be(emailTask.RenderedEmailSubject);
        createdDraft.BodyText.Should().Be(emailTask.RenderedEmailBody);
        createdDraft.ScheduledSendTimeUtc.Should().Be(emailTask.DueOn);

        string response = await GetStringAsync("/");

        response.Should().Contain("Email Flow Co");
        response.Should().Contain("Draft email attached");
        response.Should().Contain("Review Draft");
    }

    [CRMAcceptanceFact]
    public async Task Post_ConfirmDraftEmailSent_ForProcessTask_AdvancesTheProcess()
    {
        Client client = NewClient();
        client.CurrentStage = PipelineStage.OutreachReady;
        client.NextAction = null;
        client.NextActionDueOn = null;

        Company company = NewCompany(client.Id);
        company.Name = "Sent Draft Co";
        company.ContactEmailAddress = "team@sent-draft.example.com";

        ClientContact contact = NewClientContact(client.Id);
        contact.Name = "Morgan Buyer";
        contact.EmailAddress = "morgan.buyer@sent-draft.example.com";
        contact.IsPrimary = true;

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            dbContext.ClientContacts.Add(contact);
            await dbContext.SaveChangesAsync();
        });

        await GetStringAsync("/");

        ClientProcessTask? emailTask = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientProcessTasks.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.ClientId == client.Id && item.State == ClientProcessTaskState.Pending));

        emailTask.Should().NotBeNull();
        emailTask!.ActionType.Should().Be(ClientProcessActionType.Email);
        emailTask.EmailId.Should().NotBeNull();

        using HttpResponseMessage response = await Client.PostAsync(
            "/Home/ConfirmDraftEmailSent",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["ClientId"] = client.Id.ToString(),
                ["Id"] = emailTask.Id.ToString(),
                ["SourceType"] = "process",
                ["EmailId"] = emailTask.EmailId!.Value.ToString(),
            }));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Redirect);

        ClientProcessTask? completedEmailTask = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientProcessTasks.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.Id == emailTask.Id));
        ClientProcessTask? followUpTask = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientProcessTasks.IgnoreQueryFilters()
                .Where(item => item.ClientId == client.Id && item.State == ClientProcessTaskState.Pending)
                .OrderBy(item => item.CreatedOn)
                .FirstOrDefaultAsync());
        Email? sentEmail = await QueryInAdminContextAsync(dbContext =>
            dbContext.Emails.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.Id == emailTask.EmailId!.Value));
        Client? updatedClient = await QueryInAdminContextAsync(dbContext =>
            dbContext.Clients.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == client.Id));

        completedEmailTask.Should().NotBeNull();
        completedEmailTask!.State.Should().Be(ClientProcessTaskState.Completed);
        completedEmailTask.CompletionOutcomeKey.Should().Be("sent");

        sentEmail.Should().NotBeNull();
        sentEmail!.State.Should().Be(EmailState.Sent);
        sentEmail.SentOn.Should().NotBeNull();

        followUpTask.Should().NotBeNull();
        followUpTask!.RenderedTitle.Should().Contain("Check whether Sent Draft Co has replied");
        followUpTask.ActionType.Should().Be(ClientProcessActionType.ManualTask);

        updatedClient.Should().NotBeNull();
        updatedClient!.CurrentStage.Should().Be(PipelineStage.OutreachSent);
        updatedClient.NextAction.Should().Be(followUpTask.RenderedTitle);
        updatedClient.NextActionDueOn.Should().Be(followUpTask.DueOn);
    }

    [CRMAcceptanceFact]
    public async Task Post_CompleteTodo_ClearsScheduledAction()
    {
        Client client = NewClient();
        client.NextAction = "Book follow-up";
        client.NextActionDueOn = DateTimeOffset.UtcNow.AddDays(1);

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(NewCompany(client.Id));
            await dbContext.SaveChangesAsync();
        });

        using HttpResponseMessage response = await Client.PostAsync(
            "/Home/CompleteTodo",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = client.Id.ToString(),
                ["SourceType"] = "client",
                ["CompletionNote"] = "Follow-up completed and logged.",
            }));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Redirect);

        Client? updatedClient = await QueryInAdminContextAsync(dbContext =>
            dbContext.Clients.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == client.Id));

        updatedClient.Should().NotBeNull();
        updatedClient!.NextAction.Should().BeNull();
        updatedClient.NextActionDueOn.Should().BeNull();

        ClientActivity? createdActivity = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientActivities.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.ClientId == client.Id && item.Outcome == "Follow-up completed and logged."));

        createdActivity.Should().NotBeNull();
        createdActivity!.Summary.Should().Contain("Completed action");
    }

    [CRMAcceptanceFact]
    public async Task Get_Index_CollapsesExactDuplicateTasks_AndShowsAttachedDraftEmail()
    {
        Client client = NewClient();
        client.NextAction = "Review and approve first outreach";
        client.NextActionDueOn = DateTimeOffset.UtcNow.Date.AddDays(-45);

        Company company = NewCompany(client.Id);
        company.Name = "Draft Ready Co";

        ClientOpportunity opportunity = NewClientOpportunity(client.Id);
        opportunity.Type = ClientOpportunityType.SupplierPaymentReview;
        opportunity.Stage = PipelineStage.OutreachReady;
        opportunity.NextAction = client.NextAction;
        opportunity.NextActionDueOn = client.NextActionDueOn;

        ClientMaterial draftMaterial = NewClientMaterial(client.Id);
        draftMaterial.Name = "Possible supplier payment review at Draft Ready Co";
        draftMaterial.Notes = "Good morning Ms Example," + Environment.NewLine + Environment.NewLine + "Draft body";

        ClientActivity draftActivity = NewClientActivity(
            client.Id,
            clientOpportunityId: opportunity.Id,
            clientMaterialId: draftMaterial.Id);
        draftActivity.Type = ClientActivityType.Email;
        draftActivity.Summary = draftMaterial.Name;
        draftActivity.Outcome = draftMaterial.Notes;
        draftActivity.NextAction = client.NextAction;
        draftActivity.NextActionDueOn = client.NextActionDueOn;
        Email draftEmail = NewEmail(client.Id, clientMaterialId: draftMaterial.Id);
        draftEmail.Subject = draftMaterial.Name;
        draftEmail.BodyText = draftMaterial.Notes;
        draftEmail.BodyHtml = draftMaterial.Notes;
        draftEmail.State = EmailState.Draft;

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            dbContext.ClientOpportunities.Add(opportunity);
            dbContext.ClientMaterials.Add(draftMaterial);
            dbContext.Emails.Add(draftEmail);
            dbContext.ClientActivities.Add(draftActivity);
            await dbContext.SaveChangesAsync();
        });

        string response = await GetStringAsync("/");

        response.Split("Review and approve first outreach").Length.Should().Be(2);
        response.Should().Contain("Review Draft");
        response.Should().Contain("Draft email attached");
        response.Should().Contain("Possible supplier payment review at Draft Ready Co");
    }

    [CRMAcceptanceFact]
    public async Task Post_ConfirmDraftEmailSent_MarksMaterialSent_AndClearsDuplicateTasks()
    {
        Client client = NewClient();
        client.NextAction = "Send first outreach email";
        client.NextActionDueOn = DateTimeOffset.UtcNow.AddDays(1);

        Company company = NewCompany(client.Id);

        ClientOpportunity opportunity = NewClientOpportunity(client.Id);
        opportunity.NextAction = client.NextAction;
        opportunity.NextActionDueOn = client.NextActionDueOn;

        ClientMaterial draftMaterial = NewClientMaterial(client.Id);
        draftMaterial.Name = "Outreach draft";
        draftMaterial.Notes = "Email body";
        draftMaterial.Status = ClientMaterialStatus.Draft;

        ClientActivity draftActivity = NewClientActivity(
            client.Id,
            clientOpportunityId: opportunity.Id,
            clientMaterialId: draftMaterial.Id);
        draftActivity.Type = ClientActivityType.Email;
        draftActivity.Summary = draftMaterial.Name;
        draftActivity.Outcome = draftMaterial.Notes;
        draftActivity.NextAction = client.NextAction;
        draftActivity.NextActionDueOn = client.NextActionDueOn;
        Email draftEmail = NewEmail(client.Id, clientMaterialId: draftMaterial.Id);
        draftEmail.Subject = draftMaterial.Name;
        draftEmail.BodyText = draftMaterial.Notes;
        draftEmail.BodyHtml = draftMaterial.Notes;
        draftEmail.State = EmailState.Draft;

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            dbContext.ClientOpportunities.Add(opportunity);
            dbContext.ClientMaterials.Add(draftMaterial);
            dbContext.Emails.Add(draftEmail);
            dbContext.ClientActivities.Add(draftActivity);
            await dbContext.SaveChangesAsync();
        });

        using HttpResponseMessage response = await Client.PostAsync(
            "/Home/ConfirmDraftEmailSent",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["ClientId"] = client.Id.ToString(),
                ["Id"] = opportunity.Id.ToString(),
                ["SourceType"] = "opportunity",
                ["EmailId"] = draftEmail.Id.ToString(),
            }));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Redirect);

        ClientMaterial? updatedMaterial = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientMaterials.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == draftMaterial.Id));

        updatedMaterial.Should().NotBeNull();
        updatedMaterial!.Status.Should().Be(ClientMaterialStatus.Sent);
        updatedMaterial.SentOn.Should().NotBeNull();

        Email? updatedEmail = await QueryInAdminContextAsync(dbContext =>
            dbContext.Emails.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == draftEmail.Id));

        updatedEmail.Should().NotBeNull();
        updatedEmail!.State.Should().Be(EmailState.Sent);
        updatedEmail.SentOn.Should().NotBeNull();

        Client? updatedClient = await QueryInAdminContextAsync(dbContext =>
            dbContext.Clients.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == client.Id));
        ClientOpportunity? updatedOpportunity = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientOpportunities.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == opportunity.Id));
        ClientActivity? updatedActivity = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientActivities.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == draftActivity.Id));

        updatedClient!.NextAction.Should().BeNull();
        updatedClient.NextActionDueOn.Should().BeNull();
        updatedOpportunity!.NextAction.Should().BeNull();
        updatedOpportunity.NextActionDueOn.Should().BeNull();
        updatedActivity!.NextAction.Should().BeNull();
        updatedActivity.NextActionDueOn.Should().BeNull();
    }

    [CRMAcceptanceFact]
    public async Task Post_SaveDraftEmail_UpdatesAttachedDraft()
    {
        Client client = NewClient();
        client.NextAction = "Review first outreach";
        client.NextActionDueOn = DateTimeOffset.UtcNow.AddDays(1);

        Company company = NewCompany(client.Id);

        ClientMaterial draftMaterial = NewClientMaterial(client.Id);
        draftMaterial.Name = "Original draft";
        draftMaterial.Notes = "Original body";

        ClientActivity draftActivity = NewClientActivity(client.Id, clientMaterialId: draftMaterial.Id);
        draftActivity.Type = ClientActivityType.Email;
        draftActivity.Summary = draftMaterial.Name;
        draftActivity.Outcome = draftMaterial.Notes;
        draftActivity.NextAction = client.NextAction;
        draftActivity.NextActionDueOn = client.NextActionDueOn;
        Email draftEmail = NewEmail(client.Id, clientMaterialId: draftMaterial.Id);
        draftEmail.Subject = draftMaterial.Name;
        draftEmail.BodyText = draftMaterial.Notes;
        draftEmail.BodyHtml = draftMaterial.Notes;
        draftEmail.State = EmailState.Draft;

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            dbContext.ClientMaterials.Add(draftMaterial);
            dbContext.Emails.Add(draftEmail);
            dbContext.ClientActivities.Add(draftActivity);
            await dbContext.SaveChangesAsync();
        });

        using HttpResponseMessage response = await Client.PostAsync(
            "/Home/SaveDraftEmail",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["ClientId"] = client.Id.ToString(),
                ["Id"] = client.Id.ToString(),
                ["SourceType"] = "client",
                ["EmailId"] = draftEmail.Id.ToString(),
                ["ClientMaterialId"] = draftMaterial.Id.ToString(),
                ["Direction"] = ClientActivityDirection.Outbound.ToString(),
                ["ToAddresses"] = "buyer@example.com",
                ["CcAddresses"] = "sales@example.com",
                ["BccAddresses"] = "audit@example.com",
                ["Subject"] = "Updated draft subject",
                ["Body"] = "Updated draft body",
            }));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Redirect);

        ClientMaterial? updatedMaterial = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientMaterials.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == draftMaterial.Id));

        updatedMaterial.Should().NotBeNull();
        updatedMaterial!.Name.Should().Be("Updated draft subject");
        updatedMaterial.Notes.Should().Be("Updated draft body");
        updatedMaterial.Status.Should().Be(ClientMaterialStatus.Draft);

        ClientActivity? updatedActivity = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientActivities.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == draftActivity.Id));

        updatedActivity.Should().NotBeNull();
        updatedActivity!.Summary.Should().Be("Updated draft subject");
        updatedActivity.Outcome.Should().Be("Updated draft body");

        Email? updatedEmail = await QueryInAdminContextAsync(dbContext =>
            dbContext.Emails.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == draftEmail.Id));

        updatedEmail.Should().NotBeNull();
        updatedEmail!.Subject.Should().Be("Updated draft subject");
        updatedEmail.BodyText.Should().Be("Updated draft body");
        updatedEmail.ToAddresses.Should().Be("buyer@example.com");
        updatedEmail.CcAddresses.Should().Be("sales@example.com");
        updatedEmail.BccAddresses.Should().Be("audit@example.com");
        updatedEmail.State.Should().Be(EmailState.Draft);
    }

    [CRMAcceptanceFact]
    public async Task Post_ApproveDraftEmail_ApprovesAttachedDraft()
    {
        Client client = NewClient();
        Company company = NewCompany(client.Id);
        ClientMaterial draftMaterial = NewClientMaterial(client.Id);
        draftMaterial.Status = ClientMaterialStatus.Draft;
        Email draftEmail = NewEmail(client.Id, clientMaterialId: draftMaterial.Id);
        draftEmail.State = EmailState.Draft;

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            dbContext.ClientMaterials.Add(draftMaterial);
            dbContext.Emails.Add(draftEmail);
            await dbContext.SaveChangesAsync();
        });

        using HttpResponseMessage response = await Client.PostAsync(
            "/Home/ApproveDraftEmail",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["ClientId"] = client.Id.ToString(),
                ["EmailId"] = draftEmail.Id.ToString(),
                ["ScheduledSendOn"] = "2026-06-18T09:30",
            }));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Redirect);

        Email? approvedEmail = await QueryInAdminContextAsync(dbContext =>
            dbContext.Emails.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == draftEmail.Id));
        ClientMaterial? approvedMaterial = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientMaterials.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == draftMaterial.Id));

        approvedEmail.Should().NotBeNull();
        approvedEmail!.State.Should().Be(EmailState.Approved);
        approvedEmail.ApprovedOn.Should().NotBeNull();
        approvedEmail.ScheduledSendTimeUtc.Should().NotBeNull();

        approvedMaterial.Should().NotBeNull();
        approvedMaterial!.Status.Should().Be(ClientMaterialStatus.Ready);
    }
}
