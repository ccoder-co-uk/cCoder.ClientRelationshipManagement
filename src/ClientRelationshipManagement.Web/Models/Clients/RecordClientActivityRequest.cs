using System.ComponentModel.DataAnnotations;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Models.Clients;

public sealed class RecordClientActivityRequest
{
    public Guid ClientId { get; set; }
    public Guid? ClientOpportunityId { get; set; }
    public Guid? ClientAccountId { get; set; }
    public DateTime? ActivityOn { get; set; }
    public ActivityType Type { get; set; } = ActivityType.Note;
    public ActivityDirection Direction { get; set; } = ActivityDirection.Internal;
    [Required]
    public string Summary { get; set; } = string.Empty;
    [Required]
    public string Outcome { get; set; } = string.Empty;
    public string NextAction { get; set; } = string.Empty;
    public DateTime? NextActionDueOn { get; set; }
}
