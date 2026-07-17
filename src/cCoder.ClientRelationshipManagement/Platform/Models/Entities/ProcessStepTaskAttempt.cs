using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class ProcessStepTaskAttempt : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
    public Guid ProcessStepTaskRunId { get; set; }
    public int AttemptNumber { get; set; }
    public ProcessStepTaskRunState State { get; set; }
    public string InputContextJson { get; set; }
    public string OutputContextJson { get; set; }
    public string ValidationErrors { get; set; }
    public virtual ProcessStepTaskRun ProcessStepTaskRun { get; set; }
}
