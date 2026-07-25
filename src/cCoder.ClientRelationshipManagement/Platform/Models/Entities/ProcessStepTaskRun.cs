using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class ProcessStepTaskRun : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
    public Guid ProcessTaskId { get; set; }
    public Guid ProcessStepTaskId { get; set; }
    public ProcessStepTaskRunState State { get; set; }
    public int AttemptCount { get; set; }
    public string ContextJson { get; set; }
    public string ValidationErrors { get; set; }
    public DateTimeOffset? StartedOn { get; set; }
    public DateTimeOffset? CompletedOn { get; set; }
    public virtual ProcessTask ProcessTask { get; set; }
    public virtual ProcessStepTask ProcessStepTask { get; set; }
    public virtual ICollection<ProcessStepTaskAttempt> Attempts { get; set; } = new List<ProcessStepTaskAttempt>();
}
