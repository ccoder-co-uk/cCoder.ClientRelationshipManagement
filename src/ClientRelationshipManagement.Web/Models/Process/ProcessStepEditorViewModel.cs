using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClientRelationshipManagement.Web.Models.Process;

public sealed class ProcessStepEditorViewModel
{
    public Guid? Id { get; init; }
    public Guid ProcessDefinitionId { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Sequence { get; init; }
    public bool IsEntryPoint { get; init; }
    public bool IsActive { get; init; }
    public ProcessActionType ActionType { get; init; }
    public RelationshipStatus? RelationshipStatusOnActivate { get; init; }
    public SalesPipelineStage? SalesStageOnActivate { get; init; }
    public ClientAccountStatus? ClientAccountStatusOnActivate { get; init; }
    public int DueAfterDays { get; init; }
    public int DueAfterHours { get; init; }
    public string TaskTitleTemplate { get; init; } = string.Empty;
    public string TaskInstructionsTemplate { get; init; } = string.Empty;
    public string EmailSubjectTemplate { get; init; } = string.Empty;
    public string EmailBodyTemplate { get; init; } = string.Empty;
    public string CallScriptTemplate { get; init; } = string.Empty;
    public string QuestionSetTemplate { get; init; } = string.Empty;
    public IReadOnlyList<SelectListItem> ActionTypeOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> RelationshipStatusOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> SalesStageOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> ClientAccountStatusOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<ProcessTransitionEditorViewModel> Transitions { get; init; } = Array.Empty<ProcessTransitionEditorViewModel>();
}
