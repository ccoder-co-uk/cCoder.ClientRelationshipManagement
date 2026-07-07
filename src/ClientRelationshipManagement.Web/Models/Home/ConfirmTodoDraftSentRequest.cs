namespace ClientRelationshipManagement.Web.Models.Home;

public sealed class ConfirmTodoDraftSentRequest
{
    public Guid ClientId { get; set; }
    public Guid Id { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid EmailId { get; set; }
}
