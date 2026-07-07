namespace ClientRelationshipManagement.Web.Models.Opportunities;

public sealed class OpportunityListItemViewModel
{
    public Guid Id { get; init; }
    public Guid ClientId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string RelationshipStatusLabel { get; init; } = string.Empty;
    public string StageLabel { get; init; } = string.Empty;
    public string TypeLabel { get; init; } = string.Empty;
    public string PrimaryContactName { get; init; } = string.Empty;
    public string EstimatedAnnualValueLabel { get; init; } = string.Empty;
    public string ProbabilityLabel { get; init; } = string.Empty;
    public string NextAction { get; init; } = string.Empty;
    public string NextActionDueLabel { get; init; } = string.Empty;
}
