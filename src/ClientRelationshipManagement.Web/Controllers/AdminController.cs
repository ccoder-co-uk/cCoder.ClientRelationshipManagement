using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.AI.Services.Foundations.Models;
using cCoder.AI.Models.Responses;
using ClientRelationshipManagement.Web.Models.Admin;
using ClientRelationshipManagement.Web.Services.Agents;
using ClientRelationshipManagement.Web.Services.Processes;
using ClientRelationshipManagement.Web.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Controllers;

public sealed class AdminController(
    IPlatformDbContextFactory dbContextFactory,
    IAgentMessageService agentMessageService,
    IProcessDraftService processDraftService,
    IProcessValidationService processValidationService,
    IWorkflowAutomationService workflowAutomationService,
    IAiProviderSelectionService aiProviderSelectionService,
    IModelManagerService modelManagerService,
    IOptions<AgentWorkflowOptions> agentWorkflowOptions,
    ICRMAuthInfo authInfo)
    : Controller
{
    [HttpGet("/Admin")]
    public async Task<IActionResult> Index()
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);

        int pendingEmailApprovalCount = await context.Emails.CountAsync(item => item.State == EmailState.Draft);
        int pendingMessageCount = await context.AgentMessages.CountAsync(item => item.State == AgentMessageState.Pending);
        int pendingProposalCount = await context.ProcessDefinitions.CountAsync(
            item => item.LifecycleState == ProcessDefinitionLifecycleState.Draft);
        AiProviderSelection aiSelection = await aiProviderSelectionService.GetAsync(RoutingUserId);
        int approvalConcurrency = await context.AgentAutomationSettings
            .AsNoTracking()
            .Where(item => item.UserId == RoutingUserId)
            .Select(item => (int?)item.ApprovalAgentConcurrency)
            .SingleOrDefaultAsync() ?? 2;
        IReadOnlyList<AiWorkLaneSelection> workLanes =
            await aiProviderSelectionService.GetWorkLanesAsync(RoutingUserId);

        return View(new AdminDashboardViewModel
        {
            Notice = TempData["AdminNotice"]?.ToString() ?? string.Empty,
            PendingEmailApprovalCount = pendingEmailApprovalCount,
            PendingAgentMessageCount = pendingMessageCount,
            PendingProcessProposalCount = pendingProposalCount,
            SelectedAiProfileKey = aiSelection.Profile.Key,
            SelectedAiModel = aiSelection.Model,
            ApprovalConcurrency = approvalConcurrency,
            AiProfiles =
            [
                .. aiProviderSelectionService.GetProfiles().Select(profile => new AdminAiProfileViewModel
                {
                    Key = profile.Key,
                    DisplayName = profile.DisplayName,
                    Description = profile.Description,
                    Model = profile.Model,
                    Endpoint = profile.CompletionEndpoint,
                    MaxConcurrency = profile.MaxConcurrency,
                    IsConfigured = profile.IsConfigured
                })
            ],
            AiWorkLanes =
            [
                .. workLanes.Select(lane => new AdminAiWorkLaneViewModel
                {
                    Lane = lane.Lane.ToString(),
                    DisplayName = lane.Lane switch
                    {
                        AgentWorkLane.Lead => "Lead generation",
                        AgentWorkLane.Opportunity => "Opportunity conversion",
                        AgentWorkLane.Client => "Client maintenance",
                        _ => lane.Lane.ToString()
                    },
                    SelectedProfileKey = lane.IsEnabled ? lane.Profile.Key : "none",
                    SelectedModel = lane.Model,
                    IsEnabled = lane.IsEnabled,
                    Concurrency = lane.Concurrency
                })
            ]
        });
    }

    [HttpGet]
    public async Task<IActionResult> Runs(int page = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        pageSize = pageSize is 10 or 25 or 50 ? pageSize : 25;
        int totalCount = await context.AgentRuns.CountAsync(cancellationToken);
        int totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);
        List<AgentRun> runs = await context.AgentRuns
            .AsNoTracking()
            .OrderByDescending(item => item.StartedOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return View(new AdminAgentRunsPageViewModel
        {
            Runs = [.. runs.Select(MapRun)],
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet]
    public async Task<IActionResult> Messages(CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        string[] readableTenantIds = authInfo.ReadableTenants.Length > 0 ? authInfo.ReadableTenants : authInfo.WriteableTenants;
        if (readableTenantIds.Length == 0) readableTenantIds = ["default"];
        List<AgentMessage> messages = await context.AgentMessages
            .AsNoTracking()
            .Include(item => item.Entries)
            .Where(item => readableTenantIds.Contains(item.TenantId))
            .OrderByDescending(item => item.LastUpdated)
            .Take(200)
            .ToListAsync(cancellationToken);
        return View(new AdminAgentMessagesPageViewModel { Messages = [.. messages.Select(MapMessage)] });
    }

    [HttpGet("/Admin/Messages/{id:guid}")]
    public async Task<IActionResult> Message(Guid id, CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        string[] readableTenantIds = authInfo.ReadableTenants.Length > 0 ? authInfo.ReadableTenants : authInfo.WriteableTenants;
        if (readableTenantIds.Length == 0) readableTenantIds = ["default"];
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        AgentMessage message = await context.AgentMessages.AsNoTracking().Include(item => item.Entries)
            .FirstOrDefaultAsync(item => item.Id == id && readableTenantIds.Contains(item.TenantId), cancellationToken);
        if (message is null)
            return NotFound();

        List<AgentMessage> conversations = await context.AgentMessages.AsNoTracking().Include(item => item.Entries)
            .Where(item => readableTenantIds.Contains(item.TenantId))
            .OrderByDescending(item => item.LastUpdated)
            .Take(200)
            .ToListAsync(cancellationToken);
        return View("Message", new AdminAgentConversationPageViewModel
        {
            Conversation = MapMessage(message),
            Conversations = [.. conversations.Select(MapMessage)]
        });
    }

    [HttpGet("/Admin/Messages/{id:guid}/Updates")]
    public async Task<IActionResult> MessageUpdates(Guid id, CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        string[] readableTenantIds = authInfo.ReadableTenants.Length > 0 ? authInfo.ReadableTenants : authInfo.WriteableTenants;
        if (readableTenantIds.Length == 0) readableTenantIds = ["default"];
        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        AgentMessage message = await context.AgentMessages.AsNoTracking().Include(item => item.Entries)
            .FirstOrDefaultAsync(item => item.Id == id && readableTenantIds.Contains(item.TenantId), cancellationToken);
        if (message is null)
            return NotFound();

        DateTimeOffset latestHumanOrSystem = message.Entries
            .Where(entry => !string.Equals(entry.Role, "Agent", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.CreatedOn).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
        DateTimeOffset latestAgent = message.Entries
            .Where(entry => string.Equals(entry.Role, "Agent", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.CreatedOn).DefaultIfEmpty(DateTimeOffset.MinValue).Max();

        return Json(new
        {
            message.Id,
            state = message.State.ToString(),
            message.ProposedProcessDefinitionId,
            lastUpdated = message.LastUpdated,
            awaitingAgent = message.State == AgentMessageState.Pending && latestHumanOrSystem > latestAgent,
            entries = message.Entries.OrderBy(entry => entry.CreatedOn).Select(entry => new
            {
                entry.Id,
                entry.Role,
                entry.Body,
                createdOn = entry.CreatedOn.LocalDateTime.ToString("dd MMM yyyy HH:mm")
            })
        });
    }

    [HttpGet]
    public async Task<IActionResult> ProcessProposals(CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        List<ProcessDefinition> proposals = await context.ProcessDefinitions
            .AsNoTracking()
            .Where(item => item.LifecycleState == ProcessDefinitionLifecycleState.Draft)
            .OrderByDescending(item => item.CreatedOn)
            .Take(200)
            .ToListAsync(cancellationToken);
        return View(new AdminProcessProposalsPageViewModel { Proposals = [.. proposals.Select(MapProposal)] });
    }

    [HttpGet("Admin/AiProviders/{providerKey}/Models")]
    public async Task<IActionResult> ProviderModels(
        string providerKey,
        CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        AiProviderProfile profile = aiProviderSelectionService.GetProfiles().FirstOrDefault(item =>
            string.Equals(item.Key, providerKey, StringComparison.OrdinalIgnoreCase));
        if (profile is null || !profile.IsConfigured)
            return NotFound();

        AIProviderCapabilitiesResponse capabilities = modelManagerService.GetProviderCapabilities(providerKey);
        List<string> models = [];
        try
        {
            if (capabilities.SupportsModelListing)
            {
                using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                models.AddRange((await modelManagerService.RetrieveAvailableModelsAsync(providerKey, timeout.Token))
                    .Where(item => item.IsAvailable && !string.IsNullOrWhiteSpace(item.Id))
                    .Select(item => item.Id.Trim()));
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or HttpRequestException
            or TaskCanceledException)
        {
            // Providers such as Codex CLI may expose only their configured model.
        }

        models.Add(profile.Model);
        return Json(new
        {
            provider = profile.Key,
            defaultModel = profile.Model,
            maxConcurrency = capabilities.MaxConcurrency,
            supportsModelListing = capabilities.SupportsModelListing,
            models = models.Where(model => !string.IsNullOrWhiteSpace(model))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(model => model)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectAiProfile(string profileKey, CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        try
        {
            AiProviderSelection selection = await aiProviderSelectionService.SetAsync(
                RoutingUserId,
                profileKey,
                cancellationToken);
            TempData["AdminNotice"] = $"Future LLM calls will use {selection.Profile.DisplayName}.";
        }
        catch (ArgumentException exception)
        {
            TempData["AdminNotice"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfigureAiLane(
        AgentWorkLane lane,
        string profileKey,
        int concurrency,
        CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        try
        {
            AiWorkLaneSelection selection = await aiProviderSelectionService.SetWorkLaneAsync(
                RoutingUserId,
                lane,
                profileKey,
                concurrency,
                cancellationToken);
            TempData["AdminNotice"] = selection.IsEnabled
                ? $"{lane} work will use {selection.Profile.DisplayName} with concurrency {selection.Concurrency}."
                : $"{lane} work is now human managed.";
        }
        catch (ArgumentException exception)
        {
            TempData["AdminNotice"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfigureAiRouting(
        ConfigureAiRoutingRequest request,
        CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        try
        {
            await aiProviderSelectionService.SetRoutingAsync(
                RoutingUserId,
                request.ApprovalProfileKey,
                request.ApprovalModel,
                request.ApprovalConcurrency,
                [
                    new AiWorkLaneUpdate(AgentWorkLane.Lead, request.LeadProfileKey, request.LeadModel, request.LeadConcurrency),
                    new AiWorkLaneUpdate(AgentWorkLane.Opportunity, request.OpportunityProfileKey, request.OpportunityModel, request.OpportunityConcurrency),
                    new AiWorkLaneUpdate(AgentWorkLane.Client, request.ClientProfileKey, request.ClientModel, request.ClientConcurrency)
                ],
                cancellationToken);
            TempData["AdminNotice"] = "AI routing was updated for all workflow lanes.";
        }
        catch (ArgumentException exception)
        {
            TempData["AdminNotice"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    static AdminAgentRunViewModel MapRun(AgentRun item) => new()
    {
        Id = item.Id,
        Kind = item.Kind.ToString(),
        State = item.State.ToString(),
        WorkLane = item.WorkLane?.ToString() ?? string.Empty,
        ProcessTaskId = item.ProcessTaskId,
        ProcessStepId = item.ProcessStepId,
        ProcessStepKey = item.ProcessStepKey ?? string.Empty,
        ExecutionUserId = item.ExecutionUserId,
        Provider = item.Provider,
        Model = item.Model,
        Iterations = item.Iterations,
        ProcessedItemCount = item.ProcessedItemCount,
        StartedOn = item.StartedOn.LocalDateTime.ToString("dd MMM yyyy HH:mm"),
        CompletedOn = item.CompletedOn?.LocalDateTime.ToString("dd MMM yyyy HH:mm") ?? string.Empty,
        Summary = item.Summary ?? string.Empty,
        ErrorMessage = item.ErrorMessage ?? string.Empty
    };

    static AdminAgentMessageViewModel MapMessage(AgentMessage item)
    {
        DateTimeOffset latestHumanOrSystem = item.Entries
            .Where(entry => !string.Equals(entry.Role, "Agent", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.CreatedOn).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
        DateTimeOffset latestAgent = item.Entries
            .Where(entry => string.Equals(entry.Role, "Agent", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.CreatedOn).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
        return new AdminAgentMessageViewModel
        {
            Id = item.Id,
            ProposedProcessDefinitionId = item.ProposedProcessDefinitionId,
            Kind = item.Kind,
            State = item.State,
            Title = item.Title,
            Body = item.Body,
            AgentName = item.AgentName ?? string.Empty,
            ContextLink = BuildContextLink(item),
            ContextLabel = BuildContextLabel(item),
            CreatedOn = item.CreatedOn.LocalDateTime.ToString("dd MMM yyyy HH:mm"),
            LastUpdatedOn = item.LastUpdated.LocalDateTime.ToString("dd MMM yyyy HH:mm"),
            EntryCount = item.Entries.Count,
            IsAwaitingAgent = item.State == AgentMessageState.Pending && latestHumanOrSystem > latestAgent,
            Entries = item.Entries.OrderBy(entry => entry.CreatedOn).Select(entry => new AdminAgentMessageEntryViewModel
            {
                Id = entry.Id,
                Role = entry.Role,
                Body = entry.Body,
                CreatedOn = entry.CreatedOn.LocalDateTime.ToString("dd MMM yyyy HH:mm")
            }).ToList()
        };
    }

    static AdminProcessDraftViewModel MapProposal(ProcessDefinition item) => new()
    {
        Id = item.Id,
        SupersedesProcessDefinitionId = item.SupersedesProcessDefinitionId,
        Name = item.Name,
        ScopeType = item.ScopeType.ToString(),
        VersionNumber = item.VersionNumber,
        ChangeSummary = item.ChangeSummary ?? string.Empty,
        ProposedByAgent = item.ProposedByAgent ?? string.Empty,
        CreatedOn = item.CreatedOn.LocalDateTime.ToString("dd MMM yyyy HH:mm")
    };

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplyToMessage(Guid id, string responseNotes, string returnUrl, CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (string.IsNullOrWhiteSpace(responseNotes))
        {
            TempData["AdminNotice"] = "Add feedback or a question before sending the message.";
            return RedirectAfterMessageAction(returnUrl);
        }

        await agentMessageService.AppendEntryAsync(id, "User", responseNotes, CurrentUserId, cancellationToken);
        TempData["AdminNotice"] = "Your response was added. The Approval Agent will continue the review.";
        return RedirectAfterMessageAction(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveMessage(Guid id, string returnUrl, CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        AgentMessage message = await agentMessageService.ChangeStateAsync(
            id,
            AgentMessageState.Completed,
            CurrentUserId,
            $"Conversation resolved by {CurrentUserId}.",
            cancellationToken);
        TempData["AdminNotice"] = message is null ? "Conversation was not found." : "Conversation resolved.";
        return RedirectAfterMessageAction(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReopenMessage(Guid id, string returnUrl, CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        AgentMessage message = await agentMessageService.ChangeStateAsync(
            id,
            AgentMessageState.Pending,
            CurrentUserId,
            $"Conversation reopened by {CurrentUserId}.",
            cancellationToken);
        TempData["AdminNotice"] = message is null ? "Conversation was not found." : "Conversation reopened for the Approval Agent.";
        return RedirectAfterMessageAction(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveMessage(
        Guid id,
        Guid? proposedProcessDefinitionId,
        string responseNotes,
        string returnUrl)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (proposedProcessDefinitionId.HasValue)
        {
            ProcessValidationResult validation = await processValidationService.ValidateDefinitionAsync(proposedProcessDefinitionId.Value);
            if (!validation.IsValid)
            {
                string errors = string.Join(" ", validation.Issues
                    .Where(issue => issue.Severity == ProcessValidationSeverity.Error)
                    .Take(3)
                    .Select(issue => $"{issue.StepName}: {issue.Message}".TrimStart(':', ' ')));
                TempData["AdminNotice"] = $"Draft was not activated because validation failed. {errors}";
                return RedirectAfterMessageAction(returnUrl);
            }
        }

        await agentMessageService.RespondAsync(id, AgentMessageState.Approved, CurrentUserId, responseNotes);

        if (proposedProcessDefinitionId.HasValue)
        {
            ProcessDefinition activated = await processDraftService.ActivateDraftAsync(proposedProcessDefinitionId.Value, CurrentUserId, responseNotes);
            if (activated is not null)
            {
                await workflowAutomationService.EnsureDefinitionCoverageAsync(activated.Id);
                if (activated.ScopeType == ProcessScopeType.Lead)
                    await workflowAutomationService.ReevaluateDeferredLeadsAsync(activated.TenantId);
            }
        }

        TempData["AdminNotice"] = "Approval recorded.";
        return RedirectAfterMessageAction(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectMessage(Guid id, string responseNotes, string returnUrl)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        await agentMessageService.RespondAsync(id, AgentMessageState.Rejected, CurrentUserId, responseNotes);
        TempData["AdminNotice"] = "Rejection recorded.";
        return RedirectAfterMessageAction(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DismissMessage(Guid id, string responseNotes, string returnUrl)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        await agentMessageService.RespondAsync(id, AgentMessageState.Dismissed, CurrentUserId, responseNotes);
        TempData["AdminNotice"] = "Message dismissed.";
        return RedirectAfterMessageAction(returnUrl);
    }

    IActionResult RedirectAfterMessageAction(string returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Messages));

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

    string RoutingUserId => string.IsNullOrWhiteSpace(agentWorkflowOptions.Value.ExecutionUserId)
        ? CurrentUserId
        : agentWorkflowOptions.Value.ExecutionUserId.Trim();

    static string BuildContextLink(AgentMessage message)
    {
        if (message.EmailId.HasValue)
            return $"/Admin/Emails?id={message.EmailId.Value}#email-{message.EmailId.Value}";

        if (message.ProcessDefinitionId.HasValue)
            return $"/Admin/Process/Edit/{message.ProcessDefinitionId.Value}";

        if (message.TenantCompanyRelationshipId.HasValue)
            return $"/Clients/Edit/{message.TenantCompanyRelationshipId.Value}";

        if (message.LeadId.HasValue)
            return $"/Leads/Edit/{message.LeadId.Value}";

        if (message.OpportunityId.HasValue)
            return $"/Opportunities";

        return string.Empty;
    }

    static string BuildContextLabel(AgentMessage message) =>
        message.EmailId.HasValue ? "View source email"
        : message.ProcessDefinitionId.HasValue ? "View source process"
        : message.TenantCompanyRelationshipId.HasValue ? "View related account"
        : message.LeadId.HasValue ? "View related lead"
        : message.OpportunityId.HasValue ? "View related opportunity"
        : string.Empty;
}
