using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClientRelationshipManagement.Web.Models.Process;

public sealed class ProcessTransitionEditorViewModel
{
    public Guid? Id { get; init; }
    public Guid ProcessStepId { get; init; }
    public Guid? NextProcessStepId { get; init; }
    public string OutcomeKey { get; init; } = string.Empty;
    public string OutcomeLabel { get; init; } = string.Empty;
    public bool IsDefaultOutcome { get; init; }
    public bool IsTerminal { get; init; }
    public ProcessTransitionEffect Effect { get; init; }
    public RelationshipStatus? ResultingRelationshipStatus { get; init; }
    public SalesPipelineStage? ResultingSalesStage { get; init; }
    public ClientAccountStatus? ResultingClientAccountStatus { get; init; }
    public IReadOnlyList<SelectListItem> NextStepOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> EffectOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> RelationshipStatusOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> SalesStageOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> ClientAccountStatusOptions { get; init; } = Array.Empty<SelectListItem>();
}
