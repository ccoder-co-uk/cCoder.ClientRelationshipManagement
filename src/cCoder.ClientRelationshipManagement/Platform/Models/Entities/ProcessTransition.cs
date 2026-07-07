using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class ProcessTransition : AuditableEntity
{
    public Guid ProcessStepId { get; set; }
    public Guid? NextProcessStepId { get; set; }
    public string OutcomeKey { get; set; }
    public string OutcomeLabel { get; set; }
    public bool IsDefaultOutcome { get; set; }
    public bool IsTerminal { get; set; }
    public ProcessTransitionEffect Effect { get; set; }
    public RelationshipStatus? ResultingRelationshipStatus { get; set; }
    public SalesPipelineStage? ResultingSalesStage { get; set; }
    public ClientAccountStatus? ResultingClientAccountStatus { get; set; }

    public virtual ProcessStep ProcessStep { get; set; }
    public virtual ProcessStep NextProcessStep { get; set; }
}
