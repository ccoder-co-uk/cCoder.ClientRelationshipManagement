namespace ClientRelationshipManagement.Web.Models.Emails;

public sealed class ApproveEmailRequest
{
    public Guid EmailId { get; set; }
    public Guid ClientId { get; set; }
    public DateTime? ScheduledSendOn { get; set; }
}
