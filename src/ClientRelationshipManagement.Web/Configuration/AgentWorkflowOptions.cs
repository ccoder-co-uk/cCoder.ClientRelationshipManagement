namespace ClientRelationshipManagement.Web.Configuration;

public sealed class AgentWorkflowOptions
{
    public bool Enabled { get; set; }
    public bool TaskAgentEnabled { get; set; }
    public bool ProcessOptimiserEnabled { get; set; }
    public int TaskAgentIntervalMinutes { get; set; } = 10;
    public int ProcessOptimiserIntervalMinutes { get; set; } = 60;
    public string ExecutionUserId { get; set; } = string.Empty;
    public string AgentWorkspacePath { get; set; } = "Agent Workspace";
    public string CrmApiBaseUrl { get; set; } = string.Empty;
    public string TaskAgentProvider { get; set; } = string.Empty;
    public string TaskAgentModel { get; set; } = string.Empty;
    public string ProcessOptimiserProvider { get; set; } = string.Empty;
    public string ProcessOptimiserModel { get; set; } = string.Empty;
    public int MaxIterations { get; set; } = 30;
    public int SessionArchiveLimit { get; set; } = 100;
}
