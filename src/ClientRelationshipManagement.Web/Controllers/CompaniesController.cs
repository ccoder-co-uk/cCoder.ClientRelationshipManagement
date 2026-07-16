using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Data;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using cCoder.Security.Objects;
using ClientRelationshipManagement.Web.Models.Companies;
using ClientRelationshipManagement.Web.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClientRelationshipManagement.Web.Controllers;

[Route("Companies")]
public sealed class CompaniesController(
    IPlatformDbContextFactory dbContextFactory,
    ICRMAuthInfo authInfo,
    ISSOAuthInfo ssoAuthInfo)
    : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string search = null,
        string status = null,
        string source = null,
        string country = null,
        string sort = "name",
        bool descending = false,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        string[] tenantIds = GetReadableTenantIds();
        using PlatformDbContext context = dbContextFactory.CreateDbContext();
        IQueryable<Company> query = VisibleCompanies(context, tenantIds).AsNoTracking();

        search = search?.Trim() ?? string.Empty;
        status = status?.Trim() ?? string.Empty;
        source = source?.Trim() ?? string.Empty;
        country = country?.Trim() ?? string.Empty;
        sort = NormalizeSort(sort);
        pageSize = pageSize is 25 or 50 or 100 ? pageSize : 50;

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(company =>
                company.CompanyNumber.StartsWith(search)
                || company.OfficialName.StartsWith(search)
                || company.LegalEntityName.StartsWith(search)
                || company.TradingName.StartsWith(search));
        }
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(company => company.CompanyStatus == status);
        if (!string.IsNullOrWhiteSpace(source))
            query = query.Where(company => company.SourceSystem == source);
        if (!string.IsNullOrWhiteSpace(country))
            query = query.Where(company => company.CountryOfOrigin == country);

        long totalCount = await query.LongCountAsync(cancellationToken);
        int totalPages = Math.Max(1, (int)Math.Min(int.MaxValue, (totalCount + pageSize - 1) / pageSize));
        page = Math.Clamp(page, 1, totalPages);
        query = ApplySort(query, sort, descending);

        List<CompanyExplorerRowViewModel> companies = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(company => new CompanyExplorerRowViewModel
            {
                Id = company.Id,
                Name = company.TradingName ?? company.OfficialName,
                CompanyNumber = company.CompanyNumber ?? string.Empty,
                Status = company.CompanyStatus ?? string.Empty,
                Category = company.CompanyCategory ?? string.Empty,
                Country = company.CountryOfOrigin ?? string.Empty,
                Source = company.SourceSystem ?? string.Empty,
                EmployeeCount = company.EmployeeCount,
                AnnualRevenue = company.AnnualRevenue,
                RevenueCurrency = company.RevenueCurrency ?? string.Empty,
                RankingScore = company.RankingScore,
                IsVerified = company.IsVerified,
                IsProspectingSuppressed = company.IsProspectingSuppressed,
                ContactCount = company.Contacts.Count,
                LeadCount = context.Leads.Count(lead => lead.CompanyId == company.Id && tenantIds.Contains(lead.TenantId)),
                RelationshipCount = company.Relationships.Count(relationship => tenantIds.Contains(relationship.TenantId))
            })
            .ToListAsync(cancellationToken);

        return View(new CompanyExplorerPageViewModel
        {
            Search = search,
            Status = status,
            Source = source,
            Country = country,
            Sort = sort,
            Descending = descending,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Companies = companies
        });
    }

    [HttpGet("{id:guid}/Details")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        string[] tenantIds = GetReadableTenantIds();
        using PlatformDbContext context = dbContextFactory.CreateDbContext();
        if (!await VisibleCompanies(context, tenantIds).AnyAsync(company => company.Id == id, cancellationToken))
            return NotFound();

        Company company = await context.Companies
            .AsNoTracking()
            .Include(item => item.RegisteredAddress)
            .SingleAsync(item => item.Id == id, cancellationToken);

        List<CompanyContact> contacts = await context.CompanyContacts
            .AsNoTracking()
            .Where(item => item.CompanyId == id)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.Name)
            .Take(100)
            .ToListAsync(cancellationToken);

        var leads = await context.Leads.AsNoTracking()
            .Where(item => item.CompanyId == id && tenantIds.Contains(item.TenantId))
            .OrderByDescending(item => item.CreatedOn)
            .Take(100)
            .Select(item => new { item.Id, item.TenantId, item.Status, item.QualificationNotes, item.CreatedOn })
            .ToListAsync(cancellationToken);
        var relationships = await context.TenantCompanyRelationships.AsNoTracking()
            .Where(item => item.CompanyId == id && tenantIds.Contains(item.TenantId))
            .OrderByDescending(item => item.CreatedOn)
            .Take(100)
            .Select(item => new { item.Id, item.TenantId, item.Status, item.CurrentStage, item.OpportunitySummary, item.CreatedOn })
            .ToListAsync(cancellationToken);
        var opportunities = await context.Opportunities.AsNoTracking()
            .Where(item => item.TenantCompanyRelationship.CompanyId == id
                && tenantIds.Contains(item.TenantCompanyRelationship.TenantId))
            .OrderByDescending(item => item.CreatedOn)
            .Take(100)
            .Select(item => new
            {
                item.TenantCompanyRelationshipId,
                item.TenantCompanyRelationship.TenantId,
                item.Stage,
                item.PainSummary,
                item.ValueHypothesis,
                item.CreatedOn
            })
            .ToListAsync(cancellationToken);
        var clients = await context.ClientAccounts.AsNoTracking()
            .Where(item => item.TenantCompanyRelationship.CompanyId == id
                && tenantIds.Contains(item.TenantCompanyRelationship.TenantId))
            .OrderByDescending(item => item.CreatedOn)
            .Take(100)
            .Select(item => new
            {
                item.TenantCompanyRelationshipId,
                item.TenantCompanyRelationship.TenantId,
                item.Status,
                item.AccountReference,
                item.CreatedOn
            })
            .ToListAsync(cancellationToken);
        var tasks = await context.ProcessTasks.AsNoTracking()
            .Where(item =>
                (item.LeadId.HasValue && item.Lead.CompanyId == id && tenantIds.Contains(item.Lead.TenantId))
                || (item.TenantCompanyRelationshipId.HasValue
                    && item.TenantCompanyRelationship.CompanyId == id
                    && tenantIds.Contains(item.TenantCompanyRelationship.TenantId)))
            .OrderBy(item => item.State)
            .ThenBy(item => item.DueOn)
            .Take(100)
            .Select(item => new
            {
                item.RenderedTitle,
                item.ActionType,
                item.State,
                item.DueOn,
                item.ProcessInstance.ProcessDefinition.ScopeType
            })
            .ToListAsync(cancellationToken);
        List<CompanyHistoryItemViewModel> history = await context.CompanyHistory.AsNoTracking()
            .Where(item => item.CompanyId == id && tenantIds.Contains(item.TenantId))
            .OrderByDescending(item => item.OccurredOn)
            .ThenByDescending(item => item.CreatedOn)
            .Take(100)
            .Select(item => new CompanyHistoryItemViewModel
            {
                OccurredOn = item.OccurredOn,
                Lane = item.Lane,
                EventType = item.EventType,
                Summary = item.Summary,
                Details = item.Details ?? string.Empty,
                FactKey = item.FactKey ?? string.Empty,
                FactValue = item.FactValue ?? string.Empty,
                Confidence = item.Confidence ?? string.Empty,
                SourceType = item.SourceType ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        List<CompanyExplorerCommercialItemViewModel> commercialItems =
        [
            .. leads.Select(item => new CompanyExplorerCommercialItemViewModel
            {
                Kind = "Lead",
                TenantId = item.TenantId,
                Status = item.Status.ToString(),
                Summary = item.QualificationNotes ?? string.Empty,
                LinkUrl = $"/Leads/Edit/{item.Id}",
                CreatedOn = item.CreatedOn
            }),
            .. relationships.Select(item => new CompanyExplorerCommercialItemViewModel
            {
                Kind = "Relationship",
                TenantId = item.TenantId,
                Status = $"{item.Status} · {item.CurrentStage}",
                Summary = item.OpportunitySummary ?? string.Empty,
                LinkUrl = $"/Clients/Edit/{item.Id}",
                CreatedOn = item.CreatedOn
            }),
            .. opportunities.Select(item => new CompanyExplorerCommercialItemViewModel
            {
                Kind = "Opportunity",
                TenantId = item.TenantId,
                Status = item.Stage.ToString(),
                Summary = item.PainSummary ?? item.ValueHypothesis ?? string.Empty,
                LinkUrl = $"/Clients/Edit/{item.TenantCompanyRelationshipId}",
                CreatedOn = item.CreatedOn
            }),
            .. clients.Select(item => new CompanyExplorerCommercialItemViewModel
            {
                Kind = "Client",
                TenantId = item.TenantId,
                Status = item.Status.ToString(),
                Summary = item.AccountReference ?? string.Empty,
                LinkUrl = $"/Clients/Edit/{item.TenantCompanyRelationshipId}",
                CreatedOn = item.CreatedOn
            })
        ];

        return PartialView("_Details", new CompanyExplorerDetailsViewModel
        {
            Id = company.Id,
            Name = CompanyNames.ResolvePreferredName(company),
            LegalEntityName = company.LegalEntityName ?? string.Empty,
            TradingName = company.TradingName ?? string.Empty,
            CompanyNumber = company.CompanyNumber ?? string.Empty,
            VatNumber = company.VatNumber ?? string.Empty,
            Status = company.CompanyStatus ?? string.Empty,
            Category = company.CompanyCategory ?? string.Empty,
            Country = company.CountryOfOrigin ?? string.Empty,
            Source = company.SourceSystem ?? string.Empty,
            SourceRecordId = company.SourceRecordId ?? string.Empty,
            SicCodes = company.PrimarySicCodes ?? string.Empty,
            RegistryUri = company.RegistryUri ?? string.Empty,
            WebsiteUrl = company.WebsiteUrl ?? string.Empty,
            ContactEmail = company.ContactEmailAddress ?? string.Empty,
            ContactPhone = company.ContactPhoneNumber ?? string.Empty,
            Address = FormatAddress(company.RegisteredAddress),
            IncorporatedOn = company.IncorporatedOn,
            DissolvedOn = company.DissolvedOn,
            EmployeeCount = company.EmployeeCount,
            AnnualRevenue = company.AnnualRevenue,
            RevenueCurrency = company.RevenueCurrency ?? string.Empty,
            RankingScore = company.RankingScore,
            RankingRationale = company.RankingRationale ?? string.Empty,
            ResearchSummary = company.ResearchSummary ?? string.Empty,
            VerificationNotes = company.VerificationNotes ?? string.Empty,
            IsVerified = company.IsVerified,
            IsProspectingSuppressed = company.IsProspectingSuppressed,
            ProspectingSuppressedReason = company.ProspectingSuppressedReason ?? string.Empty,
            Contacts = [.. contacts.Select(item => new CompanyExplorerContactViewModel
            {
                Name = item.Name,
                Position = item.Position ?? string.Empty,
                Email = item.EmailAddress ?? string.Empty,
                Phone = item.PhoneNumber ?? string.Empty,
                Source = item.SourceSystem ?? string.Empty,
                IsPrimary = item.IsPrimary,
                IsVerified = item.IsVerified
            })],
            CommercialItems = [.. commercialItems.OrderByDescending(item => item.CreatedOn)],
            Tasks = [.. tasks.Select(item => new CompanyExplorerTaskViewModel
            {
                Lane = item.ScopeType.ToString(),
                Title = item.RenderedTitle,
                ActionType = item.ActionType.ToString(),
                State = item.State.ToString(),
                DueOn = item.DueOn
            })],
            History = history
        });
    }

    [HttpGet("{id:guid}/History")]
    public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken)
    {
        if (RedirectIfUnauthenticated() is IActionResult redirect)
            return redirect;

        string[] tenantIds = GetReadableTenantIds();
        using PlatformDbContext context = dbContextFactory.CreateDbContext();
        bool canRead = await VisibleCompanies(context, tenantIds)
            .AnyAsync(item => item.Id == id, cancellationToken);
        if (!canRead)
            return NotFound();

        var company = await context.Companies
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                Name = CompanyNames.ResolvePreferredName(item),
                item.CompanyNumber,
                item.CompanyStatus
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (company is null)
            return NotFound();

        List<CompanyHistoryItemViewModel> history = await context.CompanyHistory
            .AsNoTracking()
            .Where(item => item.CompanyId == id && tenantIds.Contains(item.TenantId))
            .OrderByDescending(item => item.OccurredOn)
            .ThenByDescending(item => item.CreatedOn)
            .Take(250)
            .Select(item => new CompanyHistoryItemViewModel
            {
                OccurredOn = item.OccurredOn,
                Lane = item.Lane,
                EventType = item.EventType,
                Summary = item.Summary,
                Details = item.Details ?? string.Empty,
                FactKey = item.FactKey ?? string.Empty,
                FactValue = item.FactValue ?? string.Empty,
                Confidence = item.Confidence ?? string.Empty,
                SourceType = item.SourceType ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        return View(new CompanyHistoryPageViewModel
        {
            CompanyId = company.Id,
            CompanyName = company.Name,
            CompanyNumber = company.CompanyNumber ?? string.Empty,
            CompanyStatus = company.CompanyStatus ?? string.Empty,
            Items = history
        });
    }

    IActionResult RedirectIfUnauthenticated()
    {
        if (!string.IsNullOrWhiteSpace(ssoAuthInfo?.SSOUserId)
            && !string.Equals(ssoAuthInfo.SSOUserId, "Guest", StringComparison.OrdinalIgnoreCase))
            return null;

        return RedirectToAction("Login", "Account", new { returnUrl = $"{Request.Path}{Request.QueryString}" });
    }

    string[] GetReadableTenantIds() => authInfo.ReadableTenants.Length > 0
        ? authInfo.ReadableTenants
        : authInfo.WriteableTenants.Length > 0
            ? authInfo.WriteableTenants
            : ["default"];

    static IQueryable<Company> VisibleCompanies(PlatformDbContext context, string[] tenantIds) =>
        context.Companies.Where(company =>
            company.SourceSystem == "CompaniesHouse"
            || company.Relationships.Any(relationship => tenantIds.Contains(relationship.TenantId))
            || context.Leads.Any(lead => lead.CompanyId == company.Id && tenantIds.Contains(lead.TenantId))
            || context.CompanyHistory.Any(history => history.CompanyId == company.Id && tenantIds.Contains(history.TenantId)));

    static string NormalizeSort(string sort) => sort?.Trim().ToLowerInvariant() switch
    {
        "number" => "number",
        "status" => "status",
        "source" => "source",
        "rank" => "rank",
        _ => "name"
    };

    static IQueryable<Company> ApplySort(IQueryable<Company> query, string sort, bool descending) =>
        (sort, descending) switch
        {
            ("number", false) => query.OrderBy(item => item.CompanyNumber).ThenBy(item => item.OfficialName),
            ("number", true) => query.OrderByDescending(item => item.CompanyNumber).ThenBy(item => item.OfficialName),
            ("status", false) => query.OrderBy(item => item.CompanyStatus).ThenBy(item => item.OfficialName),
            ("status", true) => query.OrderByDescending(item => item.CompanyStatus).ThenBy(item => item.OfficialName),
            ("source", false) => query.OrderBy(item => item.SourceSystem).ThenBy(item => item.OfficialName),
            ("source", true) => query.OrderByDescending(item => item.SourceSystem).ThenBy(item => item.OfficialName),
            ("rank", false) => query.OrderBy(item => item.RankingScore == null).ThenByDescending(item => item.RankingScore).ThenBy(item => item.OfficialName),
            ("rank", true) => query.OrderByDescending(item => item.RankingScore == null).ThenBy(item => item.RankingScore).ThenBy(item => item.OfficialName),
            (_, true) => query.OrderByDescending(item => item.OfficialName),
            _ => query.OrderBy(item => item.OfficialName)
        };

    static string FormatAddress(Address address) => address is null
        ? string.Empty
        : string.Join(", ", new[]
        {
            address.PoBox,
            address.Line1,
            address.Line2,
            address.TownOrCity,
            address.StateOrProvince,
            address.ZipOrPostalCode,
            address.CountryId
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
}
