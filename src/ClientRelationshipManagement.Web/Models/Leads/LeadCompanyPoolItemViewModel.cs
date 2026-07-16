namespace ClientRelationshipManagement.Web.Models.Leads;

public sealed class LeadCompanyPoolItemViewModel
{
    public Guid Id { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string CompanyNumber { get; init; } = string.Empty;
    public string CompanyStatus { get; init; } = string.Empty;
    public int? RankingScore { get; init; }
    public string SuppressionReason { get; init; } = string.Empty;
}
