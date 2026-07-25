namespace ClientRelationshipManagement.Web.Models.Admin;

public sealed class ProcessProposalReviewViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ScopeType { get; init; } = string.Empty;
    public int CurrentVersion { get; init; }
    public int ProposedVersion { get; init; }
    public string ChangeSummary { get; init; } = string.Empty;
    public string ProposedBy { get; init; } = string.Empty;
    public string CreatedOn { get; init; } = string.Empty;
    public IReadOnlyList<ProcessProposalStepViewModel> Steps { get; init; } = [];
    public IReadOnlyList<ProcessProposalChangeViewModel> Changes { get; init; } = [];
    public int ChangedStepCount => Steps.Count(step => step.ChangeState != "unchanged");
    public bool IsNoOp => Changes.Count == 0;
    public int MaterialChangeCount => Changes.Count;
    public bool HasRoutingChanges => Changes.Any(change => change.Category == "Routing");
}

public sealed class ProcessProposalStepViewModel
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Sequence { get; init; }
    public string ActionType { get; init; } = string.Empty;
    public string ChangeState { get; init; } = "unchanged";
    public int ChangeCount { get; init; }
    public int TaskCount { get; init; }
}

public sealed class ProcessProposalChangeViewModel
{
    public string Anchor { get; init; } = string.Empty;
    public string StepKey { get; init; } = string.Empty;
    public string StepName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Property { get; init; } = string.Empty;
    public string CurrentValue { get; init; } = string.Empty;
    public string ProposedValue { get; init; } = string.Empty;
    public string ChangeType { get; init; } = "modified";
    public bool IsLongText => Math.Max(CurrentValue.Length, ProposedValue.Length) > 140
        || CurrentValue.Contains('\n') || ProposedValue.Contains('\n');
}
