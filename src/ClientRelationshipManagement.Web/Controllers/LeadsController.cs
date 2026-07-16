using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.Security.Objects;
using ClientRelationshipManagement.Web.Models.Leads;
using ClientRelationshipManagement.Web.Services.Leads;
using ClientRelationshipManagement.Web.Services.Processes;
using ClientRelationshipManagement.Web.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PlatformEntities = cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace ClientRelationshipManagement.Web.Controllers;

public sealed class LeadsController(
    IPlatformDbContextFactory dbContextFactory,
    ILeadIngestionService leadIngestionService,
    IWorkflowAutomationService workflowAutomationService,
    ICRMAuthInfo authInfo,
    ISSOAuthInfo ssoAuthInfo)
    : Controller
{
    public async Task<IActionResult> Index(
        string search = null,
        string status = null,
        string scope = null,
        string tasks = null)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        await workflowAutomationService.EnsureCoverageAsync();

        using PlatformDbContext context = dbContextFactory.CreateDbContext();
        string[] readableTenantIds = GetReadableTenantIds();
        string normalizedScope = scope?.Trim().ToLowerInvariant() ?? string.Empty;
        string taskFilter = NormalizeTaskFilter(tasks);
        LeadStatus? parsedStatus = Enum.TryParse<LeadStatus>(status, true, out LeadStatus statusValue)
            ? statusValue
            : null;

        if (normalizedScope is "candidates" or "suppressed")
        {
            IQueryable<PlatformEntities.Company> companies = context.Companies
                .AsNoTracking()
                .Where(company => company.SourceSystem == "CompaniesHouse");

            companies = normalizedScope == "suppressed"
                ? companies.Where(company => company.IsProspectingSuppressed)
                : companies.Where(company =>
                    !company.IsProspectingSuppressed
                    && !company.Relationships.Any()
                    && !context.Leads.Any(lead => lead.CompanyId == company.Id));

            if (!string.IsNullOrWhiteSpace(search))
            {
                string trimmedSearch = search.Trim();
                companies = companies.Where(company =>
                    company.OfficialName.Contains(trimmedSearch)
                    || company.LegalEntityName.Contains(trimmedSearch)
                    || company.TradingName.Contains(trimmedSearch)
                    || company.CompanyNumber.Contains(trimmedSearch));
            }

            int matchingCompanyCount = await companies.CountAsync();
            List<LeadCompanyPoolItemViewModel> companyRows = await companies
                .OrderByDescending(company => company.RankingScore)
                .ThenBy(company => company.OfficialName)
                .Take(100)
                .Select(company => new LeadCompanyPoolItemViewModel
                {
                    Id = company.Id,
                    CompanyName = company.TradingName ?? company.OfficialName ?? company.LegalEntityName,
                    CompanyNumber = company.CompanyNumber ?? string.Empty,
                    CompanyStatus = company.CompanyStatus ?? string.Empty,
                    RankingScore = company.RankingScore,
                    SuppressionReason = company.ProspectingSuppressedReason ?? string.Empty
                })
                .ToListAsync();

            return View(new LeadsPageViewModel
            {
                Search = search ?? string.Empty,
                Scope = normalizedScope,
                QueueTitle = normalizedScope == "suppressed" ? "Not Interested Companies" : "Company Pool",
                MatchingCompanyCount = matchingCompanyCount,
                Companies = companyRows,
                StatusOptions = BuildStatusOptions(null),
                NewLead = BuildLeadEditor(new LeadEditorViewModel
                {
                    TenantId = GetWriteableTenantIds().FirstOrDefault() ?? "default",
                    SourceSystem = "Manual",
                    FormTitle = "New Lead",
                    SubmitLabel = "Create lead"
                })
            });
        }

        IQueryable<LeadRowProjection> query = context.Leads
            .AsNoTracking()
            .Where(lead => readableTenantIds.Contains(lead.TenantId))
            .Select(lead => new LeadRowProjection
            {
                Id = lead.Id,
                TenantId = lead.TenantId,
                Status = lead.Status,
                SourceSystem = lead.SourceSystem ?? string.Empty,
                CompanyName = lead.RawCompanyName,
                ContactName = context.LeadContacts
                    .Where(contact => contact.LeadId == lead.Id)
                    .OrderByDescending(contact => contact.IsPrimary)
                    .Select(contact => contact.Name)
                    .FirstOrDefault() ?? string.Empty,
                LinkedCompanyId = lead.CompanyId,
                LinkedRelationshipId = lead.TenantCompanyRelationshipId,
                LinkedOpportunityId = lead.OpportunityId,
                CreatedOn = lead.CreatedOn
            });

        if (parsedStatus.HasValue)
            query = query.Where(item => item.Status == parsedStatus.Value);

        if (normalizedScope == "active")
            query = query.Where(item => item.Status != LeadStatus.Rejected && item.Status != LeadStatus.Converted);

        DateTimeOffset today = new(DateTime.UtcNow.Date, TimeSpan.Zero);
        DateTimeOffset tomorrow = today.AddDays(1);
        if (taskFilter.Length > 0)
        {
            query = taskFilter switch
            {
                "due-today" => query.Where(item => context.ProcessTasks.Any(task =>
                    task.LeadId == item.Id && task.State == ProcessTaskState.Pending
                    && task.DueOn >= today && task.DueOn < tomorrow)),
                "overdue" => query.Where(item => context.ProcessTasks.Any(task =>
                    task.LeadId == item.Id && task.State == ProcessTaskState.Pending && task.DueOn < today)),
                _ => query.Where(item => context.ProcessTasks.Any(task =>
                    task.LeadId == item.Id && task.State == ProcessTaskState.Pending))
            };
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string trimmedSearch = search.Trim();
            query = query.Where(item =>
                item.CompanyName.Contains(trimmedSearch)
                || item.ContactName.Contains(trimmedSearch)
                || item.SourceSystem.Contains(trimmedSearch)
                || item.TenantId.Contains(trimmedSearch));
        }

        List<LeadListItemViewModel> leads = await query
            .OrderByDescending(item => item.CreatedOn)
            .Select(item => new LeadListItemViewModel
            {
                Id = item.Id,
                CompanyName = item.CompanyName,
                StatusLabel = DisplayText.Humanize(item.Status),
                SourceSystem = item.SourceSystem,
                ContactName = item.ContactName,
                TenantId = item.TenantId,
                LinkedCompanyLabel = item.LinkedCompanyId.HasValue
                    ? "Matched"
                    : item.LinkedRelationshipId.HasValue
                        ? "Qualified"
                        : "Pending",
                CreatedOnLabel = item.CreatedOn.LocalDateTime.ToString("dd MMM yyyy HH:mm")
            })
            .ToListAsync();

        return View(new LeadsPageViewModel
        {
            Notice = TempData["LeadsNotice"]?.ToString() ?? string.Empty,
            Search = search ?? string.Empty,
            StatusFilter = parsedStatus?.ToString() ?? string.Empty,
            Scope = normalizedScope,
            TaskFilter = taskFilter,
            QueueTitle = BuildQueueTitle("Lead Queue", taskFilter),
            StatusOptions = BuildStatusOptions(parsedStatus?.ToString()),
            Leads = leads,
            NewLead = BuildLeadEditor(new LeadEditorViewModel
            {
                TenantId = GetWriteableTenantIds().FirstOrDefault() ?? "default",
                SourceSystem = "Manual",
                FormTitle = "New Lead",
                SubmitLabel = "Create lead"
            })
        });
    }

    [HttpGet("/Leads/{id:guid}/Details")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        string[] tenantIds = GetReadableTenantIds();
        using PlatformDbContext context = dbContextFactory.CreateDbContext();
        PlatformEntities.Lead lead = await context.Leads
            .AsNoTracking()
            .Include(item => item.Company)
            .Include(item => item.Contacts)
            .SingleOrDefaultAsync(item => item.Id == id && tenantIds.Contains(item.TenantId), cancellationToken);
        if (lead is null)
            return NotFound();

        List<LeadDetailTaskViewModel> tasks = await context.ProcessTasks
            .AsNoTracking()
            .Where(item => item.LeadId == id)
            .OrderBy(item => item.DueOn)
            .Select(item => new LeadDetailTaskViewModel
            {
                Step = item.ProcessStep.Key,
                Title = item.RenderedTitle,
                State = item.State.ToString(),
                Outcome = item.CompletionOutcomeKey ?? string.Empty,
                Notes = item.CompletionNotes ?? string.Empty,
                DueOn = item.DueOn,
                CompletedOn = item.CompletedOn
            })
            .ToListAsync(cancellationToken);
        List<LeadDetailArtifactViewModel> artifacts = await context.CompanyHistory
            .AsNoTracking()
            .Where(item => item.CompanyId == lead.CompanyId && item.TenantId == lead.TenantId)
            .OrderByDescending(item => item.OccurredOn)
            .Take(100)
            .Select(item => new LeadDetailArtifactViewModel
            {
                OccurredOn = item.OccurredOn,
                EventType = item.EventType,
                Summary = item.Summary,
                Details = item.Details ?? string.Empty,
                Confidence = item.Confidence ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        return PartialView("_Details", new LeadDetailsViewModel
        {
            Id = lead.Id,
            CompanyName = CompanyNames.ResolvePreferredName(lead.Company),
            CompanyNumber = lead.Company.CompanyNumber ?? lead.RawCompanyNumber ?? string.Empty,
            Status = DisplayText.Humanize(lead.Status),
            Source = lead.SourceSystem ?? string.Empty,
            RankingScore = lead.RankingScore ?? lead.Company.RankingScore,
            RankingRationale = lead.RankingRationale ?? lead.Company.RankingRationale ?? string.Empty,
            QualificationNotes = lead.QualificationNotes ?? string.Empty,
            SuppressionReason = lead.Company.ProspectingSuppressedReason ?? string.Empty,
            Contacts = [.. lead.Contacts.OrderByDescending(item => item.IsPrimary).Select(item => new LeadDetailContactViewModel
            {
                Name = item.Name,
                Position = item.Position ?? string.Empty,
                Email = item.EmailAddress ?? string.Empty,
                IsPrimary = item.IsPrimary
            })],
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

    static string BuildQueueTitle(string title, string taskFilter) => taskFilter switch
    {
        "due-today" => $"{title} — Due Today",
        "overdue" => $"{title} — Overdue",
        "open" => $"{title} — Open Actions",
        _ => title
    };

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LeadEditorViewModel model)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (!ModelState.IsValid)
        {
            TempData["LeadsNotice"] = "Complete the required lead fields before saving.";
            return RedirectToAction(nameof(Index));
        }

        await leadIngestionService.CreateLeadAsync(new CreateLeadCommand
        {
            TenantId = model.TenantId,
            SourceSystem = model.SourceSystem,
            SourceRecordId = model.SourceRecordId,
            SourceFileName = model.SourceFileName,
            RawCompanyName = model.RawCompanyName,
            RawTradingName = model.RawTradingName,
            RawCompanyNumber = model.RawCompanyNumber,
            RawVatNumber = model.RawVatNumber,
            RawWebsiteUrl = model.RawWebsiteUrl,
            RawContactEmailAddress = model.RawContactEmailAddress,
            RawContactPhoneNumber = model.RawContactPhoneNumber,
            RawAddressText = model.RawAddressText,
            QualificationNotes = model.QualificationNotes,
            ContactName = model.ContactName,
            ContactPosition = model.ContactPosition,
            ContactEmailAddress = model.ContactEmailAddress,
            ContactPhoneNumber = model.ContactPhoneNumber,
            ContactLinkedInUrl = model.ContactLinkedInUrl
        });

        TempData["LeadsNotice"] = "Lead created and workflow started.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile file, string tenantId = "default", string sourceSystem = "CSV Import")
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        TempData["ImportsNotice"] = "Bulk lead import now runs through the hosted import workflow.";
        await Task.CompletedTask;
        return RedirectToAction("Index", "Imports");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        LeadEditorViewModel model = await CreateEditorModelAsync(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(LeadEditorViewModel model)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        if (model.Id is null)
            return BadRequest();

        if (!ModelState.IsValid)
            return View(await CreateEditorModelAsync(model.Id.Value, model));

        using PlatformDbContext context = dbContextFactory.CreateDbContext(useAdminConnection: true);
        PlatformEntities.Lead lead = await context.Leads
            .Include(item => item.Company)
                .ThenInclude(company => company.RegisteredAddress)
            .FirstOrDefaultAsync(item => item.Id == model.Id.Value);
        if (lead is null || !GetWriteableTenantIds().Contains(lead.TenantId))
            return NotFound();

        lead.TenantId = model.TenantId.Trim();
        lead.SourceSystem = Normalize(model.SourceSystem);
        lead.SourceRecordId = Normalize(model.SourceRecordId);
        lead.SourceFileName = Normalize(model.SourceFileName);
        lead.Status = model.Status;
        lead.RawCompanyName = model.RawCompanyName.Trim();
        lead.RawTradingName = Normalize(model.RawTradingName);
        lead.RawCompanyNumber = Normalize(model.RawCompanyNumber);
        lead.RawVatNumber = Normalize(model.RawVatNumber);
        lead.RawWebsiteUrl = Normalize(model.RawWebsiteUrl);
        lead.RawContactEmailAddress = Normalize(model.RawContactEmailAddress);
        lead.RawContactPhoneNumber = Normalize(model.RawContactPhoneNumber);
        string addressText = Normalize(model.RawAddressText);
        if (addressText is null)
        {
            lead.Company.RegisteredAddressId = null;
        }
        else if (lead.Company.RegisteredAddress is null)
        {
            PlatformEntities.Address address = AddressRecordMapper.CreateFromText(
                addressText,
                lead.SourceSystem,
                CurrentUserId,
                DateTimeOffset.UtcNow);
            context.Addresses.Add(address);
            lead.Company.RegisteredAddressId = address.Id;
        }
        else
        {
            AddressRecordMapper.ApplyText(lead.Company.RegisteredAddress, addressText, CurrentUserId, DateTimeOffset.UtcNow);
        }
        lead.Company.LastUpdatedBy = CurrentUserId;
        lead.Company.LastUpdated = DateTimeOffset.UtcNow;
        lead.QualificationNotes = Normalize(model.QualificationNotes);
        lead.LastUpdatedBy = CurrentUserId;
        lead.LastUpdated = DateTimeOffset.UtcNow;

        PlatformEntities.LeadContact contact = await context.LeadContacts
            .FirstOrDefaultAsync(item => item.LeadId == lead.Id);

        if (contact is null && HasContactData(model))
        {
            context.LeadContacts.Add(new PlatformEntities.LeadContact
            {
                Id = Guid.NewGuid(),
                LeadId = lead.Id,
                IsPrimary = true,
                Name = Normalize(model.ContactName),
                Position = Normalize(model.ContactPosition),
                EmailAddress = Normalize(model.ContactEmailAddress),
                PhoneNumber = Normalize(model.ContactPhoneNumber),
                LinkedInUrl = Normalize(model.ContactLinkedInUrl),
                CreatedBy = CurrentUserId,
                LastUpdatedBy = CurrentUserId,
                CreatedOn = DateTimeOffset.UtcNow,
                LastUpdated = DateTimeOffset.UtcNow
            });
        }
        else if (contact is not null)
        {
            contact.Name = Normalize(model.ContactName);
            contact.Position = Normalize(model.ContactPosition);
            contact.EmailAddress = Normalize(model.ContactEmailAddress);
            contact.PhoneNumber = Normalize(model.ContactPhoneNumber);
            contact.LinkedInUrl = Normalize(model.ContactLinkedInUrl);
            contact.LastUpdatedBy = CurrentUserId;
            contact.LastUpdated = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync();
        await workflowAutomationService.EnsureCoverageAsync(leadId: lead.Id, forceCreate: true);

        TempData["LeadsNotice"] = "Lead updated.";
        return RedirectToAction(nameof(Edit), new { id = lead.Id });
    }

    IActionResult RedirectIfUnauthenticated()
    {
        if (!string.IsNullOrWhiteSpace(ssoAuthInfo?.SSOUserId)
            && !string.Equals(ssoAuthInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            return null;

        string returnUrl = $"{Request.Path}{Request.QueryString}";
        return RedirectToAction("Login", "Account", new { returnUrl });
    }

    async Task<LeadEditorViewModel> CreateEditorModelAsync(Guid id, LeadEditorViewModel postedModel = null)
    {
        using PlatformDbContext context = dbContextFactory.CreateDbContext();
        PlatformEntities.Lead lead = await context.Leads
            .Include(item => item.Company)
                .ThenInclude(company => company.RegisteredAddress)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id);

        if (lead is null || !GetReadableTenantIds().Contains(lead.TenantId))
            return null;

        PlatformEntities.LeadContact contact = await context.LeadContacts
            .AsNoTracking()
            .Where(item => item.LeadId == id)
            .OrderByDescending(item => item.IsPrimary)
            .FirstOrDefaultAsync();

        LeadEditorViewModel source = postedModel ?? new LeadEditorViewModel();
        return BuildLeadEditor(new LeadEditorViewModel
        {
            Id = lead.Id,
            Notice = TempData["LeadsNotice"]?.ToString() ?? string.Empty,
            FormTitle = "Lead Details",
            SubmitLabel = "Save lead",
            TenantId = source.TenantId.Length == 0 ? lead.TenantId : source.TenantId,
            SourceSystem = source.SourceSystem.Length == 0 ? lead.SourceSystem ?? string.Empty : source.SourceSystem,
            SourceRecordId = source.SourceRecordId.Length == 0 ? lead.SourceRecordId ?? string.Empty : source.SourceRecordId,
            SourceFileName = source.SourceFileName.Length == 0 ? lead.SourceFileName ?? string.Empty : source.SourceFileName,
            RawCompanyName = source.RawCompanyName.Length == 0 ? lead.RawCompanyName : source.RawCompanyName,
            RawTradingName = source.RawTradingName.Length == 0 ? lead.RawTradingName ?? string.Empty : source.RawTradingName,
            RawCompanyNumber = source.RawCompanyNumber.Length == 0 ? lead.RawCompanyNumber ?? string.Empty : source.RawCompanyNumber,
            RawVatNumber = source.RawVatNumber.Length == 0 ? lead.RawVatNumber ?? string.Empty : source.RawVatNumber,
            RawWebsiteUrl = source.RawWebsiteUrl.Length == 0 ? lead.RawWebsiteUrl ?? string.Empty : source.RawWebsiteUrl,
            RawContactEmailAddress = source.RawContactEmailAddress.Length == 0 ? lead.RawContactEmailAddress ?? string.Empty : source.RawContactEmailAddress,
            RawContactPhoneNumber = source.RawContactPhoneNumber.Length == 0 ? lead.RawContactPhoneNumber ?? string.Empty : source.RawContactPhoneNumber,
            RawAddressText = source.RawAddressText.Length == 0
                ? AddressRecordMapper.Format(lead.Company.RegisteredAddress)
                : source.RawAddressText,
            QualificationNotes = source.QualificationNotes.Length == 0 ? lead.QualificationNotes ?? string.Empty : source.QualificationNotes,
            ContactName = source.ContactName.Length == 0 ? contact?.Name ?? string.Empty : source.ContactName,
            ContactPosition = source.ContactPosition.Length == 0 ? contact?.Position ?? string.Empty : source.ContactPosition,
            ContactEmailAddress = source.ContactEmailAddress.Length == 0 ? contact?.EmailAddress ?? string.Empty : source.ContactEmailAddress,
            ContactPhoneNumber = source.ContactPhoneNumber.Length == 0 ? contact?.PhoneNumber ?? string.Empty : source.ContactPhoneNumber,
            ContactLinkedInUrl = source.ContactLinkedInUrl.Length == 0 ? contact?.LinkedInUrl ?? string.Empty : source.ContactLinkedInUrl,
            Status = postedModel?.Status ?? lead.Status,
            LinkedCompanyId = lead.CompanyId.ToString(),
            LinkedRelationshipId = lead.TenantCompanyRelationshipId?.ToString() ?? string.Empty,
            LinkedOpportunityId = lead.OpportunityId?.ToString() ?? string.Empty
        });
    }

    static LeadEditorViewModel BuildLeadEditor(LeadEditorViewModel model) =>
        new()
        {
            Id = model.Id,
            Notice = model.Notice,
            FormTitle = string.IsNullOrWhiteSpace(model.FormTitle) ? "Lead Details" : model.FormTitle,
            SubmitLabel = string.IsNullOrWhiteSpace(model.SubmitLabel) ? "Save lead" : model.SubmitLabel,
            TenantId = model.TenantId,
            SourceSystem = model.SourceSystem,
            SourceRecordId = model.SourceRecordId,
            SourceFileName = model.SourceFileName,
            RawCompanyName = model.RawCompanyName,
            RawTradingName = model.RawTradingName,
            RawCompanyNumber = model.RawCompanyNumber,
            RawVatNumber = model.RawVatNumber,
            RawWebsiteUrl = model.RawWebsiteUrl,
            RawContactEmailAddress = model.RawContactEmailAddress,
            RawContactPhoneNumber = model.RawContactPhoneNumber,
            RawAddressText = model.RawAddressText,
            QualificationNotes = model.QualificationNotes,
            ContactName = model.ContactName,
            ContactPosition = model.ContactPosition,
            ContactEmailAddress = model.ContactEmailAddress,
            ContactPhoneNumber = model.ContactPhoneNumber,
            ContactLinkedInUrl = model.ContactLinkedInUrl,
            Status = model.Status,
            LinkedCompanyId = model.LinkedCompanyId,
            LinkedRelationshipId = model.LinkedRelationshipId,
            LinkedOpportunityId = model.LinkedOpportunityId,
            StatusOptions = Enum.GetValues<LeadStatus>()
                .Select(status => new SelectListItem(DisplayText.Humanize(status), status.ToString(), status == model.Status))
                .ToList()
        };

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

    static bool HasContactData(LeadEditorViewModel model) =>
        !string.IsNullOrWhiteSpace(model.ContactName)
        || !string.IsNullOrWhiteSpace(model.ContactEmailAddress)
        || !string.IsNullOrWhiteSpace(model.ContactPhoneNumber);

    static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    static IReadOnlyList<SelectListItem> BuildStatusOptions(string selectedValue) =>
    [
        new("All statuses", string.Empty, string.IsNullOrWhiteSpace(selectedValue)),
        .. Enum.GetValues<LeadStatus>()
            .Select(status => new SelectListItem(
                DisplayText.Humanize(status),
                status.ToString(),
                string.Equals(selectedValue, status.ToString(), StringComparison.OrdinalIgnoreCase)))
    ];

    sealed class LeadRowProjection
    {
        public Guid Id { get; init; }
        public string TenantId { get; init; }
        public LeadStatus Status { get; init; }
        public string SourceSystem { get; init; }
        public string CompanyName { get; init; }
        public string ContactName { get; init; }
        public Guid? LinkedCompanyId { get; init; }
        public Guid? LinkedRelationshipId { get; init; }
        public Guid? LinkedOpportunityId { get; init; }
        public DateTimeOffset CreatedOn { get; init; }
    }
}
