using System.ComponentModel.DataAnnotations;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Models.Clients;

public sealed class CreateClientOpportunityRequest
{
    public Guid ClientId { get; set; }
    public OpportunityType Type { get; set; } = OpportunityType.General;
    public SalesPipelineStage Stage { get; set; } = SalesPipelineStage.Researched;
    public decimal? EstimatedAnnualValue { get; set; }
    public decimal? Probability { get; set; }
    [Required]
    public string PainSummary { get; set; } = string.Empty;
    public string ValueHypothesis { get; set; } = string.Empty;
    public string DecisionProcess { get; set; } = string.Empty;
    public string NextAction { get; set; } = string.Empty;
    public DateTime? NextActionDueOn { get; set; }
}
