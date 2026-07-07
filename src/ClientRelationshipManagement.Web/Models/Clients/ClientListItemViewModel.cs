namespace ClientRelationshipManagement.Web.Models.Clients;

public sealed class ClientListItemViewModel
{
    public Guid Id { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string AccountOwner { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string StageLabel { get; init; } = string.Empty;
    public string PriorityLabel { get; init; } = string.Empty;
    public string LeadSource { get; init; } = string.Empty;
    public string NextAction { get; init; } = string.Empty;
    public string NextActionDueLabel { get; init; } = string.Empty;
}
