using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class ProcessStep : AuditableEntity
{
    public Guid ProcessDefinitionId { get; set; }
    public string Key { get; set; }
    public string Name { get; set; }
    public string Objective { get; set; }
    public string RequiredFacts { get; set; }
    public string ProducedFacts { get; set; }
    public string ViabilityImpact { get; set; }
    public int Sequence { get; set; }
    public bool IsEntryPoint { get; set; }
    public bool IsActive { get; set; }
    public ProcessActionType ActionType { get; set; }
    public int DueAfterDays { get; set; }
    public int DueAfterHours { get; set; }
    public RelationshipStatus? RelationshipStatusOnActivate { get; set; }
    public SalesPipelineStage? SalesStageOnActivate { get; set; }
    public ClientAccountStatus? ClientAccountStatusOnActivate { get; set; }
    public string TaskTitleTemplate { get; set; }
    public string TaskInstructionsTemplate { get; set; }
    public ProcessEmailRecipientTarget EmailRecipientTarget { get; set; }
    public string EmailSubjectTemplate { get; set; }
    public string EmailBodyTemplate { get; set; }
    public string CallScriptTemplate { get; set; }
    public string QuestionSetTemplate { get; set; }

    public virtual ProcessDefinition ProcessDefinition { get; set; }
    public virtual ICollection<ProcessTransition> OutgoingTransitions { get; set; } = new List<ProcessTransition>();
    public virtual ICollection<ProcessTransition> IncomingTransitions { get; set; } = new List<ProcessTransition>();
    public virtual ICollection<ProcessInstance> CurrentInstances { get; set; } = new List<ProcessInstance>();
    public virtual ICollection<ProcessTask> Tasks { get; set; } = new List<ProcessTask>();
}
