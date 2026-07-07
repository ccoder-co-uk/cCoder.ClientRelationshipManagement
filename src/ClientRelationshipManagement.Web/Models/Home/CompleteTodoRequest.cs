using System.ComponentModel.DataAnnotations;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Models.Home;

public class CompleteTodoRequest
{
    public Guid Id { get; set; }
    public string SourceType { get; set; }
    [Required]
    public string CompletionNote { get; set; }
    public string OutcomeKey { get; set; }
    public string NextAction { get; set; }
    public DateTime? NextActionDueOn { get; set; }
    public RelationshipStatus? Status { get; set; }
    public SalesPipelineStage? Stage { get; set; }
}
