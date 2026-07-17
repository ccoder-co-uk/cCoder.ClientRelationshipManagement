using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class ProcessStepTask : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
    public Guid ProcessStepId { get; set; }
    public string Key { get; set; }
    public string Name { get; set; }
    public int Sequence { get; set; }
    public ProcessStepTaskType Type { get; set; }
    public string HandlerKey { get; set; }
    public string InstructionsTemplate { get; set; }
    public string RequiredContextKeys { get; set; }
    public string ProducedContextKeys { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public bool IsActive { get; set; } = true;
    public string NextTaskKey { get; set; }
    public string FailureTaskKey { get; set; }
    public virtual ProcessStep ProcessStep { get; set; }
    public virtual ICollection<ProcessStepTaskRun> Runs { get; set; } = new List<ProcessStepTaskRun>();
}
