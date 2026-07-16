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
    public IReadOnlyList<ProcessStepPerformanceMetricsViewModel> Steps { get; init; } = [];
}

public sealed class ProcessStepPerformanceMetricsViewModel
{
    public Guid ProcessStepId { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Sequence { get; init; }
    public int PendingCount { get; init; }
    public int OverdueCount { get; init; }
    public int CompletedCount { get; init; }
    public int CancelledCount { get; init; }
    public int CompletedWithoutEvidenceCount { get; init; }
    public double? AverageTurnaroundMinutes { get; init; }
    public DateTimeOffset? OldestPendingSince { get; init; }
}
