namespace ClientRelationshipManagement.Web.Models.Opportunities;

public sealed class OpportunityDetailsViewModel
{
    public Guid Id { get; init; }
    public Guid RelationshipId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string CompanyNumber { get; init; } = string.Empty;
    public string RelationshipStatus { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public string PrimaryContact { get; init; } = string.Empty;
    public string EstimatedValue { get; init; } = string.Empty;
    public string Probability { get; init; } = string.Empty;
    public string OpportunitySummary { get; init; } = string.Empty;
    public string PainSummary { get; init; } = string.Empty;
    public string ValueHypothesis { get; init; } = string.Empty;
    public string DecisionProcess { get; init; } = string.Empty;
    public IReadOnlyList<OpportunityDetailEvidenceViewModel> LeadEvidence { get; init; } = [];
    public IReadOnlyList<OpportunityDetailActivityViewModel> Activities { get; init; } = [];
    public IReadOnlyList<OpportunityDetailTaskViewModel> Tasks { get; init; } = [];
    public IReadOnlyList<OpportunityDetailArtifactViewModel> Artifacts { get; init; } = [];
}

public sealed class OpportunityDetailEvidenceViewModel
{
    public string Status { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

public sealed class OpportunityDetailActivityViewModel
{
    public DateTimeOffset OccurredOn { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
}

public sealed class OpportunityDetailTaskViewModel
{
    public string Step { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public DateTimeOffset DueOn { get; init; }
}

public sealed class OpportunityDetailArtifactViewModel
{
    public DateTimeOffset OccurredOn { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public string Confidence { get; init; } = string.Empty;
}
