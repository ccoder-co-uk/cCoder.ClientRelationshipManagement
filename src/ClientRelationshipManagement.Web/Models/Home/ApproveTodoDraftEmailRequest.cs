namespace ClientRelationshipManagement.Web.Models.Home;

public sealed class ApproveTodoDraftEmailRequest
{
    public Guid ClientId { get; set; }
    public Guid EmailId { get; set; }
    public DateTime? ScheduledSendOn { get; set; }
}
