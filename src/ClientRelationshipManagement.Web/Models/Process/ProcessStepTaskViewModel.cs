namespace ClientRelationshipManagement.Web.Models.Process;

public sealed class ProcessStepTaskViewModel
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Sequence { get; init; }
    public string Type { get; init; } = string.Empty;
    public string HandlerKey { get; init; } = string.Empty;
    public string RequiredContextKeys { get; init; } = string.Empty;
    public string ProducedContextKeys { get; init; } = string.Empty;
    public int MaxAttempts { get; init; }
    public string NextTaskKey { get; init; } = string.Empty;
    public string FailureTaskKey { get; init; } = string.Empty;
}
