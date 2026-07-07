using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Models.Process;

public sealed class SaveProcessTransitionRequest
{
    public Guid? Id { get; set; }
    public Guid ProcessStepId { get; set; }
    public Guid? NextProcessStepId { get; set; }
    public string OutcomeKey { get; set; } = string.Empty;
    public string OutcomeLabel { get; set; } = string.Empty;
    public bool IsDefaultOutcome { get; set; }
    public bool IsTerminal { get; set; }
    public ProcessTransitionEffect Effect { get; set; }
    public RelationshipStatus? ResultingRelationshipStatus { get; set; }
    public SalesPipelineStage? ResultingSalesStage { get; set; }
    public ClientAccountStatus? ResultingClientAccountStatus { get; set; }
}
