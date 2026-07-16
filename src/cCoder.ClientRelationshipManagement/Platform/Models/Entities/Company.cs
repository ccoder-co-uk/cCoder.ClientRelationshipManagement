namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class Company : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public string LegacyId { get; set; }
    public string SourceSystem { get; set; }
    public string SourceRecordId { get; set; }
    public string AuthorityRecordHash { get; set; }
    public bool IsVerified { get; set; }
    public string OfficialName { get; set; }
    public string LegalEntityName { get; set; }
    public string TradingName { get; set; }
    public string CompanyNumber { get; set; }
    public string VatNumber { get; set; }
    public string CompanyCategory { get; set; }
    public string CompanyStatus { get; set; }
    public string CountryOfOrigin { get; set; }
    public DateTimeOffset? IncorporatedOn { get; set; }
    public DateTimeOffset? DissolvedOn { get; set; }
    public string PrimarySicCodes { get; set; }
    public string RegistryUri { get; set; }
    public string PreviousNamesJson { get; set; }
    public string WebsiteUrl { get; set; }
    public string ContactEmailAddress { get; set; }
    public string ContactPhoneNumber { get; set; }
    public string ResearchSummary { get; set; }
    public string VerificationNotes { get; set; }
    public decimal? AnnualRevenue { get; set; }
    public string RevenueCurrency { get; set; }
    public int? EmployeeCount { get; set; }
    public int? RankingScore { get; set; }
    public string RankingRationale { get; set; }
    public bool IsProspectingSuppressed { get; set; }
    public string ProspectingSuppressedReason { get; set; }
    public DateTimeOffset? ProspectingSuppressedOn { get; set; }
    public Guid? RegisteredAddressId { get; set; }

    public virtual Address RegisteredAddress { get; set; }
    public virtual ICollection<CompanyContact> Contacts { get; set; } = new List<CompanyContact>();
    public virtual ICollection<TenantCompanyRelationship> Relationships { get; set; } = new List<TenantCompanyRelationship>();
    public virtual ICollection<CompanyHistoryItem> History { get; set; } = new List<CompanyHistoryItem>();
}
