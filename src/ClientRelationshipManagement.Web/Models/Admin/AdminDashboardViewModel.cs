using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Models.Admin;

public sealed class AdminDashboardViewModel
{
    public string Notice { get; init; } = string.Empty;
    public int PendingEmailApprovalCount { get; init; }
    public int PendingAgentMessageCount { get; init; }
    public int PendingProcessProposalCount { get; init; }
    public IReadOnlyList<AdminAgentRunViewModel> RecentRuns { get; init; } = [];
    public IReadOnlyList<AdminAgentMessageViewModel> PendingMessages { get; init; } = [];
    public IReadOnlyList<AdminProcessDraftViewModel> DraftProcesses { get; init; } = [];
}

public sealed class AdminAgentRunViewModel
{
    public Guid Id { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string ExecutionUserId { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public int Iterations { get; init; }
    public int ProcessedItemCount { get; init; }
    public string StartedOn { get; init; } = string.Empty;
    public string CompletedOn { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class AdminAgentMessageViewModel
{
    public Guid Id { get; init; }
    public Guid? ProposedProcessDefinitionId { get; init; }
    public AgentMessageKind Kind { get; init; }
    public AgentMessageState State { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string AgentName { get; init; } = string.Empty;
    public string ContextLink { get; init; } = string.Empty;
    public string CreatedOn { get; init; } = string.Empty;
}

public sealed class AdminProcessDraftViewModel
{
    public Guid Id { get; init; }
    public Guid? SupersedesProcessDefinitionId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ScopeType { get; init; } = string.Empty;
    public int VersionNumber { get; init; }
    public string ChangeSummary { get; init; } = string.Empty;
    public string ProposedByAgent { get; init; } = string.Empty;
    public string CreatedOn { get; init; } = string.Empty;
}
