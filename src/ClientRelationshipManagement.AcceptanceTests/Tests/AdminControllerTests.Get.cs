using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed partial class AdminControllerTests
{
    [CRMAcceptanceFact]
    public async Task Get_Index_RendersOperationalAdminDashboard()
    {
        string html = await GetStringAsync("/Admin");

        html.Should().Contain("Operational visibility for agent runs, approvals, and process improvement proposals.");
        html.Should().Contain("Pending email approvals");
        html.Should().Contain("Pending agent messages");
        html.Should().Contain("Pending process proposals");
        html.Should().Contain("Commercial workflow");
        html.Should().Contain("Approval &amp; optimisation");
        html.Should().Contain("Save routing");
        html.Should().Contain("Provider directory");
        html.Should().Contain("Local Ollama (laptop)");
        html.Should().Contain("Desktop Ollama");
        html.Should().Contain("OpenAI cloud");
        html.Should().Contain("Codex (ChatGPT subscription)");
        html.Should().Contain("Microsoft Foundry");
        html.Should().Contain("Lead generation");
        html.Should().Contain("Opportunity conversion");
        html.Should().Contain("Client maintenance");
        html.Should().Contain("None — human managed");
        html.Should().Contain("class=\"product-brand\" href=\"/\"");
        html.Should().Contain("href=\"/Admin\"");
        html.Should().Contain("href=\"/Admin/Emails\"");
        html.Should().Contain("href=\"/Admin/Imports\"");
        html.Should().Contain("href=\"/Admin/Process\"");
        html.Should().Contain("href=\"/Companies\"");
        html.IndexOf(">Agent messages</a>", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf(">Agent runs</a>", StringComparison.Ordinal));
        html.IndexOf(">Agent runs</a>", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf(">Emails</a>", StringComparison.Ordinal));
        html.IndexOf(">Emails</a>", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf(">Imports</a>", StringComparison.Ordinal));
        html.IndexOf(">Imports</a>", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf(">Process</a>", StringComparison.Ordinal));
        html.IndexOf(">Process</a>", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf(">Process proposals</a>", StringComparison.Ordinal));
        html.Should().NotContain("/Admin/Overview");
        html.Should().NotContain(">Overview</a>");
        html.Should().NotContain(">Dashboard</a>");
    }

    [CRMAcceptanceFact]
    public async Task Get_Runs_RendersAgentRunHistoryPage()
    {
        string html = await GetStringAsync("/Admin/Runs");
        html.Should().Contain("Agent runs");
        html.Should().Contain("Execution history across approval, lead, opportunity, and client automation.");
        html.Should().Contain("Rows per page");
        html.Should().Contain("aria-label=\"Agent run history pages\"");
        html.Should().Contain("agent-runs-table-shell");
    }

    [CRMAcceptanceFact]
    public async Task Get_Messages_RendersPendingAgentMessagesPage()
    {
        string html = await GetStringAsync("/Admin/Messages");
        html.Should().Contain("Agent conversations");
        html.Should().Contain("Persistent discussions about rejected work");
    }

    [CRMAcceptanceFact]
    public async Task Get_MessageUpdates_ReturnsPersistentConversationEntriesForLiveRefresh()
    {
        Guid messageId = Guid.NewGuid();
        Guid entryId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await ExecuteInAdminContextAsync(async db =>
        {
            AgentMessage message = new()
            {
                Id = messageId,
                TenantId = AcceptanceSettings.TenantId,
                Kind = AgentMessageKind.FeedbackRequest,
                State = AgentMessageState.Pending,
                CorrelationKey = $"acceptance-live-conversation:{messageId:N}",
                Title = "Live conversation test",
                Body = "Test conversation",
                AgentName = "Approval Agent",
                CreatedBy = "acceptance-test",
                LastUpdatedBy = "acceptance-test",
                CreatedOn = now,
                LastUpdated = now
            };
            message.Entries.Add(new AgentMessageEntry
            {
                Id = entryId,
                AgentMessageId = messageId,
                Role = "Agent",
                Body = "A newly persisted response.",
                CreatedBy = "acceptance-test",
                LastUpdatedBy = "acceptance-test",
                CreatedOn = now,
                LastUpdated = now
            });
            message.Entries.Add(new AgentMessageEntry
            {
                Id = Guid.NewGuid(),
                AgentMessageId = messageId,
                Role = "User",
                Body = "Please continue the review.",
                CreatedBy = "acceptance-test",
                LastUpdatedBy = "acceptance-test",
                CreatedOn = now.AddSeconds(1),
                LastUpdated = now.AddSeconds(1)
            });
            db.AgentMessages.Add(message);
            await db.SaveChangesAsync();
        });

        string html = await GetStringAsync($"/Admin/Messages/{messageId}");
        html.Should().Contain("data-conversation-live-status");
        html.Should().Contain($"data-entry-id=\"{entryId}\"");
        html.Should().Contain("conversation-sidebar__list");
        html.Should().Contain("data-agent-working");
        html.Should().NotContain("Close conversation");
        html.Should().Contain("Resolve");

        string json = await GetStringAsync($"/Admin/Messages/{messageId}/Updates");
        using JsonDocument document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("entries")[0].GetProperty("id").GetGuid().Should().Be(entryId);
        document.RootElement.GetProperty("entries")[0].GetProperty("body").GetString()
            .Should().Be("A newly persisted response.");
        document.RootElement.GetProperty("awaitingAgent").GetBoolean().Should().BeTrue();

        await ExecuteInAdminContextAsync(async db =>
        {
            AgentMessage message = await db.AgentMessages.FindAsync(messageId);
            if (message is not null)
            {
                db.AgentMessages.Remove(message);
                await db.SaveChangesAsync();
            }
        });
    }

    [CRMAcceptanceFact]
    public async Task Post_ResolveAndReopenMessage_UpdatesConversationLifecycleWithAuditEntries()
    {
        Guid messageId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await ExecuteInAdminContextAsync(async db =>
        {
            db.AgentMessages.Add(new AgentMessage
            {
                Id = messageId,
                TenantId = AcceptanceSettings.TenantId,
                Kind = AgentMessageKind.FeedbackRequest,
                State = AgentMessageState.Pending,
                CorrelationKey = $"acceptance-resolve-conversation:{messageId:N}",
                Title = "Resolve conversation test",
                Body = "Test lifecycle",
                AgentName = "Approval Agent",
                CreatedBy = "acceptance-test",
                LastUpdatedBy = "acceptance-test",
                CreatedOn = now,
                LastUpdated = now
            });
            await db.SaveChangesAsync();
        });

        using HttpResponseMessage resolved = await PostFormWithAntiforgeryAsync(
            $"/Admin/Messages/{messageId}",
            "/Admin/ResolveMessage",
            new Dictionary<string, string>
            {
                ["id"] = messageId.ToString(),
                ["returnUrl"] = $"/Admin/Messages/{messageId}"
            });
        resolved.StatusCode.Should().Be(System.Net.HttpStatusCode.Redirect);
        (await QueryInAdminContextAsync(db => db.AgentMessages
            .Where(item => item.Id == messageId)
            .Select(item => item.State)
            .SingleAsync())).Should().Be(AgentMessageState.Completed);

        using HttpResponseMessage reopened = await PostFormWithAntiforgeryAsync(
            $"/Admin/Messages/{messageId}",
            "/Admin/ReopenMessage",
            new Dictionary<string, string>
            {
                ["id"] = messageId.ToString(),
                ["returnUrl"] = $"/Admin/Messages/{messageId}"
            });
        reopened.StatusCode.Should().Be(System.Net.HttpStatusCode.Redirect);
        await ExecuteInAdminContextAsync(async db =>
        {
            AgentMessage message = await db.AgentMessages.Include(item => item.Entries).SingleAsync(item => item.Id == messageId);
            message.State.Should().Be(AgentMessageState.Pending);
            message.Entries.Should().Contain(entry => entry.Role == "System" && entry.Body.Contains("resolved by"));
            message.Entries.Should().Contain(entry => entry.Role == "System" && entry.Body.Contains("reopened by"));
            db.AgentMessages.Remove(message);
            await db.SaveChangesAsync();
        });
    }

    [CRMAcceptanceFact]
    public async Task Get_ProcessProposals_RendersDraftProcessPage()
    {
        string html = await GetStringAsync("/Admin/ProcessProposals");
        html.Should().Contain("Process proposals");
        html.Should().Contain("Draft process definitions waiting for review");
    }

    [CRMAcceptanceFact]
    public async Task Get_ProcessProposal_ShowsTargetedCurrentAndProposedValues()
    {
        Guid currentId = Guid.NewGuid(); Guid draftId = Guid.NewGuid();
        await ExecuteInAdminContextAsync(async db =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            ProcessDefinition current = new() { Id = currentId, TenantId = AcceptanceSettings.TenantId, ScopeType = ProcessScopeType.Opportunity,
                FamilyId = currentId, VersionNumber = 1, LifecycleState = ProcessDefinitionLifecycleState.Active,
                Name = "Targeted comparison", Description = "Current process", IsActive = true,
                CreatedBy = "acceptance-test", LastUpdatedBy = "acceptance-test", CreatedOn = now, LastUpdated = now };
            current.Steps.Add(new ProcessStep { Id = Guid.NewGuid(), ProcessDefinitionId = currentId, Key = "intro-email",
                Name = "Send Intro Email", Sequence = 20, ActionType = ProcessActionType.Email,
                Objective = "", RequiredFacts = "", ProducedFacts = "", ViabilityImpact = "", TaskTitleTemplate = "", TaskInstructionsTemplate = "",
                EmailSubjectTemplate = "", EmailBodyTemplate = "Current email body", CallScriptTemplate = "", QuestionSetTemplate = "",
                CreatedBy = "acceptance-test", LastUpdatedBy = "acceptance-test", CreatedOn = now, LastUpdated = now });
            ProcessDefinition draft = new() { Id = draftId, TenantId = AcceptanceSettings.TenantId, ScopeType = ProcessScopeType.Opportunity,
                FamilyId = currentId, SupersedesProcessDefinitionId = currentId, VersionNumber = 2, LifecycleState = ProcessDefinitionLifecycleState.Draft,
                Name = "Targeted comparison", Description = "Current process", ChangeSummary = "Make the outreach recipient-ready.",
                CreatedBy = "acceptance-test", LastUpdatedBy = "acceptance-test", CreatedOn = now, LastUpdated = now };
            draft.Steps.Add(new ProcessStep { Id = Guid.NewGuid(), ProcessDefinitionId = draftId, Key = "intro-email",
                Name = "Send Intro Email", Sequence = 20, ActionType = ProcessActionType.Email,
                Objective = "", RequiredFacts = "", ProducedFacts = "", ViabilityImpact = "", TaskTitleTemplate = "", TaskInstructionsTemplate = "",
                EmailSubjectTemplate = "", EmailBodyTemplate = "Proposed email body", CallScriptTemplate = "", QuestionSetTemplate = "",
                CreatedBy = "acceptance-test", LastUpdatedBy = "acceptance-test", CreatedOn = now, LastUpdated = now });
            db.ProcessDefinitions.AddRange(current, draft); await db.SaveChangesAsync();
        });

        string html = await GetStringAsync($"/Admin/ProcessProposals/{draftId}");
        html.Should().Contain("Where the change sits");
        html.Should().Contain("Send Intro Email");
        html.Should().Contain("Email body");
        html.Should().Contain("Current email body");
        html.Should().Contain("Proposed email body");
        html.Should().Contain("workflow routing");

        await ExecuteInAdminContextAsync(async db =>
        {
            db.ProcessSteps.RemoveRange(db.ProcessSteps.Where(item => item.ProcessDefinitionId == currentId || item.ProcessDefinitionId == draftId));
            await db.SaveChangesAsync();
            db.ProcessDefinitions.RemoveRange(db.ProcessDefinitions.Where(item => item.Id == currentId || item.Id == draftId));
            await db.SaveChangesAsync();
        });
    }
}
