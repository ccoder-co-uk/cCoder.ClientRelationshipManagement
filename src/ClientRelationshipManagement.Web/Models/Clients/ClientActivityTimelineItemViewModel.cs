namespace ClientRelationshipManagement.Web.Models.Clients;

public sealed class ClientActivityTimelineItemViewModel
{
    public string WhenLabel { get; init; } = string.Empty;
    public string TypeLabel { get; init; } = string.Empty;
    public string DirectionLabel { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public string NextAction { get; init; } = string.Empty;
    public string NextActionDueLabel { get; init; } = string.Empty;
    public string OpportunityStageLabel { get; init; } = string.Empty;
}
