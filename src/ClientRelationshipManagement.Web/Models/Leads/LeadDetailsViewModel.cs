namespace ClientRelationshipManagement.Web.Models.Leads;

public sealed class LeadDetailsViewModel
{
    public Guid Id { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string CompanyNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public int? RankingScore { get; init; }
    public string RankingRationale { get; init; } = string.Empty;
    public string QualificationNotes { get; init; } = string.Empty;
    public string SuppressionReason { get; init; } = string.Empty;
    public IReadOnlyList<LeadDetailContactViewModel> Contacts { get; init; } = [];
    public IReadOnlyList<LeadDetailTaskViewModel> Tasks { get; init; } = [];
    public IReadOnlyList<LeadDetailArtifactViewModel> Artifacts { get; init; } = [];
}

public sealed class LeadDetailContactViewModel
{
    public string Name { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
}

public sealed class LeadDetailTaskViewModel
{
    public string Step { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public DateTimeOffset DueOn { get; init; }
    public DateTimeOffset? CompletedOn { get; init; }
}

public sealed class LeadDetailArtifactViewModel
{
    public DateTimeOffset OccurredOn { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public string Confidence { get; init; } = string.Empty;
}
