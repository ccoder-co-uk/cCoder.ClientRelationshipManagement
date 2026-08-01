namespace cCoder.ClientRelationshipManagement.Runtime.Services.Processes;

public sealed class WorkflowRuleViolationException(string message) : InvalidOperationException(message);
