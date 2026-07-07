namespace ClientRelationshipManagement.Web.Models.AgentWorkflow;

public sealed class AgentTaskOutcomeViewModel
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
}
