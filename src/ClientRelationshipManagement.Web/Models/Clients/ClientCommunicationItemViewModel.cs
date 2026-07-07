namespace ClientRelationshipManagement.Web.Models.Clients;

public sealed class ClientCommunicationItemViewModel
{
    public Guid? ActivityId { get; init; }
    public Guid? EmailId { get; init; }
    public Guid? MaterialId { get; init; }
    public string WhenLabel { get; init; } = string.Empty;
    public string TypeLabel { get; init; } = string.Empty;
    public string DirectionLabel { get; init; } = string.Empty;
    public string DirectionValue { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string ToAddresses { get; init; } = string.Empty;
    public string CcAddresses { get; init; } = string.Empty;
    public string BccAddresses { get; init; } = string.Empty;
    public string ScheduledSendOnLabel { get; init; } = string.Empty;
    public string ScheduledSendOnValue { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string NextAction { get; init; } = string.Empty;
    public string NextActionDueLabel { get; init; } = string.Empty;
    public string NextActionDueValue { get; init; } = string.Empty;
    public Guid? ClientOpportunityId { get; init; }
    public bool IsDraftEmail { get; init; }
}
