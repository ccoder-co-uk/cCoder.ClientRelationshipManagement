using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.Web.Services.Processes;

namespace ClientRelationshipManagement.Web.Models.Process;

public sealed class WorkflowModelPageViewModel
{
    public DateTimeOffset GeneratedOn { get; init; }
    public bool IsValid { get; init; }
    public IReadOnlyList<ProcessValidationIssue> Issues { get; init; } = [];
    public IReadOnlyList<WorkflowProcessViewModel> Processes { get; init; } = [];
    public WorkflowCompanyCoverageViewModel CompanyCoverage { get; init; } = new();
    public int TotalSteps => Processes.Sum(process => process.Steps.Count);
    public int TotalTransitions => Processes.Sum(process => process.Steps.Sum(step => step.Transitions.Count));
}

public sealed class WorkflowCompanyCoverageViewModel
{
    public long TotalCompanies { get; init; }
    public long CandidateCompanies { get; init; }
    public long LeadCompanies { get; init; }
    public long OpportunityCompanies { get; init; }
    public long ClientCompanies { get; init; }
    public long ExcludedCompanies { get; init; }
    public long ManualRelationshipCompanies { get; init; }
    public long UnclassifiedCompanies { get; init; }
    public long AccountedCompanies => Math.Max(0, TotalCompanies - UnclassifiedCompanies);
    public bool IsComplete => TotalCompanies == AccountedCompanies;
}

public sealed class WorkflowProcessViewModel
{
    public Guid Id { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public ProcessScopeType ScopeType { get; init; }
    public int VersionNumber { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string LaneKey { get; init; } = string.Empty;
    public string LaneLabel { get; init; } = string.Empty;
    public int ActiveInstances { get; init; }
    public int CompletedInstances { get; init; }
    public int UnmappedActiveInstances { get; init; }
    public long PortalCount { get; init; }
    public IReadOnlyList<WorkflowStepViewModel> Steps { get; init; } = [];
}

public sealed class WorkflowStepViewModel
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Sequence { get; init; }
    public bool IsEntryPoint { get; init; }
    public string ActionType { get; init; } = string.Empty;
    public string Objective { get; init; } = string.Empty;
    public string RequiredFacts { get; init; } = string.Empty;
    public string ProducedFacts { get; init; } = string.Empty;
    public string ViabilityImpact { get; init; } = string.Empty;
    public string TaskTitleTemplate { get; init; } = string.Empty;
    public string TaskInstructionsTemplate { get; init; } = string.Empty;
    public string QuestionSetTemplate { get; init; } = string.Empty;
    public int DueAfterDays { get; init; }
    public int DueAfterHours { get; init; }
    public string StateOnActivate { get; init; } = string.Empty;
    public int CurrentInstances { get; init; }
    public IReadOnlyList<WorkflowStepTaskViewModel> Tasks { get; init; } = [];
    public WorkflowStepHealthViewModel Health { get; init; } = new();
    public IReadOnlyList<WorkflowTransitionViewModel> Transitions { get; init; } = [];
}

public sealed class WorkflowStepTaskViewModel
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Sequence { get; init; }
    public string Type { get; init; } = string.Empty;
    public string HandlerKey { get; init; } = string.Empty;
    public int MaxAttempts { get; init; }
}

public sealed class WorkflowTransitionViewModel
{
    public Guid Id { get; init; }
    public string OutcomeKey { get; init; } = string.Empty;
    public string OutcomeLabel { get; init; } = string.Empty;
    public Guid? NextProcessStepId { get; init; }
    public string NextStepName { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public bool IsTerminal { get; init; }
    public ProcessTransitionEffect Effect { get; init; }
    public string EffectLabel { get; init; } = string.Empty;
    public string ResultingState { get; init; } = string.Empty;
    public string GraphTargetId { get; init; } = string.Empty;
    public string DestinationLabel { get; init; } = string.Empty;
    public int HistoricalCompletedCount { get; init; }
    public long CurrentStateCount { get; init; }
}

public sealed class WorkflowStepHealthViewModel
{
    public int Pending { get; init; }
    public int Running { get; init; }
    public int Overdue { get; init; }
    public int Completed { get; init; }
    public int Failed { get; init; }
    public double? AverageTurnaroundMinutes { get; init; }
}

public sealed class WorkflowStepHealthProjection
{
    public Guid StepId { get; init; }
    public int Pending { get; init; }
    public int Running { get; init; }
    public int Overdue { get; init; }
    public int Completed { get; init; }
    public int Failed { get; init; }
    public double? AverageMinutes { get; init; }
}

public sealed class WorkflowInstanceProjection
{
    public Guid ProcessDefinitionId { get; init; }
    public int Active { get; init; }
    public int Completed { get; init; }
}

public sealed class WorkflowLaneInstanceProjection
{
    public string TenantId { get; init; } = string.Empty;
    public ProcessScopeType ScopeType { get; init; }
    public int Active { get; init; }
    public int Completed { get; init; }
}

public sealed class WorkflowStepIdentityProjection
{
    public Guid StepId { get; init; }
    public Guid ProcessDefinitionId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public ProcessScopeType ScopeType { get; init; }
    public string StepKey { get; init; } = string.Empty;
}

public sealed class WorkflowCurrentStepProjection
{
    public Guid ProcessDefinitionId { get; init; }
    public Guid? CurrentProcessStepId { get; init; }
    public int Count { get; init; }
}

public sealed class WorkflowTransitionOutcomeProjection
{
    public Guid StepId { get; init; }
    public string OutcomeKey { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class WorkflowLeadCompanyProjection
{
    public Guid CompanyId { get; init; }
    public LeadStatus Status { get; init; }
    public DateTimeOffset LastUpdated { get; init; }
}

public sealed class WorkflowOpportunityCompanyProjection
{
    public Guid CompanyId { get; init; }
    public SalesPipelineStage Stage { get; init; }
    public DateTimeOffset LastUpdated { get; init; }
}

public sealed class WorkflowClientCompanyProjection
{
    public Guid CompanyId { get; init; }
    public ClientAccountStatus Status { get; init; }
    public DateTimeOffset LastUpdated { get; init; }
}
