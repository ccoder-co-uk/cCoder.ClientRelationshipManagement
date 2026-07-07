using System.ComponentModel.DataAnnotations;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Models.Clients;

public sealed class DraftClientEmailRequest
{
    public Guid ClientId { get; set; }
    public Guid? EmailId { get; set; }
    public Guid? ClientMaterialId { get; set; }
    public Guid? ClientOpportunityId { get; set; }
    public Guid? ClientAccountId { get; set; }
    public DateTime? ActivityOn { get; set; }
    public ActivityDirection Direction { get; set; } = ActivityDirection.Outbound;
    public string ToAddresses { get; set; } = string.Empty;
    public string CcAddresses { get; set; } = string.Empty;
    public string BccAddresses { get; set; } = string.Empty;
    public DateTime? ScheduledSendOn { get; set; }
    [Required]
    public string Subject { get; set; } = string.Empty;
    [Required]
    public string Body { get; set; } = string.Empty;
    public string NextAction { get; set; } = string.Empty;
    public DateTime? NextActionDueOn { get; set; }
}
