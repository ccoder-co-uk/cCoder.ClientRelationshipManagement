namespace ClientRelationshipManagement.Web.Models.AgentWorkflow;

public sealed class CompleteAgentTaskRequest
{
    public string OutcomeKey { get; set; } = string.Empty;
    public string CompletionNote { get; set; } = string.Empty;
}
