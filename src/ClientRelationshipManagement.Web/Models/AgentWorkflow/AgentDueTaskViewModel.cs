using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Models.AgentWorkflow;

public sealed class AgentDueTaskViewModel
{
    public Guid ProcessTaskId { get; init; }
    public Guid? LeadId { get; init; }
    public Guid? ClientId { get; init; }
    public Guid? OpportunityId { get; init; }
    public Guid? ClientAccountId { get; init; }
    public Guid? EmailId { get; init; }
    public Guid? MaterialId { get; init; }
    public string ScopeType { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string ContactName { get; init; } = string.Empty;
    public string ContactEmailAddress { get; init; } = string.Empty;
    public string OwnerUserId { get; init; } = string.Empty;
    public string OwnerDisplayName { get; init; } = string.Empty;
    public DateTimeOffset DueOn { get; init; }
    public ProcessActionType ActionType { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Instructions { get; init; } = string.Empty;
    public string EmailSubjectTemplate { get; init; } = string.Empty;
    public string EmailBodyTemplate { get; init; } = string.Empty;
    public string CallScriptTemplate { get; init; } = string.Empty;
    public string QuestionSetTemplate { get; init; } = string.Empty;
    public string ExistingEmailState { get; init; } = string.Empty;
    public string ExistingEmailSubject { get; init; } = string.Empty;
    public string ExistingEmailBody { get; init; } = string.Empty;
    public IReadOnlyList<AgentTaskOutcomeViewModel> AvailableOutcomes { get; init; } = [];
}
