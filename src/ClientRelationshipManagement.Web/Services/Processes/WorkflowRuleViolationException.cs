namespace ClientRelationshipManagement.Web.Services.Processes;

public sealed class WorkflowRuleViolationException(string message) : InvalidOperationException(message);
