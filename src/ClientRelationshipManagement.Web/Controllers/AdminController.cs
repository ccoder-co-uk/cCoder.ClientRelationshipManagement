using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Models.Security;
using ClientRelationshipManagement.Web.Models.Admin;
using ClientRelationshipManagement.Web.Services.Agents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Controllers;

public sealed class AdminController(
    IPlatformDbContextFactory dbContextFactory,
    IAgentMessageService agentMessageService,
    IProcessDraftService processDraftService,
    ICRMAuthInfo authInfo)
    : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);

        List<AgentRun> recentRuns = await context.AgentRuns
            .AsNoTracking()
            .OrderByDescending(item => item.StartedOn)
            .Take(20)
            .ToListAsync();

        List<AgentMessage> pendingMessages = await context.AgentMessages
            .AsNoTracking()
            .Where(item => item.State == AgentMessageState.Pending)
            .OrderBy(item => item.CreatedOn)
            .Take(50)
            .ToListAsync();

        List<ProcessDefinition> draftProcesses = await context.ProcessDefinitions
            .AsNoTracking()
            .Where(item => item.LifecycleState == ProcessDefinitionLifecycleState.Draft)
            .OrderByDescending(item => item.CreatedOn)
            .Take(20)
            .ToListAsync();

        int pendingEmailApprovalCount = await context.Emails.CountAsync(item => item.State == EmailState.Draft);

        return View(new AdminDashboardViewModel
        {
            Notice = TempData["AdminNotice"]?.ToString() ?? string.Empty,
            PendingEmailApprovalCount = pendingEmailApprovalCount,
            PendingAgentMessageCount = pendingMessages.Count,
            PendingProcessProposalCount = pendingMessages.Count(item => item.Kind == AgentMessageKind.ProcessProposal),
            RecentRuns =
            [
                .. recentRuns.Select(item => new AdminAgentRunViewModel
                {
                    Id = item.Id,
                    Kind = item.Kind.ToString(),
                    State = item.State.ToString(),
                    ExecutionUserId = item.ExecutionUserId,
                    Provider = item.Provider,
                    Model = item.Model,
                    Iterations = item.Iterations,
                    ProcessedItemCount = item.ProcessedItemCount,
                    StartedOn = item.StartedOn.LocalDateTime.ToString("dd MMM yyyy HH:mm"),
                    CompletedOn = item.CompletedOn?.LocalDateTime.ToString("dd MMM yyyy HH:mm") ?? string.Empty,
                    Summary = item.Summary ?? string.Empty,
                    ErrorMessage = item.ErrorMessage ?? string.Empty
                })
            ],
            PendingMessages =
            [
                .. pendingMessages.Select(item => new AdminAgentMessageViewModel
                {
                    Id = item.Id,
                    ProposedProcessDefinitionId = item.ProposedProcessDefinitionId,
                    Kind = item.Kind,
                    State = item.State,
                    Title = item.Title,
                    Body = item.Body,
                    AgentName = item.AgentName ?? string.Empty,
                    ContextLink = BuildContextLink(item),
                    CreatedOn = item.CreatedOn.LocalDateTime.ToString("dd MMM yyyy HH:mm")
                })
            ],
            DraftProcesses =
            [
                .. draftProcesses.Select(item => new AdminProcessDraftViewModel
                {
                    Id = item.Id,
                    SupersedesProcessDefinitionId = item.SupersedesProcessDefinitionId,
                    Name = item.Name,
                    ScopeType = item.ScopeType.ToString(),
                    VersionNumber = item.VersionNumber,
                    ChangeSummary = item.ChangeSummary ?? string.Empty,
                    ProposedByAgent = item.ProposedByAgent ?? string.Empty,
                    CreatedOn = item.CreatedOn.LocalDateTime.ToString("dd MMM yyyy HH:mm")
                })
            ]
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveMessage(Guid id, Guid? proposedProcessDefinitionId, string responseNotes)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        await agentMessageService.RespondAsync(id, AgentMessageState.Approved, CurrentUserId, responseNotes);

        if (proposedProcessDefinitionId.HasValue)
            await processDraftService.ActivateDraftAsync(proposedProcessDefinitionId.Value, CurrentUserId, responseNotes);

        TempData["AdminNotice"] = "Approval recorded.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectMessage(Guid id, string responseNotes)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        await agentMessageService.RespondAsync(id, AgentMessageState.Rejected, CurrentUserId, responseNotes);
        TempData["AdminNotice"] = "Rejection recorded.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DismissMessage(Guid id, string responseNotes)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        await agentMessageService.RespondAsync(id, AgentMessageState.Dismissed, CurrentUserId, responseNotes);
        TempData["AdminNotice"] = "Message dismissed.";
        return RedirectToAction(nameof(Index));
    }

    IActionResult RedirectIfUnauthenticated()
    {
        if (!string.IsNullOrWhiteSpace(authInfo?.SSOUserId)
            && !string.Equals(authInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            return null;

        string returnUrl = $"{Request.Path}{Request.QueryString}";
        return RedirectToAction("Login", "Account", new { returnUrl });
    }

    string CurrentUserId =>
        string.IsNullOrWhiteSpace(authInfo?.SSOUserId)
            ? "system"
            : authInfo.SSOUserId;

    static string BuildContextLink(AgentMessage message)
    {
        if (message.ProcessDefinitionId.HasValue)
            return $"/Process/Edit/{message.ProcessDefinitionId.Value}";

        if (message.TenantCompanyRelationshipId.HasValue)
            return $"/Clients/Edit/{message.TenantCompanyRelationshipId.Value}";

        if (message.LeadId.HasValue)
            return $"/Leads/Edit/{message.LeadId.Value}";

        if (message.OpportunityId.HasValue)
            return $"/Opportunities";

        return "/Admin";
    }
}
