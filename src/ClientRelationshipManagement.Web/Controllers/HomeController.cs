using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.Security.Objects;
using ClientRelationshipManagement.Web.Documentation;
using ClientRelationshipManagement.Web.Models.Home;
using ClientRelationshipManagement.Web.Services.Mail;
using ClientRelationshipManagement.Web.Services.Processes;
using ClientRelationshipManagement.Web.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PlatformEntities = cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace ClientRelationshipManagement.Web.Controllers;

public class HomeController(
    IPlatformDbContextFactory dbContextFactory,
    IEmailDraftWorkflowService emailDraftWorkflowService,
    IWorkflowAutomationService workflowAutomationService,
    ICRMAuthInfo authInfo,
    ISSOAuthInfo ssoAuthInfo)
    : Controller
{
    const string ProcessSourceType = "process";
    const int ProcessSourcePriority = 4;

    public async Task<IActionResult> Index()
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        return View(await BuildDashboardAsync());
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

        PlatformEntities.ProcessTask updatedTask = await workflowAutomationService.CompleteTaskAsync(
            new ProcessTaskCompletionCommand
            {
                ProcessTaskId = request.Id,
                OutcomeKey = request.OutcomeKey,
                CompletionNote = request.CompletionNote
            });

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

    async Task<HomeDashboardViewModel> BuildDashboardAsync()
    {
        await workflowAutomationService.EnsureCoverageAsync();

        using PlatformDbContext context = dbContextFactory.CreateDbContext();
        string[] readableTenantIds = GetReadableTenantIds();
        DateTime today = DateTime.UtcNow.Date;

        List<PlatformEntities.ProcessTask> tasks = await context.ProcessTasks
            .AsNoTracking()
            .Include(task => task.ProcessStep)
            .Include(task => task.Lead)
            .Include(task => task.Email)
            .Include(task => task.TenantCompanyRelationship)
                .ThenInclude(relationship => relationship.Company)
            .Where(task => task.State == ProcessTaskState.Pending)
            .Where(task =>
                (task.LeadId.HasValue && readableTenantIds.Contains(task.Lead!.TenantId))
                || (task.TenantCompanyRelationshipId.HasValue && readableTenantIds.Contains(task.TenantCompanyRelationship!.TenantId)))
            .OrderBy(task => task.DueOn)
            .ThenBy(task => task.RenderedTitle)
            .ToListAsync();

        List<Guid> stepIds =
        [
            .. tasks.Select(task => task.ProcessStepId).Distinct()
        ];

        Dictionary<Guid, List<TodoOutcomeOptionViewModel>> outcomeLookup = stepIds.Count == 0
            ? new Dictionary<Guid, List<TodoOutcomeOptionViewModel>>()
            : await context.ProcessTransitions
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

        List<ClientStateSummaryViewModel> stateSummaries = await context.TenantCompanyRelationships
            .AsNoTracking()
            .Where(relationship => !relationship.IsArchived && readableTenantIds.Contains(relationship.TenantId))
            .GroupBy(relationship => relationship.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync()
            .ContinueWith(task =>
            {
                Dictionary<RelationshipStatus, int> counts = task.Result.ToDictionary(item => item.Status, item => item.Count);

                return Enum.GetValues<RelationshipStatus>()
                    .Select((status, index) => new ClientStateSummaryViewModel
                    {
                        Label = DisplayText.Humanize(status),
                        Count = counts.GetValueOrDefault(status),
                        AccentClass = AccentClasses[index % AccentClasses.Length],
                        Tooltip = ClientStateGuide.GetTooltip(status),
                        LinkUrl = $"/Clients?status={Uri.EscapeDataString(status.ToString())}"
                    })
                    .ToList();
            });

        return new HomeDashboardViewModel
        {
            Notice = TempData["DashboardNotice"]?.ToString(),
            TotalClients = stateSummaries.Sum(item => item.Count),
            TotalOpenActions = todoItems.Count,
            DueTodayActions = todoItems.Count(item => item.DueOn.UtcDateTime.Date == today),
            OverdueActions = todoItems.Count(item => item.DueOn.UtcDateTime.Date < today),
            AdditionalActionCount = Math.Max(todoItems.Count - 5, 0),
            StatusOptions = BuildStatusOptions(),
            StageOptions = BuildStageOptions(),
            ClientStateSummaries = stateSummaries,
            TodoItems = todoItems.Take(5).ToList()
        };
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
            SourceLabel = "Process",
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

    static readonly string[] AccentClasses =
    [
        "accent-gold",
        "accent-teal",
        "accent-slate",
        "accent-coral",
        "accent-olive",
        "accent-ink"
    ];
}
