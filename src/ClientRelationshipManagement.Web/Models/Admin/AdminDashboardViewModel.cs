using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Models.Admin;

public sealed class AdminDashboardViewModel
{
    public string Notice { get; init; } = string.Empty;
    public int PendingEmailApprovalCount { get; init; }
    public int PendingAgentMessageCount { get; init; }
    public int PendingProcessProposalCount { get; init; }
    public string SelectedAiProfileKey { get; init; } = string.Empty;
    public string SelectedAiModel { get; init; } = string.Empty;
    public int ApprovalConcurrency { get; init; } = 2;
    public IReadOnlyList<AdminAiProfileViewModel> AiProfiles { get; init; } = [];
    public IReadOnlyList<AdminAiWorkLaneViewModel> AiWorkLanes { get; init; } = [];
}

public sealed class ConfigureAiRoutingRequest
{
    public string ApprovalProfileKey { get; init; } = string.Empty;
    public string ApprovalModel { get; init; } = string.Empty;
    public int ApprovalConcurrency { get; init; } = 2;
    public string LeadProfileKey { get; init; } = string.Empty;
    public string LeadModel { get; init; } = string.Empty;
    public int LeadConcurrency { get; init; } = 1;
    public string OpportunityProfileKey { get; init; } = string.Empty;
    public string OpportunityModel { get; init; } = string.Empty;
    public int OpportunityConcurrency { get; init; } = 1;
    public string ClientProfileKey { get; init; } = string.Empty;
    public string ClientModel { get; init; } = string.Empty;
    public int ClientConcurrency { get; init; } = 1;
}

public sealed class AdminAiWorkLaneViewModel
{
    public string Lane { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string SelectedProfileKey { get; init; } = string.Empty;
    public string SelectedModel { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public int Concurrency { get; init; } = 1;
}

public sealed class AdminAiProfileViewModel
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty;
    public int MaxConcurrency { get; init; } = 1;
    public bool IsConfigured { get; init; }
}

public sealed class AdminAgentRunViewModel
{
    public Guid Id { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string WorkLane { get; init; } = string.Empty;
    public Guid? ProcessTaskId { get; init; }
    public Guid? ProcessStepId { get; init; }
    public string ProcessStepKey { get; init; } = string.Empty;
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
    public string ContextLabel { get; init; } = string.Empty;
    public string CreatedOn { get; init; } = string.Empty;
    public string LastUpdatedOn { get; init; } = string.Empty;
    public int EntryCount { get; init; }
    public bool IsAwaitingAgent { get; init; }
    public IReadOnlyList<AdminAgentMessageEntryViewModel> Entries { get; init; } = [];
}

public sealed class AdminAgentMessageEntryViewModel
{
    public Guid Id { get; init; }
    public string Role { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
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

public sealed class AdminAgentRunsPageViewModel
{
    public IReadOnlyList<AdminAgentRunViewModel> Runs { get; init; } = [];
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public int TotalCount { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public sealed class AdminAgentMessagesPageViewModel
{
    public IReadOnlyList<AdminAgentMessageViewModel> Messages { get; init; } = [];
}

public sealed class AdminAgentConversationPageViewModel
{
    public AdminAgentMessageViewModel Conversation { get; init; } = new();
    public IReadOnlyList<AdminAgentMessageViewModel> Conversations { get; init; } = [];
}

public sealed class AdminProcessProposalsPageViewModel
{
    public IReadOnlyList<AdminProcessDraftViewModel> Proposals { get; init; } = [];
}
