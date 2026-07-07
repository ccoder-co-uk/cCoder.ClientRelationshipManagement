using cCoder.ClientRelationshipManagement.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Models.Requests;

public class CreateClientRequest
{
    public string TenantId { get; set; }
    public string CompanyName { get; set; }
    public string LegalEntityName { get; set; }
    public string TradingName { get; set; }
    public string CompanyNumber { get; set; }
    public string VatNumber { get; set; }
    public string WebsiteUrl { get; set; }
    public string AccountOwner { get; set; }
    public string LeadSource { get; set; }
    public string InitialRoute { get; set; }
    public string OpportunitySummary { get; set; }
    public string PreferredOpeningAngle { get; set; }
    public ClientPriority Priority { get; set; } = ClientPriority.Unknown;
    public ClientOpportunityType OpportunityType { get; set; } = ClientOpportunityType.Unknown;
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
}
