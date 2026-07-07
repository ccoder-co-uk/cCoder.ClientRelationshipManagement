using System.ComponentModel.DataAnnotations;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Models.Home;

public sealed class SaveTodoDraftEmailRequest
{
    public Guid ClientId { get; set; }
    public Guid Id { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid? EmailId { get; set; }
    public Guid ClientMaterialId { get; set; }
    public Guid? ClientOpportunityId { get; set; }
    public ActivityDirection Direction { get; set; } = ActivityDirection.Outbound;
    public string ToAddresses { get; set; } = string.Empty;
    public string CcAddresses { get; set; } = string.Empty;
    public string BccAddresses { get; set; } = string.Empty;
    public DateTime? ScheduledSendOn { get; set; }
    [Required]
    public string Subject { get; set; } = string.Empty;
    [Required]
    public string Body { get; set; } = string.Empty;
}
