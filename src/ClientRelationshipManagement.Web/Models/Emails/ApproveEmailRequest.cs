namespace ClientRelationshipManagement.Web.Models.Emails;

public sealed class ApproveEmailRequest : EmailGridRequest
{
    public Guid EmailId { get; set; }
    public Guid ClientId { get; set; }
    public DateTime? ScheduledSendOn { get; set; }
}

public abstract class EmailGridRequest
{
    public string ReturnUrl { get; set; } = string.Empty;
}
