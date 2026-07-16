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
public sealed class ProcessPageTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    [CRMAcceptanceFact]
    public async Task Get_Index_ShowsProcessDesigner()
    {
        string response = await GetStringAsync("/Admin/Process");

        response.Should().Contain("Process Page");
        response.Should().Contain("Default Outbound Prospecting");
        response.Should().Contain("Supported Tokens");
        response.Should().Contain("Save process");
    }

    [CRMAcceptanceFact]
    public async Task Post_SaveDefinition_CreatesProcessDefinition()
    {
        await using CRMAcceptanceFactory factory = CreateSessionAuthFactory();
        await factory.EnsureSessionUserCanLoginAsync();
        using HttpClient clientHttp = CreateCookieClient(factory);

        using HttpResponseMessage loginResponse = await clientHttp.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["User"] = Fixture.Settings.SessionUserEmail,
                ["Pass"] = Fixture.Settings.SessionUserPassword,
                ["ReturnUrl"] = "/Admin/Process",
            }));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync(
            clientHttp,
            "/Admin/Process",
            "/Admin/Process/SaveDefinition",
            new Dictionary<string, string>
            {
                ["TenantId"] = "default",
                ["Name"] = "QA Process",
                ["Description"] = "Process created by acceptance test",
                ["IsActive"] = "true",
            });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        ClientProcessDefinition? createdDefinition = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientProcessDefinitions.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.Name == "QA Process"));

        createdDefinition.Should().NotBeNull();
        createdDefinition!.Description.Should().Be("Process created by acceptance test");
    }

    [CRMAcceptanceFact]
    public async Task Post_SaveStep_CreatesProcessStep()
    {
        await GetStringAsync("/Admin/Process");

        ClientProcessDefinition definition = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientProcessDefinitions.IgnoreQueryFilters()
                .OrderByDescending(item => item.IsDefault)
                .FirstAsync());

        await using CRMAcceptanceFactory factory = CreateSessionAuthFactory();
        await factory.EnsureSessionUserCanLoginAsync();
        using HttpClient clientHttp = CreateCookieClient(factory);

        using HttpResponseMessage loginResponse = await clientHttp.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["User"] = Fixture.Settings.SessionUserEmail,
                ["Pass"] = Fixture.Settings.SessionUserPassword,
                ["ReturnUrl"] = $"/Admin/Process?id={definition.Id}",
            }));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync(
            clientHttp,
            $"/Admin/Process?id={definition.Id}",
            "/Admin/Process/SaveStep",
            new Dictionary<string, string>
            {
                ["ClientProcessDefinitionId"] = definition.Id.ToString(),
                ["Key"] = "qa-step",
                ["Name"] = "QA Step",
                ["Sequence"] = "250",
                ["ActionType"] = ClientProcessActionType.ManualTask.ToString(),
                ["TaskTitleTemplate"] = "QA title",
                ["TaskInstructionsTemplate"] = "QA instructions",
                ["IsActive"] = "true",
            });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        ClientProcessStep? createdStep = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientProcessSteps.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.ClientProcessDefinitionId == definition.Id && item.Key == "qa-step"));

        createdStep.Should().NotBeNull();
        createdStep!.Name.Should().Be("QA Step");
        createdStep.TaskTitleTemplate.Should().Be("QA title");
    }

    [CRMAcceptanceFact]
    public async Task Post_SaveTransition_CreatesProcessTransition()
    {
        await GetStringAsync("/Admin/Process");

        ClientProcessDefinition definition = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientProcessDefinitions.IgnoreQueryFilters()
                .OrderByDescending(item => item.IsDefault)
                .FirstAsync());
        List<ClientProcessStep> steps = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientProcessSteps.IgnoreQueryFilters()
                .Where(item => item.ClientProcessDefinitionId == definition.Id)
                .OrderBy(item => item.Sequence)
                .Take(2)
                .ToListAsync());

        steps.Count.Should().BeGreaterThanOrEqualTo(2);

        await using CRMAcceptanceFactory factory = CreateSessionAuthFactory();
        await factory.EnsureSessionUserCanLoginAsync();
        using HttpClient clientHttp = CreateCookieClient(factory);

        using HttpResponseMessage loginResponse = await clientHttp.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["User"] = Fixture.Settings.SessionUserEmail,
                ["Pass"] = Fixture.Settings.SessionUserPassword,
                ["ReturnUrl"] = $"/Admin/Process?id={definition.Id}",
            }));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using HttpResponseMessage response = await PostFormWithAntiforgeryAsync(
            clientHttp,
            $"/Admin/Process?id={definition.Id}",
            "/Admin/Process/SaveTransition",
            new Dictionary<string, string>
            {
                ["ClientProcessStepId"] = steps[0].Id.ToString(),
                ["NextClientProcessStepId"] = steps[1].Id.ToString(),
                ["OutcomeKey"] = "qa-outcome",
                ["OutcomeLabel"] = "QA Outcome",
            });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        ClientProcessTransition? createdTransition = await QueryInAdminContextAsync(dbContext =>
            dbContext.ClientProcessTransitions.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item =>
                    item.ClientProcessStepId == steps[0].Id
                    && item.OutcomeKey == "qa-outcome"));

        createdTransition.Should().NotBeNull();
        createdTransition!.NextClientProcessStepId.Should().Be(steps[1].Id);
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
