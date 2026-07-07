using cCoder.ClientRelationshipManagement.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Models.Entities;

public class ClientProcessStep
{
    public Guid Id { get; set; }
    public Guid ClientProcessDefinitionId { get; set; }
    public string Key { get; set; }
    public string Name { get; set; }
    public int Sequence { get; set; }
    public bool IsEntryPoint { get; set; }
    public ClientProcessActionType ActionType { get; set; }
    public RelationshipStatus? StatusOnActivate { get; set; }
    public PipelineStage? StageOnActivate { get; set; }
    public int DueAfterDays { get; set; }
    public int DueAfterHours { get; set; }
    public string TaskTitleTemplate { get; set; }
    public string TaskInstructionsTemplate { get; set; }
    public string EmailSubjectTemplate { get; set; }
    public string EmailBodyTemplate { get; set; }
    public string CallScriptTemplate { get; set; }
    public string QuestionSetTemplate { get; set; }
    public bool IsActive { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public virtual ClientProcessDefinition ClientProcessDefinition { get; set; }
    public virtual ICollection<ClientProcessTransition> OutgoingTransitions { get; set; } = new List<ClientProcessTransition>();
    public virtual ICollection<ClientProcessTransition> IncomingTransitions { get; set; } = new List<ClientProcessTransition>();
    public virtual ICollection<ClientProcessTask> Tasks { get; set; } = new List<ClientProcessTask>();
    public virtual ICollection<ClientProcessInstance> CurrentInstances { get; set; } = new List<ClientProcessInstance>();
}
