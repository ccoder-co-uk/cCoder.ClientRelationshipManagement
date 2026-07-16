namespace ClientRelationshipManagement.Web.Models.Companies;

public sealed class CompanyExplorerPageViewModel
{
    public string Search { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string Sort { get; init; } = "name";
    public bool Descending { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public long TotalCount { get; init; }
    public int TotalPages { get; init; }
    public IReadOnlyList<CompanyExplorerRowViewModel> Companies { get; init; } = [];

    public long FirstItem => TotalCount == 0 ? 0 : ((long)(Page - 1) * PageSize) + 1;
    public long LastItem => Math.Min((long)Page * PageSize, TotalCount);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public sealed class CompanyExplorerRowViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string CompanyNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public int? EmployeeCount { get; init; }
    public decimal? AnnualRevenue { get; init; }
    public string RevenueCurrency { get; init; } = string.Empty;
    public int? RankingScore { get; init; }
    public bool IsVerified { get; init; }
    public bool IsProspectingSuppressed { get; init; }
    public int ContactCount { get; init; }
    public int LeadCount { get; init; }
    public int RelationshipCount { get; init; }
}

public sealed class CompanyExplorerDetailsViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string LegalEntityName { get; init; } = string.Empty;
    public string TradingName { get; init; } = string.Empty;
    public string CompanyNumber { get; init; } = string.Empty;
    public string VatNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string SourceRecordId { get; init; } = string.Empty;
    public string SicCodes { get; init; } = string.Empty;
    public string RegistryUri { get; init; } = string.Empty;
    public string WebsiteUrl { get; init; } = string.Empty;
    public string ContactEmail { get; init; } = string.Empty;
    public string ContactPhone { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public DateTimeOffset? IncorporatedOn { get; init; }
    public DateTimeOffset? DissolvedOn { get; init; }
    public int? EmployeeCount { get; init; }
    public decimal? AnnualRevenue { get; init; }
    public string RevenueCurrency { get; init; } = string.Empty;
    public int? RankingScore { get; init; }
    public string RankingRationale { get; init; } = string.Empty;
    public string ResearchSummary { get; init; } = string.Empty;
    public string VerificationNotes { get; init; } = string.Empty;
    public bool IsVerified { get; init; }
    public bool IsProspectingSuppressed { get; init; }
    public string ProspectingSuppressedReason { get; init; } = string.Empty;
    public IReadOnlyList<CompanyExplorerContactViewModel> Contacts { get; init; } = [];
    public IReadOnlyList<CompanyExplorerCommercialItemViewModel> CommercialItems { get; init; } = [];
    public IReadOnlyList<CompanyExplorerTaskViewModel> Tasks { get; init; } = [];
    public IReadOnlyList<CompanyHistoryItemViewModel> History { get; init; } = [];
}

public sealed class CompanyExplorerContactViewModel
{
    public string Name { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
    public bool IsVerified { get; init; }
}

public sealed class CompanyExplorerCommercialItemViewModel
{
    public string Kind { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string LinkUrl { get; init; } = string.Empty;
    public DateTimeOffset CreatedOn { get; init; }
}

public sealed class CompanyExplorerTaskViewModel
{
    public string Lane { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string ActionType { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public DateTimeOffset DueOn { get; init; }
}
