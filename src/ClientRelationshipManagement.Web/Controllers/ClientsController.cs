using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.Security.Objects;
using ClientRelationshipManagement.Web.Models.Clients;
using ClientRelationshipManagement.Web.Services.Mail;
using ClientRelationshipManagement.Web.Services.Processes;
using ClientRelationshipManagement.Web.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PlatformEntities = cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace ClientRelationshipManagement.Web.Controllers;

public sealed class ClientsController(
    IPlatformDbContextFactory dbContextFactory,
    IEmailDraftWorkflowService emailDraftWorkflowService,
    IWorkflowAutomationService workflowAutomationService,
    ICRMAuthInfo authInfo,
    ISSOAuthInfo ssoAuthInfo)
    : Controller
{
    public IActionResult Create() => RedirectToAction("Index", "Leads");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ClientEditorViewModel _) => RedirectToAction("Index", "Leads");

    public async Task<IActionResult> Index(
        string search = null,
        string status = null,
        string sort = "name",
        string scope = null,
        string tasks = null)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        await workflowAutomationService.EnsureCoverageAsync();

        using PlatformDbContext context = dbContextFactory.CreateDbContext();
        string[] readableTenantIds = GetReadableTenantIds();
        RelationshipStatus? parsedStatus = Enum.TryParse<RelationshipStatus>(status, true, out RelationshipStatus statusValue)
            ? statusValue
            : null;
        string normalizedScope = scope?.Trim().ToLowerInvariant() ?? string.Empty;
        string taskFilter = NormalizeTaskFilter(tasks);
        DateTimeOffset today = new(DateTime.UtcNow.Date, TimeSpan.Zero);
        DateTimeOffset tomorrow = today.AddDays(1);

        IQueryable<PlatformEntities.TenantCompanyRelationship> relationshipQuery = context.TenantCompanyRelationships
            .AsNoTracking()
            .Include(relationship => relationship.Company)
            .Where(relationship => !relationship.IsArchived
                && readableTenantIds.Contains(relationship.TenantId)
                && relationship.ClientAccounts.Any(account => account.Status != ClientAccountStatus.Closed));

        if (taskFilter.Length > 0)
        {
            relationshipQuery = taskFilter switch
            {
                "due-today" => relationshipQuery.Where(relationship => context.ProcessTasks.Any(task =>
                    task.TenantCompanyRelationshipId == relationship.Id
                    && task.ClientAccountId.HasValue
                    && task.State == ProcessTaskState.Pending
                    && task.DueOn >= today && task.DueOn < tomorrow)),
                "overdue" => relationshipQuery.Where(relationship => context.ProcessTasks.Any(task =>
                    task.TenantCompanyRelationshipId == relationship.Id
                    && task.ClientAccountId.HasValue
                    && task.State == ProcessTaskState.Pending && task.DueOn < today)),
                _ => relationshipQuery.Where(relationship => context.ProcessTasks.Any(task =>
                    task.TenantCompanyRelationshipId == relationship.Id
                    && task.ClientAccountId.HasValue
                    && task.State == ProcessTaskState.Pending))
            };
        }

        List<ClientListProjection> rows = await relationshipQuery
            .Select(relationship => new ClientListProjection
            {
                Id = relationship.Id,
                CompanyName = CompanyNames.ResolvePreferredName(relationship.Company),
                AccountOwner = relationship.AccountOwnerDisplayName ?? string.Empty,
                Status = relationship.Status,
                Stage = relationship.CurrentStage,
                Priority = relationship.Priority,
                LeadSource = relationship.LeadSource ?? string.Empty
            })
            .ToListAsync();

        Dictionary<Guid, (string ActionText, DateTimeOffset? DueOn)> nextTaskLookup = await context.ProcessTasks
            .AsNoTracking()
            .Where(task => task.State == ProcessTaskState.Pending && task.TenantCompanyRelationshipId.HasValue)
            .Where(task => rows.Select(row => row.Id).Contains(task.TenantCompanyRelationshipId!.Value))
            .GroupBy(task => task.TenantCompanyRelationshipId!.Value)
            .ToDictionaryAsync(
                group => group.Key,
                group =>
                {
                    var nextTask = group.OrderBy(item => item.DueOn).ThenBy(item => item.RenderedTitle).First();
                    return (nextTask.RenderedTitle, (DateTimeOffset?)nextTask.DueOn);
                });

        if (!string.IsNullOrWhiteSpace(search))
        {
            string trimmedSearch = search.Trim();
            rows = rows
                .Where(item =>
                    item.CompanyName.Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase)
                    || item.AccountOwner.Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase)
                    || item.LeadSource.Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase)
                    || nextTaskLookup.GetValueOrDefault(item.Id).ActionText?.Contains(trimmedSearch, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
        }

        if (parsedStatus.HasValue)
            rows = rows.Where(item => item.Status == parsedStatus.Value).ToList();

        rows = sort?.ToLowerInvariant() switch
        {
            "owner" => rows.OrderBy(item => item.AccountOwner).ThenBy(item => item.CompanyName).ToList(),
            "status" => rows.OrderBy(item => item.Status).ThenBy(item => item.CompanyName).ToList(),
            "stage" => rows.OrderBy(item => item.Stage).ThenBy(item => item.CompanyName).ToList(),
            "priority" => rows.OrderByDescending(item => item.Priority).ThenBy(item => item.CompanyName).ToList(),
            "due" => rows.OrderBy(item => nextTaskLookup.GetValueOrDefault(item.Id).DueOn == null)
                .ThenBy(item => nextTaskLookup.GetValueOrDefault(item.Id).DueOn)
                .ThenBy(item => item.CompanyName)
                .ToList(),
            _ => rows.OrderBy(item => item.CompanyName).ThenBy(item => item.AccountOwner).ToList()
        };

        return View(new ClientListPageViewModel
        {
            Notice = TempData["ClientsNotice"]?.ToString() ?? string.Empty,
            TotalClients = rows.Count,
            Search = search ?? string.Empty,
            StatusFilter = parsedStatus?.ToString() ?? string.Empty,
            Scope = normalizedScope,
            TaskFilter = taskFilter,
            Sort = sort ?? "name",
            StatusOptions = BuildStatusOptions(parsedStatus?.ToString()),
            SortOptions = BuildSortOptions(sort),
            Clients =
            [
                .. rows.Select(item =>
                {
                    (string actionText, DateTimeOffset? dueOn) = nextTaskLookup.GetValueOrDefault(item.Id);
                    return new ClientListItemViewModel
                    {
                        Id = item.Id,
                        CompanyName = item.CompanyName,
                        AccountOwner = item.AccountOwner,
                        StatusLabel = DisplayText.Humanize(item.Status),
                        StageLabel = DisplayText.Humanize(item.Stage),
                        PriorityLabel = DisplayText.Humanize(item.Priority),
                        LeadSource = item.LeadSource,
                        NextAction = actionText ?? string.Empty,
                        NextActionDueLabel = dueOn == null
                            ? "No due date"
                            : dueOn.Value.LocalDateTime.ToString("dd MMM yyyy")
                    };
                })
            ]
        });
    }

    static string NormalizeTaskFilter(string value) => value?.Trim().ToLowerInvariant() switch
    {
        "due-today" => "due-today",
        "overdue" => "overdue",
        "open" => "open",
        _ => string.Empty
    };

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        await workflowAutomationService.EnsureCoverageAsync();
        ClientEditorViewModel model = await CreateEditorModelAsync(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ClientEditorViewModel model)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (model.Id is null)
            return BadRequest();

        if (!ModelState.IsValid)
            return View(await CreateEditorModelAsync(model.Id.Value, model));

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        string[] writeableTenantIds = GetWriteableTenantIds();
        PlatformEntities.TenantCompanyRelationship relationship = await context.TenantCompanyRelationships
            .Include(item => item.Company)
            .FirstOrDefaultAsync(item => item.Id == model.Id.Value);

        if (relationship is null || !writeableTenantIds.Contains(relationship.TenantId))
            return NotFound();

        PlatformEntities.Company company = relationship.Company;
        PlatformEntities.ClientAccount clientAccount = await context.ClientAccounts
            .Where(item => item.TenantCompanyRelationshipId == relationship.Id && item.Status != ClientAccountStatus.Closed)
            .OrderByDescending(item => item.CreatedOn)
            .FirstOrDefaultAsync();
        PlatformEntities.RelationshipContact primaryContact = await context.RelationshipContacts
            .Include(item => item.CompanyContact)
            .Where(item => item.TenantCompanyRelationshipId == relationship.Id && item.Status == RelationshipContactStatus.Active)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.CreatedOn)
            .FirstOrDefaultAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string currentUser = CurrentUserId;

        company.OfficialName = model.CompanyName.Trim();
        company.TradingName = Normalize(model.TradingName);
        company.ContactEmailAddress = Normalize(model.ContactEmailAddress);
        company.ContactPhoneNumber = Normalize(model.ContactPhoneNumber);
        company.WebsiteUrl = Normalize(model.WebsiteUrl);
        company.ResearchSummary = Normalize(model.ResearchSummary);
        company.VerificationNotes = Normalize(model.DataQualityNotes);
        company.IsVerified = model.IsVerified;
        company.LastUpdatedBy = currentUser;
        company.LastUpdated = now;

        if (primaryContact is not null)
        {
            primaryContact.IsPrimary = true;
            primaryContact.CompanyContact.Name = model.PrimaryContactName.Trim();
            primaryContact.CompanyContact.Position = Normalize(model.PrimaryContactPosition);
            primaryContact.CompanyContact.EmailAddress = Normalize(model.ContactEmailAddress);
            primaryContact.CompanyContact.PhoneNumber = Normalize(model.ContactPhoneNumber);
            primaryContact.CompanyContact.IsPrimary = true;
            primaryContact.CompanyContact.LastUpdatedBy = currentUser;
            primaryContact.CompanyContact.LastUpdated = now;
            primaryContact.LastUpdatedBy = currentUser;
            primaryContact.LastUpdated = now;
        }

        if (clientAccount is not null)
        {
            clientAccount.Status = model.ClientAccountStatus;
            clientAccount.AccountReference = Normalize(model.AccountReference);
            clientAccount.ContractSignedOn = ToDateTimeOffset(model.ContractSignedOn);
            clientAccount.GoLiveOn = ToDateTimeOffset(model.GoLiveOn);
            clientAccount.LastUpdatedBy = currentUser;
            clientAccount.LastUpdated = now;
        }

        relationship.AccountOwnerDisplayName = Normalize(model.AccountOwner);
        relationship.AccountOwnerUserId ??= currentUser;
        relationship.Status = model.Status;
        relationship.CurrentStage = model.CurrentStage;
        relationship.Priority = model.Priority;
        relationship.LeadSource = Normalize(model.LeadSource);
        relationship.InitialRoute = Normalize(model.InitialRoute);
        relationship.FitScore = model.FitScore;
        relationship.OpportunitySummary = Normalize(model.OpportunitySummary);
        relationship.PreferredOpeningAngle = Normalize(model.PreferredOpeningAngle);
        relationship.ResearchSummary = Normalize(model.ResearchSummary);
        relationship.DataQualityNotes = Normalize(model.DataQualityNotes);
        relationship.IsArchived = model.IsArchived;
        relationship.LastUpdatedBy = currentUser;
        relationship.LastUpdated = now;

        await context.SaveChangesAsync();

        TempData["ClientsNotice"] = "Client workspace updated.";
        return RedirectToAction(nameof(Edit), new { id = model.Id.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordActivity(RecordClientActivityRequest request)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (request.ClientId == Guid.Empty)
            return RedirectToAction(nameof(Index));

        if (!TryValidateModel(request))
        {
            TempData["ClientWorkspaceNotice"] = "Add both a summary and an outcome to record activity.";
            return RedirectToAction(nameof(Edit), new { id = request.ClientId });
        }

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        PlatformEntities.TenantCompanyRelationship relationship = await context.TenantCompanyRelationships
            .FirstOrDefaultAsync(item => item.Id == request.ClientId);

        if (relationship is null || !GetWriteableTenantIds().Contains(relationship.TenantId))
            return NotFound();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        context.Activities.Add(new PlatformEntities.Activity
        {
            Id = Guid.NewGuid(),
            TenantCompanyRelationshipId = request.ClientId,
            OpportunityId = request.ClientOpportunityId,
            ClientAccountId = request.ClientAccountId,
            ActivityOn = ToDateTimeOffset(request.ActivityOn) ?? now,
            Type = request.Type,
            Direction = request.Direction,
            Summary = request.Summary.Trim(),
            Outcome = request.Outcome.Trim(),
            NextAction = Normalize(request.NextAction),
            NextActionDueOn = ToDateTimeOffset(request.NextActionDueOn),
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        });

        await context.SaveChangesAsync();

        TempData["ClientWorkspaceNotice"] = "Activity recorded.";
        return RedirectToAction(nameof(Edit), new { id = request.ClientId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddOpportunity(CreateClientOpportunityRequest request)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (request.ClientId == Guid.Empty)
            return RedirectToAction(nameof(Index));

        if (!TryValidateModel(request))
        {
            TempData["ClientWorkspaceNotice"] = "Add at least the opportunity pain summary before creating a pipeline item.";
            return RedirectToAction(nameof(Edit), new { id = request.ClientId });
        }

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        PlatformEntities.TenantCompanyRelationship relationship = await context.TenantCompanyRelationships
            .FirstOrDefaultAsync(item => item.Id == request.ClientId);

        if (relationship is null || !GetWriteableTenantIds().Contains(relationship.TenantId))
            return NotFound();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        PlatformEntities.Opportunity opportunity = new()
        {
            Id = Guid.NewGuid(),
            TenantCompanyRelationshipId = request.ClientId,
            Type = request.Type,
            Stage = request.Stage,
            EstimatedAnnualValue = request.EstimatedAnnualValue,
            Probability = request.Probability,
            PainSummary = request.PainSummary.Trim(),
            ValueHypothesis = Normalize(request.ValueHypothesis),
            DecisionProcess = Normalize(request.DecisionProcess),
            CreatedBy = CurrentUserId,
            LastUpdatedBy = CurrentUserId,
            CreatedOn = now,
            LastUpdated = now
        };

        context.Opportunities.Add(opportunity);
        await context.SaveChangesAsync();

        await workflowAutomationService.EnsureCoverageAsync(opportunityId: opportunity.Id, forceCreate: true);

        TempData["ClientWorkspaceNotice"] = "Opportunity added to the pipeline.";
        return RedirectToAction(nameof(Edit), new { id = request.ClientId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEmailDraft(DraftClientEmailRequest request)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (request.ClientId == Guid.Empty)
            return RedirectToAction(nameof(Index));

        if (!TryValidateModel(request))
        {
            TempData["ClientWorkspaceNotice"] = "Add both an email subject and the draft body before saving.";
            return RedirectToAction(nameof(Edit), new { id = request.ClientId });
        }

        var email = await emailDraftWorkflowService.SaveDraftAsync(new EmailDraftUpsertCommand
        {
            ClientId = request.ClientId,
            EmailId = request.EmailId,
            ClientMaterialId = request.ClientMaterialId,
            ClientOpportunityId = request.ClientOpportunityId,
            ClientAccountId = request.ClientAccountId,
            ActivityOn = ToDateTimeOffset(request.ActivityOn),
            Direction = request.Direction,
            Subject = request.Subject,
            Body = request.Body,
            NextAction = request.NextAction,
            NextActionDueOn = ToDateTimeOffset(request.NextActionDueOn),
            ToAddresses = request.ToAddresses,
            CcAddresses = request.CcAddresses,
            BccAddresses = request.BccAddresses,
            ScheduledSendTimeUtc = ToDateTimeOffset(request.ScheduledSendOn),
        });

        if (email is null)
            return NotFound();

        TempData["ClientWorkspaceNotice"] = request.EmailId.HasValue || request.ClientMaterialId.HasValue
            ? "Email draft updated."
            : "Email draft saved.";
        return RedirectToAction(nameof(Edit), new { id = request.ClientId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveEmail(ApproveClientEmailRequest request)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (request.ClientId == Guid.Empty || request.EmailId == Guid.Empty)
            return RedirectToAction(nameof(Index));

        var email = await emailDraftWorkflowService.ApproveAsync(
            request.ClientId,
            request.EmailId,
            ToDateTimeOffset(request.ScheduledSendOn));

        TempData["ClientWorkspaceNotice"] = email is null
            ? "That email could not be approved."
            : "Email approved for sending.";

        return RedirectToAction(nameof(Edit), new { id = request.ClientId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkEmailSent(MarkClientEmailSentRequest request)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (request.ClientId == Guid.Empty || request.EmailId == Guid.Empty)
            return RedirectToAction(nameof(Index));

        var email = await emailDraftWorkflowService.MarkSentAsync(request.ClientId, request.EmailId);
        if (email is not null)
            await workflowAutomationService.CompleteEmailTaskAsync(request.EmailId);

        TempData["ClientWorkspaceNotice"] = email is null
            ? "That email could not be marked as sent."
            : "Email marked as sent.";

        return RedirectToAction(nameof(Edit), new { id = request.ClientId });
    }

    IActionResult RedirectIfUnauthenticated()
    {
        if (!string.IsNullOrWhiteSpace(ssoAuthInfo?.SSOUserId)
            && !string.Equals(ssoAuthInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            return null;

        string returnUrl = $"{Request.Path}{Request.QueryString}";
        return RedirectToAction("Login", "Account", new { returnUrl });
    }

    async Task<ClientEditorViewModel> CreateEditorModelAsync(Guid relationshipId, ClientEditorViewModel postedModel = null)
    {
        using PlatformDbContext context = dbContextFactory.CreateDbContext();
        string[] readableTenantIds = GetReadableTenantIds();

        PlatformEntities.TenantCompanyRelationship relationship = await context.TenantCompanyRelationships
            .AsNoTracking()
            .Include(item => item.Company)
            .FirstOrDefaultAsync(item => item.Id == relationshipId);

        if (relationship is null || !readableTenantIds.Contains(relationship.TenantId))
            return null;

        PlatformEntities.ClientAccount clientAccount = await context.ClientAccounts
            .AsNoTracking()
            .Where(item => item.TenantCompanyRelationshipId == relationshipId && item.Status != ClientAccountStatus.Closed)
            .OrderByDescending(item => item.CreatedOn)
            .FirstOrDefaultAsync();
        PlatformEntities.RelationshipContact primaryContact = await context.RelationshipContacts
            .AsNoTracking()
            .Include(item => item.CompanyContact)
            .Where(item => item.TenantCompanyRelationshipId == relationshipId && item.Status == RelationshipContactStatus.Active)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.CreatedOn)
            .FirstOrDefaultAsync();

        List<PlatformEntities.Activity> activities = await context.Activities
            .AsNoTracking()
            .Where(activity => activity.TenantCompanyRelationshipId == relationshipId)
            .OrderByDescending(activity => activity.ActivityOn)
            .Take(20)
            .ToListAsync();

        List<Guid> materialIds =
        [
            .. activities.Where(item => item.MaterialId.HasValue).Select(item => item.MaterialId!.Value).Distinct()
        ];

        List<PlatformEntities.Material> materials = materialIds.Count == 0
            ? []
            : await context.Materials
                .AsNoTracking()
                .Where(material => materialIds.Contains(material.Id))
                .ToListAsync();

        Dictionary<Guid, PlatformEntities.Email> emailLookup = materialIds.Count == 0
            ? new Dictionary<Guid, PlatformEntities.Email>()
            : await context.Emails
                .AsNoTracking()
                .Where(email => email.MaterialId.HasValue && materialIds.Contains(email.MaterialId.Value))
                .ToDictionaryAsync(email => email.MaterialId!.Value);

        List<ClientCommunicationItemViewModel> communications =
        [
            .. activities
                .Where(activity => CommunicationTypes.Contains(activity.Type))
                .Select(activity =>
                {
                    PlatformEntities.Material material = activity.MaterialId.HasValue
                        ? materials.FirstOrDefault(item => item.Id == activity.MaterialId.Value)
                        : null;
                    PlatformEntities.Email email = activity.MaterialId.HasValue
                        ? emailLookup.GetValueOrDefault(activity.MaterialId.Value)
                        : null;
                    bool isDraftEmail = activity.Type == ActivityType.Email
                        && email is not null
                        && email.State is not EmailState.Sent and not EmailState.Sending;

                    return new ClientCommunicationItemViewModel
                    {
                        ActivityId = activity.Id,
                        EmailId = email?.Id,
                        MaterialId = material?.Id,
                        WhenLabel = activity.ActivityOn.LocalDateTime.ToString("dd MMM yyyy HH:mm"),
                        TypeLabel = DisplayText.Humanize(activity.Type),
                        DirectionLabel = DisplayText.Humanize(activity.Direction),
                        DirectionValue = activity.Direction.ToString(),
                        StatusLabel = email is not null
                            ? DisplayText.Humanize(email.State)
                            : material is not null
                                ? DisplayText.Humanize(material.Status)
                                : string.Empty,
                        ToAddresses = email?.ToAddresses ?? string.Empty,
                        CcAddresses = email?.CcAddresses ?? string.Empty,
                        BccAddresses = email?.BccAddresses ?? string.Empty,
                        ScheduledSendOnLabel = email?.ScheduledSendTimeUtc == null
                            ? string.Empty
                            : email.ScheduledSendTimeUtc.Value.LocalDateTime.ToString("dd MMM yyyy HH:mm"),
                        ScheduledSendOnValue = email?.ScheduledSendTimeUtc == null
                            ? string.Empty
                            : email.ScheduledSendTimeUtc.Value.LocalDateTime.ToString("yyyy-MM-ddTHH:mm"),
                        Title = email?.Subject ?? material?.Name ?? activity.Summary ?? string.Empty,
                        Content = FirstNonEmpty(email?.BodyText, email?.BodyHtml, material?.Notes, activity.Outcome),
                        NextAction = activity.NextAction ?? string.Empty,
                        NextActionDueLabel = activity.NextActionDueOn == null
                            ? string.Empty
                            : activity.NextActionDueOn.Value.LocalDateTime.ToString("dd MMM yyyy"),
                        NextActionDueValue = activity.NextActionDueOn == null
                            ? string.Empty
                            : activity.NextActionDueOn.Value.LocalDateTime.ToString("yyyy-MM-dd"),
                        ClientOpportunityId = activity.OpportunityId,
                        IsDraftEmail = isDraftEmail
                    };
                })
                .ToList()
        ];

        List<ClientActivityTimelineItemViewModel> recentActivities =
        [
            .. activities.Select(activity => new ClientActivityTimelineItemViewModel
            {
                WhenLabel = activity.ActivityOn.LocalDateTime.ToString("dd MMM yyyy HH:mm"),
                TypeLabel = DisplayText.Humanize(activity.Type),
                DirectionLabel = DisplayText.Humanize(activity.Direction),
                Summary = activity.Summary ?? string.Empty,
                Outcome = activity.Outcome ?? string.Empty,
                NextAction = activity.NextAction ?? string.Empty,
                NextActionDueLabel = activity.NextActionDueOn == null
                    ? string.Empty
                    : activity.NextActionDueOn.Value.LocalDateTime.ToString("dd MMM yyyy"),
                OpportunityStageLabel = string.Empty
            })
        ];

        List<ClientOpportunitySummaryViewModel> opportunities = await context.Opportunities
            .AsNoTracking()
            .Where(opportunity => opportunity.TenantCompanyRelationshipId == relationshipId)
            .OrderBy(opportunity => opportunity.Stage)
            .ThenByDescending(opportunity => opportunity.CreatedOn)
            .Select(opportunity => new ClientOpportunitySummaryViewModel
            {
                Id = opportunity.Id,
                TypeLabel = DisplayText.Humanize(opportunity.Type),
                StageLabel = DisplayText.Humanize(opportunity.Stage),
                EstimatedAnnualValueLabel = opportunity.EstimatedAnnualValue.HasValue
                    ? opportunity.EstimatedAnnualValue.Value.ToString("C0")
                    : "Not set",
                ProbabilityLabel = opportunity.Probability.HasValue
                    ? $"{opportunity.Probability:0}%"
                    : "Not set",
                PainSummary = opportunity.PainSummary ?? string.Empty,
                ValueHypothesis = opportunity.ValueHypothesis ?? string.Empty,
                NextAction = string.Empty,
                NextActionDueLabel = string.Empty
            })
            .ToListAsync();

        List<ClientScheduledActionItemViewModel> scheduledActions = await context.ProcessTasks
            .AsNoTracking()
            .Where(task => task.TenantCompanyRelationshipId == relationshipId && task.State == ProcessTaskState.Pending)
            .OrderBy(task => task.DueOn)
            .ThenBy(task => task.RenderedTitle)
            .Take(8)
            .Select(task => new ClientScheduledActionItemViewModel
            {
                TypeLabel = "Process",
                SourceLabel = DisplayText.Humanize(task.ActionType),
                ActionText = task.RenderedTitle,
                DueLabel = task.DueOn.LocalDateTime.ToString("dd MMM yyyy"),
                SortOn = task.DueOn.LocalDateTime,
                SourcePriority = 4
            })
            .ToListAsync();

        ClientEditorViewModel baseModel = postedModel ?? new ClientEditorViewModel();

        return new ClientEditorViewModel
        {
            Id = relationshipId,
            IsNew = false,
            Notice = TempData["ClientWorkspaceNotice"]?.ToString() ?? string.Empty,
            FormTitle = "Client Details",
            SubmitLabel = "Save changes",
            CompanyName = postedModel?.CompanyName ?? CompanyNames.ResolvePreferredName(relationship.Company),
            TradingName = postedModel?.TradingName ?? relationship.Company.TradingName ?? string.Empty,
            ContactEmailAddress = postedModel?.ContactEmailAddress ?? primaryContact?.CompanyContact.EmailAddress ?? relationship.Company.ContactEmailAddress ?? string.Empty,
            ContactPhoneNumber = postedModel?.ContactPhoneNumber ?? primaryContact?.CompanyContact.PhoneNumber ?? relationship.Company.ContactPhoneNumber ?? string.Empty,
            PrimaryContactName = postedModel?.PrimaryContactName ?? primaryContact?.CompanyContact.Name ?? string.Empty,
            PrimaryContactPosition = postedModel?.PrimaryContactPosition ?? primaryContact?.CompanyContact.Position ?? string.Empty,
            WebsiteUrl = postedModel?.WebsiteUrl ?? relationship.Company.WebsiteUrl ?? string.Empty,
            AccountOwner = postedModel?.AccountOwner ?? relationship.AccountOwnerDisplayName ?? string.Empty,
            LeadSource = postedModel?.LeadSource ?? relationship.LeadSource ?? string.Empty,
            InitialRoute = postedModel?.InitialRoute ?? relationship.InitialRoute ?? string.Empty,
            OpportunitySummary = postedModel?.OpportunitySummary ?? relationship.OpportunitySummary ?? string.Empty,
            ResearchSummary = postedModel?.ResearchSummary ?? FirstNonEmpty(relationship.ResearchSummary, relationship.Company.ResearchSummary),
            DataQualityNotes = postedModel?.DataQualityNotes ?? FirstNonEmpty(relationship.DataQualityNotes, relationship.Company.VerificationNotes),
            PreferredOpeningAngle = postedModel?.PreferredOpeningAngle ?? relationship.PreferredOpeningAngle ?? string.Empty,
            FitScore = postedModel?.FitScore ?? relationship.FitScore,
            IsArchived = postedModel?.IsArchived ?? relationship.IsArchived,
            IsVerified = postedModel?.IsVerified ?? relationship.Company.IsVerified,
            Status = postedModel?.Status ?? relationship.Status,
            CurrentStage = postedModel?.CurrentStage ?? relationship.CurrentStage,
            Priority = postedModel?.Priority ?? relationship.Priority,
            ClientAccountStatus = postedModel?.ClientAccountStatus ?? clientAccount?.Status ?? ClientAccountStatus.Onboarding,
            AccountReference = postedModel?.AccountReference ?? clientAccount?.AccountReference ?? string.Empty,
            ContractSignedOn = postedModel?.ContractSignedOn ?? clientAccount?.ContractSignedOn?.LocalDateTime.Date,
            GoLiveOn = postedModel?.GoLiveOn ?? clientAccount?.GoLiveOn?.LocalDateTime.Date,
            StatusOptions = Enum.GetValues<RelationshipStatus>()
                .Select(status => new SelectListItem(DisplayText.Humanize(status), status.ToString()))
                .ToList(),
            StageOptions = Enum.GetValues<SalesPipelineStage>()
                .Select(stage => new SelectListItem(DisplayText.Humanize(stage), stage.ToString()))
                .ToList(),
            PriorityOptions = Enum.GetValues<RelationshipPriority>()
                .Select(priority => new SelectListItem(DisplayText.Humanize(priority), priority.ToString()))
                .ToList(),
            ClientAccountStatusOptions = Enum.GetValues<ClientAccountStatus>()
                .Where(status => status != ClientAccountStatus.Closed)
                .Select(status => new SelectListItem(DisplayText.Humanize(status), status.ToString()))
                .ToList(),
            ActivityTypeOptions = Enum.GetValues<ActivityType>()
                .Select(type => new SelectListItem(DisplayText.Humanize(type), type.ToString()))
                .ToList(),
            ActivityDirectionOptions = Enum.GetValues<ActivityDirection>()
                .Select(direction => new SelectListItem(DisplayText.Humanize(direction), direction.ToString()))
                .ToList(),
            OpportunityTypeOptions = Enum.GetValues<OpportunityType>()
                .Select(type => new SelectListItem(DisplayText.Humanize(type), type.ToString()))
                .ToList(),
            RecentActivities = recentActivities,
            Communications = communications,
            Opportunities = opportunities,
            ScheduledActions = scheduledActions
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

    string[] GetWriteableTenantIds()
    {
        if (authInfo.WriteableTenants.Length > 0)
            return authInfo.WriteableTenants;

        return GetReadableTenantIds();
    }

    string CurrentUserId =>
        string.IsNullOrWhiteSpace(ssoAuthInfo?.SSOUserId) || string.Equals(ssoAuthInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase)
            ? "system"
            : ssoAuthInfo.SSOUserId;

    static DateTimeOffset? ToDateTimeOffset(DateTime? value)
    {
        if (value is null)
            return null;

        DateTime localTime = DateTime.SpecifyKind(value.Value, DateTimeKind.Local);
        return new DateTimeOffset(localTime).ToUniversalTime();
    }

    static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    static IReadOnlyList<SelectListItem> BuildStatusOptions(string selectedValue) =>
    [
        new("All states", string.Empty, string.IsNullOrWhiteSpace(selectedValue)),
        .. Enum.GetValues<RelationshipStatus>()
            .Select(status => new SelectListItem(
                DisplayText.Humanize(status),
                status.ToString(),
                string.Equals(selectedValue, status.ToString(), StringComparison.OrdinalIgnoreCase)))
    ];

    static IReadOnlyList<SelectListItem> BuildSortOptions(string selectedValue)
    {
        string normalized = string.IsNullOrWhiteSpace(selectedValue) ? "name" : selectedValue;
        return
        [
            new("Client name", "name", normalized.Equals("name", StringComparison.OrdinalIgnoreCase)),
            new("Owner", "owner", normalized.Equals("owner", StringComparison.OrdinalIgnoreCase)),
            new("Status", "status", normalized.Equals("status", StringComparison.OrdinalIgnoreCase)),
            new("Stage", "stage", normalized.Equals("stage", StringComparison.OrdinalIgnoreCase)),
            new("Priority", "priority", normalized.Equals("priority", StringComparison.OrdinalIgnoreCase)),
            new("Next action due", "due", normalized.Equals("due", StringComparison.OrdinalIgnoreCase))
        ];
    }

    sealed class ClientListProjection
    {
        public Guid Id { get; init; }
        public string CompanyName { get; init; }
        public string AccountOwner { get; init; }
        public RelationshipStatus Status { get; init; }
        public SalesPipelineStage Stage { get; init; }
        public RelationshipPriority Priority { get; init; }
        public string LeadSource { get; init; }
    }

    static ActivityType[] CommunicationTypes =>
    [
        ActivityType.Email,
        ActivityType.PhoneCall,
        ActivityType.Meeting,
        ActivityType.Demo
    ];
}
