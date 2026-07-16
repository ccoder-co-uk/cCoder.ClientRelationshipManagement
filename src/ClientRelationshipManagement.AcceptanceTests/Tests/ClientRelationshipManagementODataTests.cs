using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

[Collection(CRMAcceptanceCollection.Name)]
public sealed class ClientRelationshipManagementODataTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    [CRMAcceptanceFact]
    public async Task Metadata_DescribesCanonicalModelAndConversationActions()
    {
        string metadata = await GetStringAsync("/Api/ClientRelationshipManagement/$metadata");
        metadata.Should().Contain("EntitySet Name=\"AgentMessages\"");
        metadata.Should().Contain("Action Name=\"Reply\"");
        metadata.Should().Contain("Action Name=\"Respond\"");
        metadata.Should().Contain("Action Name=\"ChangeState\"");
    }

    [CRMAcceptanceFact]
    public async Task AgentMessages_QueryAndReplyUseSameConversationSeenByUi()
    {
        Guid messageId = await SeedMessageAsync();
        using HttpRequestMessage query = await AuthorizedAsync(
            HttpMethod.Get,
            $"/Api/ClientRelationshipManagement/AgentMessages({messageId})?$expand=Entries");
        using HttpResponseMessage queryResponse = await Client.SendAsync(query);
        string queryContent = await queryResponse.Content.ReadAsStringAsync();
        queryResponse.StatusCode.Should().Be(HttpStatusCode.OK, queryContent);
        queryContent.Should().Contain(messageId.ToString());

        using HttpRequestMessage reply = await AuthorizedAsync(
            HttpMethod.Post,
            $"/Api/ClientRelationshipManagement/AgentMessages({messageId})/CRM.Reply");
        reply.Content = JsonContent.Create(new { body = "Shared API reply" });
        using HttpResponseMessage replyResponse = await Client.SendAsync(reply);
        string replyContent = await replyResponse.Content.ReadAsStringAsync();
        replyResponse.StatusCode.Should().Be(HttpStatusCode.OK, replyContent);

        AgentMessage stored = await QueryInAdminContextAsync(db => db.AgentMessages
            .AsNoTracking().Include(item => item.Entries)
            .SingleAsync(item => item.Id == messageId));
        stored.Entries.Should().ContainSingle(entry => entry.Body == "Shared API reply");

        string ui = await GetStringAsync($"/Admin/Messages/{messageId}");
        ui.Should().Contain("Shared API reply");
    }

    [CRMAcceptanceFact]
    public async Task Sources_QueryAndImportsUiUseSameDomainData()
    {
        string sourceName = Unique("Shared import source");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await ExecuteInAdminContextAsync(async db =>
        {
            db.Sources.Add(new Source
            {
                Id = Guid.NewGuid(), Name = sourceName, SourceType = SourceType.Other,
                CountryCode = "GB", CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId, CreatedOn = now, LastUpdated = now
            });
            await db.SaveChangesAsync();
        });

        using HttpRequestMessage query = await AuthorizedAsync(
            HttpMethod.Get,
            $"/Api/ClientRelationshipManagement/Sources?$filter=Name eq '{sourceName}'");
        using HttpResponseMessage response = await Client.SendAsync(query);
        string api = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, api);
        api.Should().Contain(sourceName);

        string ui = await GetStringAsync("/Admin/Imports");
        ui.Should().Contain(sourceName);
    }

    async Task<Guid> SeedMessageAsync()
    {
        Guid id = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await ExecuteInAdminContextAsync(async db =>
        {
            db.AgentMessages.Add(new AgentMessage
            {
                Id = id, TenantId = AcceptanceSettings.TenantId,
                Kind = AgentMessageKind.FeedbackRequest, State = AgentMessageState.Pending,
                Title = Unique("Shared conversation"), Body = "Can UI and agent see this?",
                CreatedBy = Fixture.Settings.UserId, LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now, LastUpdated = now
            });
            await db.SaveChangesAsync();
        });
        return id;
    }

    async Task<HttpRequestMessage> AuthorizedAsync(HttpMethod method, string path)
    {
        string token = await Fixture.IssueAgentTokenAsync();
        HttpRequestMessage request = new(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
