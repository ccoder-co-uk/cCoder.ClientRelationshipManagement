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
public sealed class EmailsPageTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    [CRMAcceptanceFact]
    public async Task Get_Index_ShowsEmailQueue()
    {
        Client client = NewClient();
        Company company = NewCompany(client.Id);
        company.Name = "Queue Co";
        ClientMaterial material = NewClientMaterial(client.Id);
        Email email = NewEmail(client.Id, clientMaterialId: material.Id);
        email.Subject = "Queue draft";
        email.BodyText = "Queued draft body";
        email.BodyHtml = "Queued draft body";
        email.State = EmailState.Draft;

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            dbContext.ClientMaterials.Add(material);
            dbContext.Emails.Add(email);
            await dbContext.SaveChangesAsync();
        });

        string response = await GetStringAsync("/Admin/Emails");

        response.Should().Contain("Emails Page");
        response.Should().Contain("Queue Co");
        response.Should().Contain("Queue draft");
        response.Should().Contain("Approve");
        response.Should().Contain("Mark Sent");
        response.Should().Contain("Showing 1–1 of 1 emails");
        response.Should().Contain("Email result pages");
    }

    [CRMAcceptanceFact]
    public async Task Get_Index_DoesNotOfferApprovalWithoutRecipient()
    {
        Client client = NewClient();
        Company company = NewCompany(client.Id);
        company.Name = "Missing Recipient Co";
        ClientMaterial material = NewClientMaterial(client.Id);
        Email email = NewEmail(client.Id, clientMaterialId: material.Id);
        email.ToAddresses = null;
        email.Subject = "Recipient-less draft";
        email.State = EmailState.Draft;

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            dbContext.Clients.Add(client);
            dbContext.Companies.Add(company);
            dbContext.ClientMaterials.Add(material);
            dbContext.Emails.Add(email);
            await dbContext.SaveChangesAsync();
        });

        string response = await GetStringAsync($"/Admin/Emails?id={email.Id}");

        response.Should().Contain("Recipient unavailable");
        response.Should().Contain("cannot be approved or sent");
        response.Should().NotContain("Approve Send");
        response.Should().NotContain("Mark Sent");
    }

    [CRMAcceptanceFact]
    public async Task Post_Approve_UpdatesEmailState()
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
                ["ReturnUrl"] = "/Admin/Emails",
            }));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync(
            clientHttp,
            "/Admin/Emails",
            "/Admin/Emails/Approve",
            new Dictionary<string, string>
            {
                ["ClientId"] = client.Id.ToString(),
                ["EmailId"] = email.Id.ToString(),
                ["ScheduledSendOn"] = "2026-06-18T08:45",
                ["ReturnUrl"] = "/Admin/Emails?search=Queue&state=Draft&page=2&pageSize=25",
            });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().Be(
            new Uri("/Admin/Emails?search=Queue&state=Draft&page=2&pageSize=25", UriKind.Relative));

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

    [CRMAcceptanceFact]
    public async Task Post_MarkSent_UpdatesEmailState()
    {
        Client client = NewClient();
        Company company = NewCompany(client.Id);
        ClientMaterial material = NewClientMaterial(client.Id);
        material.Status = ClientMaterialStatus.Ready;
        Email email = NewEmail(client.Id, clientMaterialId: material.Id);
        email.State = EmailState.Approved;

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
                ["ReturnUrl"] = "/Admin/Emails",
            }));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync(
            clientHttp,
            "/Admin/Emails",
            "/Admin/Emails/MarkSent",
            new Dictionary<string, string>
            {
                ["ClientId"] = client.Id.ToString(),
                ["EmailId"] = email.Id.ToString(),
            });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        Email? sentEmail = await QueryInAdminContextAsync(dbContext =>
            dbContext.Emails.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == email.Id));
        ClientMaterial? sentMaterial = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientMaterials.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Id == material.Id));

        sentEmail.Should().NotBeNull();
        sentEmail!.State.Should().Be(EmailState.Sent);
        sentEmail.SentOn.Should().NotBeNull();

        sentMaterial.Should().NotBeNull();
        sentMaterial!.Status.Should().Be(ClientMaterialStatus.Sent);
        sentMaterial.SentOn.Should().NotBeNull();
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
