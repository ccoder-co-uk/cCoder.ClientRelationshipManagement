namespace ClientRelationshipManagement.Web.Models.Clients;

public sealed class ApproveClientEmailRequest
{
    public Guid ClientId { get; set; }
    public Guid EmailId { get; set; }
    public DateTime? ScheduledSendOn { get; set; }
}
