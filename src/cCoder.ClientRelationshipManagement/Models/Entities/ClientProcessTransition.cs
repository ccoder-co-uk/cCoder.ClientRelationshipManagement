using cCoder.ClientRelationshipManagement.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Models.Entities;

public class ClientProcessTransition
{
    public Guid Id { get; set; }
    public Guid ClientProcessStepId { get; set; }
    public Guid? NextClientProcessStepId { get; set; }
    public string OutcomeKey { get; set; }
    public string OutcomeLabel { get; set; }
    public bool IsDefaultOutcome { get; set; }
    public bool IsTerminal { get; set; }
    public RelationshipStatus? TerminalStatus { get; set; }
    public PipelineStage? TerminalStage { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public virtual ClientProcessStep ClientProcessStep { get; set; }
    public virtual ClientProcessStep NextClientProcessStep { get; set; }
}
