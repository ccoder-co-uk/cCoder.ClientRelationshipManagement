namespace ClientRelationshipManagement.Web.Models.Companies;

public sealed class CompanyHistoryPageViewModel
{
    public Guid CompanyId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string CompanyNumber { get; init; } = string.Empty;
    public string CompanyStatus { get; init; } = string.Empty;
    public IReadOnlyList<CompanyHistoryItemViewModel> Items { get; init; } = [];
}

public sealed class CompanyHistoryItemViewModel
{
    public DateTimeOffset OccurredOn { get; init; }
    public string Lane { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public string FactKey { get; init; } = string.Empty;
    public string FactValue { get; init; } = string.Empty;
    public string Confidence { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
}
