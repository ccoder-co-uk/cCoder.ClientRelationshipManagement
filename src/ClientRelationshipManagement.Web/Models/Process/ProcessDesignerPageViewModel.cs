using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.Web.Services.Processes;

namespace ClientRelationshipManagement.Web.Models.Process;

public sealed class ProcessDesignerPageViewModel
{
    public DateTimeOffset ValidatedOn { get; init; }
    public bool IsValid { get; init; }
    public IReadOnlyList<ProcessDesignerLaneViewModel> Lanes { get; init; } = [];
    public IReadOnlyList<ProcessValidationIssue> Issues { get; init; } = [];
}

public sealed class ProcessDesignerLaneViewModel
{
    public Guid ProcessDefinitionId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public ProcessScopeType ScopeType { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string CssClass { get; init; } = string.Empty;
    public IReadOnlyList<ProcessDesignerStepViewModel> Steps { get; init; } = [];
}

public sealed class ProcessDesignerStepViewModel
{
    public Guid Id { get; init; }
    public Guid ProcessDefinitionId { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Sequence { get; init; }
    public bool IsEntryPoint { get; init; }
    public string ActionType { get; init; } = string.Empty;
    public string Objective { get; init; } = string.Empty;
    public string RequiredFacts { get; init; } = string.Empty;
    public string ProducedFacts { get; init; } = string.Empty;
    public string ViabilityImpact { get; init; } = string.Empty;
    public ProcessDesignerStepHealthViewModel Health { get; init; } = new();
    public IReadOnlyList<ProcessDesignerTransitionViewModel> Transitions { get; init; } = [];
}

public sealed class ProcessDesignerTransitionViewModel
{
    public Guid Id { get; init; }
    public Guid? NextProcessStepId { get; init; }
    public string OutcomeLabel { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public bool IsTerminal { get; init; }
}

public sealed class ProcessDesignerStepHealthViewModel
{
    public int Pending { get; init; }
    public int Overdue { get; init; }
    public int Completed { get; init; }
    public int Cancelled { get; init; }
    public double? AverageTurnaroundMinutes { get; init; }
}

public sealed class ProcessDesignerStepHealthRow
{
    public Guid StepId { get; init; }
    public int Pending { get; init; }
    public int Overdue { get; init; }
    public int Completed { get; init; }
    public int Cancelled { get; init; }
    public double? AverageMinutes { get; init; }
}

public sealed class ReorderProcessStepsRequest
{
    public Guid ProcessDefinitionId { get; set; }
    public IReadOnlyList<Guid> StepIds { get; set; } = [];
}

public sealed class ConnectProcessStepsRequest
{
    public Guid ProcessDefinitionId { get; set; }
    public Guid FromStepId { get; set; }
    public Guid ToStepId { get; set; }
}
