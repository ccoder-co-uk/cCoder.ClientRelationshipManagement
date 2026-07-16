namespace ClientRelationshipManagement.Web.Models.Emails;

public sealed class MarkEmailSentRequest : EmailGridRequest
{
    public Guid EmailId { get; set; }
    public Guid ClientId { get; set; }
}
