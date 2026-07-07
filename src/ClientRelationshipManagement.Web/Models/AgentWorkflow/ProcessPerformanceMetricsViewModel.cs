namespace ClientRelationshipManagement.Web.Models.AgentWorkflow;

public sealed class ProcessPerformanceMetricsViewModel
{
    public Guid ProcessDefinitionId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public string ScopeType { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int ActiveInstanceCount { get; init; }
    public int PendingTaskCount { get; init; }
    public int SentEmailCount { get; init; }
    public int ReplyActivityCount { get; init; }
    public int WonCount { get; init; }
    public int LostCount { get; init; }
}
