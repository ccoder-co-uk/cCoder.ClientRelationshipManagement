using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.Security.Objects;
using ClientRelationshipManagement.Web.Models.Opportunities;
using ClientRelationshipManagement.Web.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PlatformEntities = cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace ClientRelationshipManagement.Web.Controllers;

public sealed class OpportunitiesController(
    IPlatformDbContextFactory dbContextFactory,
    ICRMAuthInfo authInfo,
    ISSOAuthInfo ssoAuthInfo)
    : Controller
{
    public async Task<IActionResult> Index(
        string search = null,
        string stage = null,
        string sort = "due",
        string status = null,
        string scope = null,
        string tasks = null)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        using PlatformDbContext context = dbContextFactory.CreateDbContext();
        string[] readableTenantIds = GetReadableTenantIds();
        SalesPipelineStage? parsedStage = Enum.TryParse<SalesPipelineStage>(stage, true, out SalesPipelineStage stageValue)
            ? stageValue
            : null;
        RelationshipStatus? parsedStatus = Enum.TryParse<RelationshipStatus>(status, true, out RelationshipStatus statusValue)
            ? statusValue
            : null;
        string normalizedScope = scope?.Trim().ToLowerInvariant() ?? string.Empty;
        string taskFilter = NormalizeTaskFilter(tasks);
        DateTimeOffset today = new(DateTime.UtcNow.Date, TimeSpan.Zero);
        DateTimeOffset tomorrow = today.AddDays(1);

        IQueryable<PlatformEntities.Opportunity> opportunityQuery = context.Opportunities
            .AsNoTracking()
            .Include(item => item.TenantCompanyRelationship)
                .ThenInclude(relationship => relationship.Company)
            .Include(item => item.PrimaryRelationshipContact)
                .ThenInclude(contact => contact.CompanyContact)
            .Where(item => readableTenantIds.Contains(item.TenantCompanyRelationship.TenantId));

        if (normalizedScope == "active")
            opportunityQuery = opportunityQuery.Where(item => item.Stage != SalesPipelineStage.Won && item.Stage != SalesPipelineStage.Lost);

        if (parsedStatus.HasValue)
            opportunityQuery = opportunityQuery.Where(item => item.TenantCompanyRelationship.Status == parsedStatus.Value);

        if (taskFilter.Length > 0)
        {
            opportunityQuery = taskFilter switch
            {
                "due-today" => opportunityQuery.Where(item => context.ProcessTasks.Any(task =>
                    task.OpportunityId == item.Id && task.State == ProcessTaskState.Pending
                    && task.DueOn >= today && task.DueOn < tomorrow)),
                "overdue" => opportunityQuery.Where(item => context.ProcessTasks.Any(task =>
                    task.OpportunityId == item.Id && task.State == ProcessTaskState.Pending && task.DueOn < today)),
                _ => opportunityQuery.Where(item => context.ProcessTasks.Any(task =>
                    task.OpportunityId == item.Id && task.State == ProcessTaskState.Pending))
            };
        }

        List<OpportunityProjection> rows = await opportunityQuery
            .Select(item => new OpportunityProjection
            {
                Id = item.Id,
                ClientId = item.TenantCompanyRelationshipId,
                CompanyName = CompanyNames.ResolvePreferredName(item.TenantCompanyRelationship.Company),
                RelationshipStatus = item.TenantCompanyRelationship.Status,
                Stage = item.Stage,
                Type = item.Type,
                PrimaryContactName = item.PrimaryRelationshipContact != null
                    ? item.PrimaryRelationshipContact.CompanyContact.Name
                    : string.Empty,
                EstimatedAnnualValue = item.EstimatedAnnualValue,
                Probability = item.Probability
            })
            .ToListAsync();

        Dictionary<Guid, (string ActionText, DateTimeOffset? DueOn)> nextTaskLookup = await context.ProcessTasks
            .AsNoTracking()
            .Where(task => task.State == ProcessTaskState.Pending && task.OpportunityId.HasValue)
            .Where(task => rows.Select(row => row.Id).Contains(task.OpportunityId!.Value))
            .GroupBy(task => task.OpportunityId!.Value)
            .ToDictionaryAsync(
                group => group.Key,
                group =>
                {
                    PlatformEntities.ProcessTask nextTask = group.OrderBy(item => item.DueOn).ThenBy(item => item.RenderedTitle).First();
                    return (nextTask.RenderedTitle, (DateTimeOffset?)nextTask.DueOn);
                });

        if (parsedStage.HasValue)
            rows = rows.Where(item => item.Stage == parsedStage.Value).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string trimmedSearch = search.Trim();
            rows = rows.Where(item =>
                    item.CompanyName.Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase)
                    || item.PrimaryContactName.Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase)
                    || DisplayText.Humanize(item.Type).Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase)
                    || nextTaskLookup.GetValueOrDefault(item.Id).ActionText?.Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
        }

        rows = sort?.ToLowerInvariant() switch
        {
            "name" => rows.OrderBy(item => item.CompanyName).ThenBy(item => item.Stage).ToList(),
            "stage" => rows.OrderBy(item => item.Stage).ThenBy(item => item.CompanyName).ToList(),
            "value" => rows.OrderByDescending(item => item.EstimatedAnnualValue ?? decimal.MinValue).ThenBy(item => item.CompanyName).ToList(),
            "probability" => rows.OrderByDescending(item => item.Probability ?? decimal.MinValue).ThenBy(item => item.CompanyName).ToList(),
            _ => rows.OrderBy(item => nextTaskLookup.GetValueOrDefault(item.Id).DueOn == null)
                .ThenBy(item => nextTaskLookup.GetValueOrDefault(item.Id).DueOn)
                .ThenBy(item => item.CompanyName)
                .ToList()
        };

        return View(new OpportunitiesPageViewModel
        {
            Notice = TempData["OpportunitiesNotice"]?.ToString() ?? string.Empty,
            TotalOpportunities = rows.Count,
            Search = search ?? string.Empty,
            StageFilter = parsedStage?.ToString() ?? string.Empty,
            StatusFilter = parsedStatus?.ToString() ?? string.Empty,
            Scope = normalizedScope,
            TaskFilter = taskFilter,
            Sort = sort ?? "due",
            StageOptions = BuildStageOptions(parsedStage?.ToString()),
            StatusOptions = BuildStatusOptions(parsedStatus?.ToString()),
            SortOptions = BuildSortOptions(sort),
            Opportunities =
            [
                .. rows.Select(item =>
                {
                    (string actionText, DateTimeOffset? dueOn) = nextTaskLookup.GetValueOrDefault(item.Id);
                    return new OpportunityListItemViewModel
                    {
                        Id = item.Id,
                        ClientId = item.ClientId,
                        CompanyName = item.CompanyName,
                        RelationshipStatusLabel = DisplayText.Humanize(item.RelationshipStatus),
                        StageLabel = DisplayText.Humanize(item.Stage),
                        TypeLabel = DisplayText.Humanize(item.Type),
                        PrimaryContactName = string.IsNullOrWhiteSpace(item.PrimaryContactName) ? "Not set" : item.PrimaryContactName,
                        EstimatedAnnualValueLabel = item.EstimatedAnnualValue.HasValue ? item.EstimatedAnnualValue.Value.ToString("C0") : "Not set",
                        ProbabilityLabel = item.Probability.HasValue ? $"{item.Probability.Value:0}%" : "Not set",
                        NextAction = actionText ?? string.Empty,
                        NextActionDueLabel = dueOn == null ? "No due date" : dueOn.Value.LocalDateTime.ToString("dd MMM yyyy")
                    };
                })
            ]
        });
    }

    [HttpGet("/Opportunities/{id:guid}/Details")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        string[] tenantIds = GetReadableTenantIds();
        using PlatformDbContext context = dbContextFactory.CreateDbContext();
        PlatformEntities.Opportunity opportunity = await context.Opportunities
            .AsNoTracking()
            .Include(item => item.TenantCompanyRelationship)
                .ThenInclude(item => item.Company)
            .Include(item => item.PrimaryRelationshipContact)
                .ThenInclude(item => item.CompanyContact)
            .SingleOrDefaultAsync(item => item.Id == id
                && tenantIds.Contains(item.TenantCompanyRelationship.TenantId), cancellationToken);
        if (opportunity is null)
            return NotFound();

        Guid relationshipId = opportunity.TenantCompanyRelationshipId;
        string tenantId = opportunity.TenantCompanyRelationship.TenantId;
        List<OpportunityDetailEvidenceViewModel> leadEvidence = await context.Leads
            .AsNoTracking()
            .Where(item => item.OpportunityId == id || item.TenantCompanyRelationshipId == relationshipId)
            .OrderByDescending(item => item.CreatedOn)
            .Select(item => new OpportunityDetailEvidenceViewModel
            {
                Status = item.Status.ToString(),
                Notes = item.QualificationNotes ?? string.Empty
            })
            .ToListAsync(cancellationToken);
        List<OpportunityDetailActivityViewModel> activities = await context.Activities
            .AsNoTracking()
            .Where(item => item.OpportunityId == id)
            .OrderByDescending(item => item.ActivityOn)
            .Take(100)
            .Select(item => new OpportunityDetailActivityViewModel
            {
                OccurredOn = item.ActivityOn,
                Type = item.Type.ToString(),
                Summary = item.Summary ?? string.Empty,
                Outcome = item.Outcome ?? string.Empty
            })
            .ToListAsync(cancellationToken);
        List<OpportunityDetailTaskViewModel> tasks = await context.ProcessTasks
            .AsNoTracking()
            .Where(item => item.OpportunityId == id)
            .OrderBy(item => item.DueOn)
            .Select(item => new OpportunityDetailTaskViewModel
            {
                Step = item.ProcessStep.Key,
                Title = item.RenderedTitle,
                State = item.State.ToString(),
                Outcome = item.CompletionOutcomeKey ?? string.Empty,
                Notes = item.CompletionNotes ?? string.Empty,
                DueOn = item.DueOn
            })
            .ToListAsync(cancellationToken);
        List<OpportunityDetailArtifactViewModel> artifacts = await context.CompanyHistory
            .AsNoTracking()
            .Where(item => item.CompanyId == opportunity.TenantCompanyRelationship.CompanyId
                && item.TenantId == tenantId)
            .OrderByDescending(item => item.OccurredOn)
            .Take(100)
            .Select(item => new OpportunityDetailArtifactViewModel
            {
                OccurredOn = item.OccurredOn,
                EventType = item.EventType,
                Summary = item.Summary,
                Details = item.Details ?? string.Empty,
                Confidence = item.Confidence ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        return PartialView("_Details", new OpportunityDetailsViewModel
        {
            Id = opportunity.Id,
            RelationshipId = relationshipId,
            CompanyName = CompanyNames.ResolvePreferredName(opportunity.TenantCompanyRelationship.Company),
            CompanyNumber = opportunity.TenantCompanyRelationship.Company.CompanyNumber ?? string.Empty,
            RelationshipStatus = DisplayText.Humanize(opportunity.TenantCompanyRelationship.Status),
            Stage = DisplayText.Humanize(opportunity.Stage),
            PrimaryContact = opportunity.PrimaryRelationshipContact?.CompanyContact?.Name ?? string.Empty,
            EstimatedValue = opportunity.EstimatedAnnualValue?.ToString("C0") ?? "Not set",
            Probability = opportunity.Probability.HasValue ? $"{opportunity.Probability:0}%" : "Not set",
            OpportunitySummary = opportunity.TenantCompanyRelationship.OpportunitySummary ?? string.Empty,
            PainSummary = opportunity.PainSummary ?? string.Empty,
            ValueHypothesis = opportunity.ValueHypothesis ?? string.Empty,
            DecisionProcess = opportunity.DecisionProcess ?? string.Empty,
            LeadEvidence = leadEvidence,
            Activities = activities,
            Tasks = tasks,
            Artifacts = artifacts
        });
    }

    static string NormalizeTaskFilter(string value) => value?.Trim().ToLowerInvariant() switch
    {
        "due-today" => "due-today",
        "overdue" => "overdue",
        "open" => "open",
        _ => string.Empty
    };

    IActionResult RedirectIfUnauthenticated()
    {
        if (!string.IsNullOrWhiteSpace(ssoAuthInfo?.SSOUserId)
            && !string.Equals(ssoAuthInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            return null;

        string returnUrl = $"{Request.Path}{Request.QueryString}";
        return RedirectToAction("Login", "Account", new { returnUrl });
    }

    string[] GetReadableTenantIds()
    {
        if (authInfo.WriteableTenants.Length > 0)
            return authInfo.WriteableTenants;

        if (authInfo.ReadableTenants.Length > 0)
            return authInfo.ReadableTenants;

        return ["default"];
    }

    static IReadOnlyList<SelectListItem> BuildStageOptions(string selectedValue) =>
    [
        new("All stages", string.Empty, string.IsNullOrWhiteSpace(selectedValue)),
        .. Enum.GetValues<SalesPipelineStage>()
            .Select(stage => new SelectListItem(
                DisplayText.Humanize(stage),
                stage.ToString(),
                string.Equals(selectedValue, stage.ToString(), StringComparison.OrdinalIgnoreCase)))
    ];

    static IReadOnlyList<SelectListItem> BuildStatusOptions(string selectedValue) =>
    [
        new("All relationship states", string.Empty, string.IsNullOrWhiteSpace(selectedValue)),
        .. Enum.GetValues<RelationshipStatus>()
            .Select(status => new SelectListItem(
                DisplayText.Humanize(status),
                status.ToString(),
                string.Equals(selectedValue, status.ToString(), StringComparison.OrdinalIgnoreCase)))
    ];

    static IReadOnlyList<SelectListItem> BuildSortOptions(string selectedValue)
    {
        string normalized = string.IsNullOrWhiteSpace(selectedValue) ? "due" : selectedValue;
        return
        [
            new("Next action due", "due", normalized.Equals("due", StringComparison.OrdinalIgnoreCase)),
            new("Company name", "name", normalized.Equals("name", StringComparison.OrdinalIgnoreCase)),
            new("Stage", "stage", normalized.Equals("stage", StringComparison.OrdinalIgnoreCase)),
            new("Estimated value", "value", normalized.Equals("value", StringComparison.OrdinalIgnoreCase)),
            new("Probability", "probability", normalized.Equals("probability", StringComparison.OrdinalIgnoreCase))
        ];
    }

    sealed class OpportunityProjection
    {
        public Guid Id { get; init; }
        public Guid ClientId { get; init; }
        public string CompanyName { get; init; } = string.Empty;
        public RelationshipStatus RelationshipStatus { get; init; }
        public SalesPipelineStage Stage { get; init; }
        public OpportunityType Type { get; init; }
        public string PrimaryContactName { get; init; } = string.Empty;
        public decimal? EstimatedAnnualValue { get; init; }
        public decimal? Probability { get; init; }
    }
}
