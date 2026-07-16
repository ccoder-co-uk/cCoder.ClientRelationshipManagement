using System.ComponentModel.DataAnnotations;

namespace ClientRelationshipManagement.Web.Models.Emails;

public sealed class ReviewEmailRequest
{
    public Guid EmailId { get; set; }
    public Guid ClientId { get; set; }
    [Required]
    public string Subject { get; set; } = string.Empty;
    [Required]
    public string Body { get; set; } = string.Empty;
    public DateTime? ScheduledSendOn { get; set; }
}

public sealed class RejectEmailRequest
{
    public Guid EmailId { get; set; }
    public Guid ClientId { get; set; }
    [Required]
    [MinLength(5)]
    public string Reason { get; set; } = string.Empty;
}
