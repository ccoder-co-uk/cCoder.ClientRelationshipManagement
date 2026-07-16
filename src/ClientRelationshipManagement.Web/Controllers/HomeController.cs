using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Services.Foundations.Platform;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.Security.Objects;
using ClientRelationshipManagement.Web.Documentation;
using ClientRelationshipManagement.Web.Configuration;
using ClientRelationshipManagement.Web.Models.Home;
using ClientRelationshipManagement.Web.Services.Agents;
using ClientRelationshipManagement.Web.Services.Mail;
using ClientRelationshipManagement.Web.Services.Processes;
using ClientRelationshipManagement.Web.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PlatformEntities = cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace ClientRelationshipManagement.Web.Controllers;

public class HomeController(
    ISalesCoordinationService salesWorkspaceService,
    IOperationsCoordinationService operationsService,
    IEmailDraftWorkflowService emailDraftWorkflowService,
    IWorkflowAutomationService workflowAutomationService,
    IAgentAutomationSettingsService automationSettingsService,
    ICRMAuthInfo authInfo,
    ISSOAuthInfo ssoAuthInfo)
    : Controller
{
    const string ProcessSourceType = "process";
    const int ProcessSourcePriority = 4;
    const int LeadLane = 0;
    const int OpportunityLane = 1;
    const int ClientLane = 2;

    public async Task<IActionResult> Index()
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        SetNoStoreHeaders();
        return View(await BuildDashboardAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Stats(CancellationToken cancellationToken = default)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        SetNoStoreHeaders();
        string[] readableTenantIds = GetReadableTenantIds();
        DateTimeOffset today = new(DateTime.UtcNow.Date, TimeSpan.Zero);
        DateTimeOffset tomorrow = today.AddDays(1);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        IQueryable<PlatformEntities.ProcessTask> pendingTasks = salesWorkspaceService.RetrieveProcessTasks()
            .AsNoTracking()
            .Where(task => task.State == ProcessTaskState.Pending)
            .Where(task =>
                (task.LeadId.HasValue && readableTenantIds.Contains(task.Lead!.TenantId))
                || (task.TenantCompanyRelationshipId.HasValue && readableTenantIds.Contains(task.TenantCompanyRelationship!.TenantId)));

        List<LaneTaskSummary> laneTaskSummaries = await BuildLaneTaskSummariesAsync(
            pendingTasks,
            today,
            tomorrow,
            cancellationToken);
        LaneActionStatsViewModel leadActions = GetLaneActions(laneTaskSummaries, LeadLane);
        LaneActionStatsViewModel opportunityActions = GetLaneActions(laneTaskSummaries, OpportunityLane);
        LaneActionStatsViewModel clientActions = GetLaneActions(laneTaskSummaries, ClientLane);
        Dictionary<AgentWorkLane, LaneAgentHealthViewModel> agentHealth = await BuildAgentHealthAsync(
            cancellationToken);
        List<Guid> activeTaskIds = await pendingTasks
            .Where(task => task.AgentClaimId.HasValue && task.AgentClaimExpiresOn > now)
            .Select(task => task.Id)
            .ToListAsync(cancellationToken);

        Dictionary<RelationshipStatus, int> clientStateCounts = await salesWorkspaceService.RetrieveRelationships()
            .AsNoTracking()
            .Where(relationship => !relationship.IsArchived && readableTenantIds.Contains(relationship.TenantId))
            .GroupBy(relationship => relationship.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);

        int totalClients = await salesWorkspaceService.RetrieveClientAccounts().AsNoTracking()
            .CountAsync(account => account.Status != ClientAccountStatus.Closed
                && readableTenantIds.Contains(account.TenantCompanyRelationship.TenantId), cancellationToken);
        int activeOpportunities = await salesWorkspaceService.RetrieveOpportunities().AsNoTracking()
            .CountAsync(opportunity => opportunity.Stage != SalesPipelineStage.Won
                && opportunity.Stage != SalesPipelineStage.Lost
                && readableTenantIds.Contains(opportunity.TenantCompanyRelationship.TenantId), cancellationToken);
        int activeLeads = await salesWorkspaceService.RetrieveLeads().AsNoTracking()
            .CountAsync(lead => lead.Status != LeadStatus.Rejected
                && lead.Status != LeadStatus.Converted
                && readableTenantIds.Contains(lead.TenantId), cancellationToken);
        int suppressedCompanies = await salesWorkspaceService.RetrieveCompanies().AsNoTracking()
            .CountAsync(company => company.SourceSystem == "CompaniesHouse" && company.IsProspectingSuppressed, cancellationToken);
        int candidateCompanies = await salesWorkspaceService.RetrieveCompanies().AsNoTracking()
            .CountAsync(company => company.SourceSystem == "CompaniesHouse"
                && !company.IsProspectingSuppressed
                && !company.Relationships.Any()
                && !salesWorkspaceService.RetrieveLeads().Any(lead => lead.CompanyId == company.Id), cancellationToken);

        long dashboardVersion = await GetDashboardVersionAsync(cancellationToken);
        int totalOpenActions = laneTaskSummaries.Sum(item => item.Total);
        return Json(new HomeDashboardStatsViewModel
        {
            TotalClients = totalClients,
            ActiveLeads = activeLeads,
            ActiveOpportunities = activeOpportunities,
            CandidateCompanies = candidateCompanies,
            SuppressedCompanies = suppressedCompanies,
            TotalOpenActions = totalOpenActions,
            DueTodayActions = laneTaskSummaries.Sum(item => item.DueToday),
            OverdueActions = laneTaskSummaries.Sum(item => item.Overdue),
            LeadActions = leadActions,
            OpportunityActions = opportunityActions,
            ClientActions = clientActions,
            LeadAgentHealth = agentHealth[AgentWorkLane.Lead],
            OpportunityAgentHealth = agentHealth[AgentWorkLane.Opportunity],
            ClientAgentHealth = agentHealth[AgentWorkLane.Client],
            AdditionalActionCount = Math.Max(totalOpenActions - 5, 0),
            QueueVersion = dashboardVersion,
            UpdatedOn = DateTimeOffset.UtcNow,
            ActiveTaskIds = activeTaskIds,
            ClientStates =
            [
                .. Enum.GetValues<RelationshipStatus>().Select(status => new HomeDashboardStateStatViewModel
                {
                    Key = status.ToString(),
                    Count = clientStateCounts.GetValueOrDefault(status)
                })
            ]
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAutoApproveProcessEmails(bool enabled)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        await automationSettingsService.SetAutoApproveProcessEmailsAsync(ssoAuthInfo.SSOUserId, enabled);
        TempData["DashboardNotice"] = enabled
            ? "Email auto-approval is on. The approval agent will review in-process drafts for intent, spelling, grammar, and tone."
            : "Email auto-approval is off. In-process drafts require manual approval.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> CompleteTodo(CompleteTodoRequest request)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (request?.Id == Guid.Empty)
            return RedirectToAction(nameof(Index));

        if (string.IsNullOrWhiteSpace(request.CompletionNote))
        {
            TempData["DashboardNotice"] = "Add an outcome note before clearing the action.";
            return RedirectToAction(nameof(Index));
        }

        PlatformEntities.ProcessTask updatedTask;
        try
        {
            updatedTask = await workflowAutomationService.CompleteTaskAsync(
                new ProcessTaskCompletionCommand
                {
                    ProcessTaskId = request.Id,
                    OutcomeKey = request.OutcomeKey,
                    CompletionNote = request.CompletionNote
                });
        }
        catch (WorkflowRuleViolationException exception)
        {
            TempData["DashboardNotice"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }

        TempData["DashboardNotice"] = updatedTask is null
            ? "That action could not be updated."
            : "Progress recorded and workflow advanced.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SaveDraftEmail(SaveTodoDraftEmailRequest request)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (request.ClientId == Guid.Empty)
            return RedirectToAction(nameof(Index));

        if (!TryValidateModel(request))
        {
            TempData["DashboardNotice"] = "Add both an email subject and body before saving the draft.";
            return RedirectToAction(nameof(Index));
        }

        PlatformEntities.Email email = await emailDraftWorkflowService.SaveDraftAsync(
            new EmailDraftUpsertCommand
            {
                ClientId = request.ClientId,
                EmailId = request.EmailId,
                ClientMaterialId = request.ClientMaterialId == Guid.Empty ? null : request.ClientMaterialId,
                ClientOpportunityId = request.ClientOpportunityId,
                Direction = request.Direction,
                Subject = request.Subject,
                Body = request.Body,
                ToAddresses = request.ToAddresses,
                CcAddresses = request.CcAddresses,
                BccAddresses = request.BccAddresses,
                ScheduledSendTimeUtc = ToDateTimeOffset(request.ScheduledSendOn)
            });

        TempData["DashboardNotice"] = email is null
            ? "That draft email could not be updated."
            : "Draft email updated.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ApproveDraftEmail(ApproveTodoDraftEmailRequest request)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (request.ClientId == Guid.Empty || request.EmailId == Guid.Empty)
            return RedirectToAction(nameof(Index));

        PlatformEntities.Email approvedEmail = await emailDraftWorkflowService.ApproveAsync(
            request.ClientId,
            request.EmailId,
            ToDateTimeOffset(request.ScheduledSendOn));

        TempData["DashboardNotice"] = approvedEmail is null
            ? "That draft email could not be approved."
            : "Draft approved for sending.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmDraftEmailSent(ConfirmTodoDraftSentRequest request)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (request.ClientId == Guid.Empty || request.EmailId == Guid.Empty)
            return RedirectToAction(nameof(Index));

        PlatformEntities.Email email = await emailDraftWorkflowService.MarkSentAsync(request.ClientId, request.EmailId);
        bool taskCompleted = email is not null && await workflowAutomationService.CompleteEmailTaskAsync(request.EmailId);

        TempData["DashboardNotice"] = email is null
            ? "That draft email could not be marked as sent."
            : taskCompleted
                ? "Draft marked as sent and workflow advanced."
                : "Draft marked as sent.";

        return RedirectToAction(nameof(Index));
    }

    IActionResult RedirectIfUnauthenticated()
    {
        if (!string.IsNullOrWhiteSpace(ssoAuthInfo?.SSOUserId)
            && !string.Equals(ssoAuthInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            return null;

        string returnUrl = $"{Request.Path}{Request.QueryString}";
        return RedirectToAction("Login", "Account", new { returnUrl });
    }

    void SetNoStoreHeaders()
    {
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
    }

    async ValueTask<long> GetDashboardVersionAsync(
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset? taskVersion = await salesWorkspaceService.RetrieveProcessTasks()
            .AsNoTracking()
            .MaxAsync(task => (DateTimeOffset?)task.LastUpdated, cancellationToken);

        DateTimeOffset? emailVersion = await operationsService.RetrieveAllEmails()
            .AsNoTracking()
            .MaxAsync(email => (DateTimeOffset?)email.LastUpdated, cancellationToken);

        DateTimeOffset latest = new[] { taskVersion, emailVersion }
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty(DateTimeOffset.MinValue)
            .Max();
        return latest == DateTimeOffset.MinValue ? 0 : latest.ToUnixTimeMilliseconds();
    }

    async Task<HomeDashboardViewModel> BuildDashboardAsync()
    {
        PlatformEntities.AgentAutomationSetting automationSetting =
            await automationSettingsService.GetAsync(ssoAuthInfo.SSOUserId);
        string[] readableTenantIds = GetReadableTenantIds();
        DateTimeOffset today = new(DateTime.UtcNow.Date, TimeSpan.Zero);
        DateTimeOffset tomorrow = today.AddDays(1);

        IQueryable<PlatformEntities.ProcessTask> pendingTasks = salesWorkspaceService.RetrieveProcessTasks()
            .AsNoTracking()
            .Where(task => task.State == ProcessTaskState.Pending)
            .Where(task =>
                (task.LeadId.HasValue && readableTenantIds.Contains(task.Lead!.TenantId))
                || (task.TenantCompanyRelationshipId.HasValue && readableTenantIds.Contains(task.TenantCompanyRelationship!.TenantId)));

        List<LaneTaskSummary> laneTaskSummaries = await BuildLaneTaskSummariesAsync(
            pendingTasks,
            today,
            tomorrow,
            CancellationToken.None);
        LaneActionStatsViewModel leadActions = GetLaneActions(laneTaskSummaries, LeadLane);
        LaneActionStatsViewModel opportunityActions = GetLaneActions(laneTaskSummaries, OpportunityLane);
        LaneActionStatsViewModel clientActions = GetLaneActions(laneTaskSummaries, ClientLane);
        Dictionary<AgentWorkLane, LaneAgentHealthViewModel> agentHealth = await BuildAgentHealthAsync(
            CancellationToken.None);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        IQueryable<PlatformEntities.ProcessTask> runnableTasks = salesWorkspaceService.RetrieveRunnableProcessTasks(now);
        runnableTasks = runnableTasks.Where(task =>
            (task.LeadId.HasValue && readableTenantIds.Contains(task.Lead!.TenantId))
            || (task.TenantCompanyRelationshipId.HasValue && readableTenantIds.Contains(task.TenantCompanyRelationship!.TenantId)));

        IQueryable<PlatformEntities.ProcessTask> displayableTasks = runnableTasks.Concat(
            pendingTasks.Where(task => task.AgentClaimId.HasValue && task.AgentClaimExpiresOn > now));

        List<PlatformEntities.ProcessTask> tasks = await WorkflowTaskQueue.OrderByCommercialProgress(displayableTasks)
            .AsNoTracking()
            .Include(task => task.ProcessStep)
            .Include(task => task.Lead)
            .Include(task => task.Email)
            .Include(task => task.TenantCompanyRelationship)
                .ThenInclude(relationship => relationship.Company)
            .Take(5)
            .ToListAsync();

        if (tasks.Count < 5)
        {
            Guid[] selectedTaskIds = [.. tasks.Select(task => task.Id)];
            List<PlatformEntities.ProcessTask> remainingTasks = await WorkflowTaskQueue.OrderByCommercialProgress(
                    pendingTasks.Where(task => !selectedTaskIds.Contains(task.Id)))
                .AsNoTracking()
                .Include(task => task.ProcessStep)
                .Include(task => task.Lead)
                .Include(task => task.Email)
                .Include(task => task.TenantCompanyRelationship)
                    .ThenInclude(relationship => relationship.Company)
                .Take(5 - tasks.Count)
                .ToListAsync();
            tasks.AddRange(remainingTasks);
        }

        List<Guid> stepIds =
        [
            .. tasks.Select(task => task.ProcessStepId).Distinct()
        ];

        Dictionary<Guid, List<TodoOutcomeOptionViewModel>> outcomeLookup = stepIds.Count == 0
            ? new Dictionary<Guid, List<TodoOutcomeOptionViewModel>>()
            : await salesWorkspaceService.RetrieveProcessTransitions()
                .AsNoTracking()
                .Where(transition => stepIds.Contains(transition.ProcessStepId))
                .GroupBy(transition => transition.ProcessStepId)
                .ToDictionaryAsync(
                    group => group.Key,
                    group => group
                        .OrderByDescending(item => item.IsDefaultOutcome)
                        .ThenBy(item => item.OutcomeLabel)
                        .Select(item => new TodoOutcomeOptionViewModel
                        {
                            Key = item.OutcomeKey,
                            Label = item.OutcomeLabel
                        })
                        .ToList());

        List<TodoItemViewModel> todoItems =
        [
            .. tasks.Select(task => ToTodoItem(task, outcomeLookup.GetValueOrDefault(task.ProcessStepId) ?? []))
        ];

        List<ClientStateSummaryViewModel> stateSummaries = await salesWorkspaceService.RetrieveRelationships()
            .AsNoTracking()
            .Where(relationship => !relationship.IsArchived && readableTenantIds.Contains(relationship.TenantId))
            .GroupBy(relationship => relationship.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync()
            .ContinueWith(task =>
            {
                Dictionary<RelationshipStatus, int> counts = task.Result.ToDictionary(item => item.Status, item => item.Count);

                return Enum.GetValues<RelationshipStatus>()
                    .Select(status =>
                    {
                        string lane = GetRelationshipLane(status);
                        string destination = lane == "opportunity" ? "/Opportunities" : "/Clients";
                        return new ClientStateSummaryViewModel
                        {
                            Label = DisplayText.Humanize(status),
                            Count = counts.GetValueOrDefault(status),
                            AccentClass = $"metric-pill--lane-{lane}",
                            Lane = lane,
                            Tooltip = ClientStateGuide.GetTooltip(status),
                            LinkUrl = $"{destination}?status={Uri.EscapeDataString(status.ToString())}"
                        };
                    })
                    .ToList();
            });

        int totalClients = await salesWorkspaceService.RetrieveClientAccounts().AsNoTracking()
            .CountAsync(account => account.Status != ClientAccountStatus.Closed
                && readableTenantIds.Contains(account.TenantCompanyRelationship.TenantId));
        int activeOpportunities = await salesWorkspaceService.RetrieveOpportunities().AsNoTracking()
            .CountAsync(opportunity => opportunity.Stage != SalesPipelineStage.Won
                && opportunity.Stage != SalesPipelineStage.Lost
                && readableTenantIds.Contains(opportunity.TenantCompanyRelationship.TenantId));
        int activeLeads = await salesWorkspaceService.RetrieveLeads().AsNoTracking()
            .CountAsync(lead => lead.Status != LeadStatus.Rejected
                && lead.Status != LeadStatus.Converted
                && readableTenantIds.Contains(lead.TenantId));
        int suppressedCompanies = await salesWorkspaceService.RetrieveCompanies().AsNoTracking()
            .CountAsync(company => company.SourceSystem == "CompaniesHouse" && company.IsProspectingSuppressed);
        int candidateCompanies = await salesWorkspaceService.RetrieveCompanies().AsNoTracking()
            .CountAsync(company => company.SourceSystem == "CompaniesHouse"
                && !company.IsProspectingSuppressed
                && !company.Relationships.Any()
                && !salesWorkspaceService.RetrieveLeads().Any(lead => lead.CompanyId == company.Id));

        long dashboardVersion = await GetDashboardVersionAsync();
        return new HomeDashboardViewModel
        {
            Notice = TempData["DashboardNotice"]?.ToString(),
            AutoApproveProcessEmails = automationSetting?.AutoApproveProcessEmails == true,
            TotalClients = totalClients,
            ActiveLeads = activeLeads,
            ActiveOpportunities = activeOpportunities,
            CandidateCompanies = candidateCompanies,
            SuppressedCompanies = suppressedCompanies,
            TotalOpenActions = laneTaskSummaries.Sum(item => item.Total),
            DueTodayActions = laneTaskSummaries.Sum(item => item.DueToday),
            OverdueActions = laneTaskSummaries.Sum(item => item.Overdue),
            LeadActions = leadActions,
            OpportunityActions = opportunityActions,
            ClientActions = clientActions,
            LeadAgentHealth = agentHealth[AgentWorkLane.Lead],
            OpportunityAgentHealth = agentHealth[AgentWorkLane.Opportunity],
            ClientAgentHealth = agentHealth[AgentWorkLane.Client],
            AdditionalActionCount = Math.Max(laneTaskSummaries.Sum(item => item.Total) - 5, 0),
            QueueVersion = dashboardVersion,
            StatusOptions = BuildStatusOptions(),
            StageOptions = BuildStageOptions(),
            ClientStateSummaries = stateSummaries,
            TodoItems = todoItems
        };
    }

    async Task<Dictionary<AgentWorkLane, LaneAgentHealthViewModel>> BuildAgentHealthAsync(CancellationToken cancellationToken)
    {
        const int sampleLimit = 10;
        Dictionary<AgentWorkLane, LaneAgentHealthViewModel> result = [];

        foreach (AgentWorkLane lane in Enum.GetValues<AgentWorkLane>())
        {
            List<AgentRunState> states = await operationsService.RetrieveAllAgentRuns()
                .AsNoTracking()
                .Where(run => run.Kind == AgentRunKind.TaskAgent
                    && run.WorkLane == lane
                    && run.ExecutionUserId == ssoAuthInfo.SSOUserId
                    && (run.State == AgentRunState.Succeeded || run.State == AgentRunState.Failed))
                .OrderByDescending(run => run.CompletedOn)
                .Take(sampleLimit)
                .Select(run => run.State)
                .ToListAsync(cancellationToken);

            int succeeded = states.Count(state => state == AgentRunState.Succeeded);
            int failed = states.Count - succeeded;
            string status = states.Count == 0
                ? "unknown"
                : failed == 0
                    ? "healthy"
                    : succeeded == 0
                        ? "failing"
                        : "degraded";
            result[lane] = new LaneAgentHealthViewModel
            {
                Status = status,
                SampleSize = states.Count,
                Succeeded = succeeded,
                Failed = failed
            };
        }

        return result;
    }

    static TodoItemViewModel ToTodoItem(
        PlatformEntities.ProcessTask task,
        IReadOnlyList<TodoOutcomeOptionViewModel> outcomeOptions)
    {
        string title = FirstNonEmpty(
            CompanyNames.ResolvePreferredName(task.TenantCompanyRelationship?.Company),
            task.Lead?.RawCompanyName,
            "Untitled workflow item");

        string context = task.LeadId.HasValue
            ? "Lead qualification"
            : task.ClientAccountId.HasValue
                ? "Client account"
                : task.OpportunityId.HasValue
                    ? "Opportunity"
                    : "Relationship";

        string lane = task.LeadId.HasValue
            ? "lead"
            : task.ClientAccountId.HasValue
                ? "client"
                : "opportunity";

        string detailUrl = task.LeadId.HasValue
            ? $"/Leads/Edit/{task.LeadId}"
            : task.TenantCompanyRelationshipId.HasValue
                ? $"/Clients/Edit/{task.TenantCompanyRelationshipId}"
                : "/Clients";

        return new TodoItemViewModel
        {
            Id = task.Id,
            ClientId = task.TenantCompanyRelationshipId ?? Guid.Empty,
            SourceType = ProcessSourceType,
            SourceLabel = DisplayText.Humanize(task.ActionType),
            Lane = lane,
            SourcePriority = ProcessSourcePriority,
            Title = title,
            Context = context,
            Description = string.IsNullOrWhiteSpace(task.RenderedInstructions)
                ? task.RenderedTitle
                : task.RenderedInstructions,
            DetailUrl = detailUrl,
            DueOn = task.DueOn,
            DueLabel = BuildDueLabel(task.DueOn),
            IsOverdue = task.DueOn.UtcDateTime.Date < DateTime.UtcNow.Date,
            IsAgentWorking = task.AgentClaimId.HasValue && task.AgentClaimExpiresOn > DateTimeOffset.UtcNow,
            ProcessActionType = task.ActionType,
            ProcessInstructions = task.RenderedInstructions,
            ProcessCallScript = task.RenderedCallScript,
            ProcessQuestionSet = task.RenderedQuestionSet,
            OutcomeOptions = outcomeOptions,
            DraftEmailId = task.EmailId,
            DraftEmailMaterialId = task.Email?.MaterialId,
            DraftEmailClientOpportunityId = task.Email?.OpportunityId,
            DraftEmailDirectionValue = ActivityDirection.Outbound.ToString(),
            DraftEmailStatusLabel = task.Email is null ? string.Empty : DisplayText.Humanize(task.Email.State),
            DraftEmailToAddresses = task.Email?.ToAddresses,
            DraftEmailCcAddresses = task.Email?.CcAddresses,
            DraftEmailBccAddresses = task.Email?.BccAddresses,
            DraftEmailScheduledSendOnValue = task.Email?.ScheduledSendTimeUtc?.LocalDateTime.ToString("yyyy-MM-ddTHH:mm"),
            DraftEmailSubject = task.Email?.Subject ?? task.RenderedEmailSubject,
            DraftEmailBody = FirstNonEmpty(task.Email?.BodyText, task.Email?.BodyHtml, task.RenderedEmailBody)
        };
    }

    string[] GetReadableTenantIds()
    {
        if (authInfo.WriteableTenants.Length > 0)
            return authInfo.WriteableTenants;

        if (authInfo.ReadableTenants.Length > 0)
            return authInfo.ReadableTenants;

        return ["default"];
    }

    static string BuildDueLabel(DateTimeOffset dueOn)
    {
        DateTime dueDate = dueOn.UtcDateTime.Date;
        DateTime today = DateTime.UtcNow.Date;

        return dueDate == today
            ? $"Due today | {dueOn:dd MMM}"
            : dueDate < today
                ? $"Overdue | {dueOn:dd MMM}"
                : $"Due {dueOn:ddd dd MMM}";
    }

    static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    static Task<List<LaneTaskSummary>> BuildLaneTaskSummariesAsync(
        IQueryable<PlatformEntities.ProcessTask> pendingTasks,
        DateTimeOffset today,
        DateTimeOffset tomorrow,
        CancellationToken cancellationToken) =>
        pendingTasks
            .GroupBy(task => task.LeadId.HasValue
                ? LeadLane
                : task.OpportunityId.HasValue
                    ? OpportunityLane
                    : task.ClientAccountId.HasValue
                        ? ClientLane
                        : OpportunityLane)
            .Select(group => new LaneTaskSummary
            {
                Lane = group.Key,
                Total = group.Count(),
                DueToday = group.Count(task => task.DueOn >= today && task.DueOn < tomorrow),
                Overdue = group.Count(task => task.DueOn < today)
            })
            .ToListAsync(cancellationToken);

    static LaneActionStatsViewModel GetLaneActions(
        IReadOnlyList<LaneTaskSummary> summaries,
        int lane)
    {
        LaneTaskSummary summary = summaries.FirstOrDefault(item => item.Lane == lane);
        return summary is null
            ? new LaneActionStatsViewModel()
            : new LaneActionStatsViewModel
            {
                Open = summary.Total,
                DueToday = summary.DueToday,
                Overdue = summary.Overdue
            };
    }

    static IReadOnlyList<SelectListItem> BuildStatusOptions() =>
        Enum.GetValues<RelationshipStatus>()
            .Select(status => new SelectListItem(DisplayText.Humanize(status), status.ToString()))
            .ToList();

    static IReadOnlyList<SelectListItem> BuildStageOptions() =>
        Enum.GetValues<SalesPipelineStage>()
            .Select(stage => new SelectListItem(DisplayText.Humanize(stage), stage.ToString()))
            .ToList();

    static DateTimeOffset? ToDateTimeOffset(DateTime? value)
    {
        if (value is null)
            return null;

        DateTime localTime = DateTime.SpecifyKind(value.Value, DateTimeKind.Local);
        return new DateTimeOffset(localTime).ToUniversalTime();
    }

    static string GetRelationshipLane(RelationshipStatus status) => status switch
    {
        RelationshipStatus.Onboarding or RelationshipStatus.Client or RelationshipStatus.Dormant => "client",
        _ => "opportunity"
    };

    sealed class LaneTaskSummary
    {
        public int Lane { get; init; }
        public int Total { get; init; }
        public int DueToday { get; init; }
        public int Overdue { get; init; }
    }
}
