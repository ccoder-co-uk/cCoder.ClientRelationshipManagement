using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Platform.Models.Entities;

public class AgentRun : ICrmEntity
{
    public Guid Id { get; set; }
    public string CreatedBy { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public AgentRunKind Kind { get; set; }
    public AgentWorkLane? WorkLane { get; set; }
    public Guid? ProcessTaskId { get; set; }
    public Guid? ProcessStepId { get; set; }
    public string ProcessStepKey { get; set; }
    public AgentRunState State { get; set; }
    public string ExecutionUserId { get; set; }
    public string Provider { get; set; }
    public string Model { get; set; }
    public string WorkingDirectory { get; set; }
    public string Summary { get; set; }
    public string ErrorMessage { get; set; }
    public int Iterations { get; set; }
    public int ProcessedItemCount { get; set; }
    public DateTimeOffset StartedOn { get; set; }
    public DateTimeOffset? CompletedOn { get; set; }
}
