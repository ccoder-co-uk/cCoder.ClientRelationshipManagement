namespace ClientRelationshipManagement.Web.Models.Clients;

public sealed class ClientOpportunitySummaryViewModel
{
    public Guid Id { get; init; }
    public string TypeLabel { get; init; } = string.Empty;
    public string StageLabel { get; init; } = string.Empty;
    public string EstimatedAnnualValueLabel { get; init; } = string.Empty;
    public string ProbabilityLabel { get; init; } = string.Empty;
    public string PainSummary { get; init; } = string.Empty;
    public string ValueHypothesis { get; init; } = string.Empty;
    public string NextAction { get; init; } = string.Empty;
    public string NextActionDueLabel { get; init; } = string.Empty;
}
