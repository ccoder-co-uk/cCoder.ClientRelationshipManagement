namespace ClientRelationshipManagement.Web.Models.Emails;

public sealed class EmailListItemViewModel
{
    public Guid Id { get; init; }
    public Guid ClientId { get; init; }
    public Guid? ClientMaterialId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string StateLabel { get; init; } = string.Empty;
    public string ToAddresses { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Preview { get; init; } = string.Empty;
    public string ScheduledSendLabel { get; init; } = string.Empty;
    public string ScheduledSendValue { get; init; } = string.Empty;
    public string SentOnLabel { get; init; } = string.Empty;
    public string CreatedOnLabel { get; init; } = string.Empty;
    public string LastError { get; init; } = string.Empty;
    public bool CanApprove { get; init; }
    public bool CanMarkSent { get; init; }
}
