using cCoder.ClientRelationshipManagement.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Models.Entities;

public class ClientProcessInstance
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Guid ClientProcessDefinitionId { get; set; }
    public Guid? CurrentClientProcessStepId { get; set; }
    public Guid? CurrentClientProcessTaskId { get; set; }
    public ClientProcessInstanceState State { get; set; }
    public string CompletionOutcomeKey { get; set; }
    public DateTimeOffset StartedOn { get; set; }
    public DateTimeOffset? CompletedOn { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public virtual Client Client { get; set; }
    public virtual ClientProcessDefinition ClientProcessDefinition { get; set; }
    public virtual ClientProcessStep CurrentClientProcessStep { get; set; }
    public virtual ClientProcessTask CurrentClientProcessTask { get; set; }
    public virtual ICollection<ClientProcessTask> Tasks { get; set; } = new List<ClientProcessTask>();
}
