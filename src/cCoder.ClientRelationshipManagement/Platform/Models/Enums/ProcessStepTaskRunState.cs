namespace cCoder.ClientRelationshipManagement.Platform.Models.Enums;

public enum ProcessStepTaskRunState
{
    Pending = 0,
    Running = 10,
    Completed = 20,
    Blocked = 30,
    Failed = 40,
    Cancelled = 50
}
