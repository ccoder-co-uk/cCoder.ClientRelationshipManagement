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
    public string CompanyNumber { get; init; } = string.Empty;
    public string CompanyStatus { get; init; } = string.Empty;
    public string CompanyWebsiteUrl { get; init; } = string.Empty;
    public string CompanyRegistryUrl { get; init; } = string.Empty;
    public string CompanySicCodes { get; init; } = string.Empty;
    public string CompanyRegisteredOffice { get; init; } = string.Empty;
    public string ExistingResearchSummary { get; init; } = string.Empty;
    public string ExistingQualificationNotes { get; init; } = string.Empty;
    public string ContactName { get; init; } = string.Empty;
    public string ContactEmailAddress { get; init; } = string.Empty;
    public string OwnerUserId { get; init; } = string.Empty;
    public string OwnerDisplayName { get; init; } = string.Empty;
    public DateTimeOffset DueOn { get; init; }
    public ProcessActionType ActionType { get; init; }
    public string StepKey { get; init; } = string.Empty;
    public string StepObjective { get; init; } = string.Empty;
    public string RequiredFacts { get; init; } = string.Empty;
    public string ProducedFacts { get; init; } = string.Empty;
    public string ViabilityImpact { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Instructions { get; init; } = string.Empty;
    public string EmailSubjectTemplate { get; init; } = string.Empty;
    public string EmailBodyTemplate { get; init; } = string.Empty;
    public string CallScriptTemplate { get; init; } = string.Empty;
    public string QuestionSetTemplate { get; init; } = string.Empty;
    public string ExistingEmailState { get; init; } = string.Empty;
    public string ExistingEmailSubject { get; init; } = string.Empty;
    public string ExistingEmailBody { get; init; } = string.Empty;
    public IReadOnlyList<AgentCompanyHistoryViewModel> CompanyHistory { get; init; } = [];
    public IReadOnlyList<AgentTaskOutcomeViewModel> AvailableOutcomes { get; init; } = [];
}

public sealed class AgentCompanyHistoryViewModel
{
    public DateTimeOffset OccurredOn { get; init; }
    public string Lane { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string FactKey { get; init; } = string.Empty;
    public string FactValue { get; init; } = string.Empty;
    public string Confidence { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
}
