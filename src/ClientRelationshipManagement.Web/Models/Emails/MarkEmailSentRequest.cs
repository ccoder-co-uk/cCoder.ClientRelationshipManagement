namespace ClientRelationshipManagement.Web.Models.Emails;

public sealed class MarkEmailSentRequest
{
    public Guid EmailId { get; set; }
    public Guid ClientId { get; set; }
}
