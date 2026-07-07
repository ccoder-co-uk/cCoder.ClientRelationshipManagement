namespace ClientRelationshipManagement.Web.Models.Leads;

public sealed class LeadListItemViewModel
{
    public Guid Id { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string SourceSystem { get; init; } = string.Empty;
    public string ContactName { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string LinkedCompanyLabel { get; init; } = string.Empty;
    public string CreatedOnLabel { get; init; } = string.Empty;
}
