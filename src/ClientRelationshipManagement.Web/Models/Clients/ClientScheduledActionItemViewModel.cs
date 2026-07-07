namespace ClientRelationshipManagement.Web.Models.Clients;

public sealed class ClientScheduledActionItemViewModel
{
    public string TypeLabel { get; init; } = string.Empty;
    public string SourceLabel { get; init; } = string.Empty;
    public string ActionText { get; init; } = string.Empty;
    public string DueLabel { get; init; } = string.Empty;
    public DateTime SortOn { get; init; }
    public int SourcePriority { get; init; }
}
