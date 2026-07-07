namespace ClientRelationshipManagement.Web.Models.Clients;

public sealed class MarkClientEmailSentRequest
{
    public Guid ClientId { get; set; }
    public Guid EmailId { get; set; }
}
