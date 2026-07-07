using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Models.Process;

public sealed class ProcessDefinitionSummaryViewModel
{
    public Guid Id { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public ProcessScopeType ScopeType { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; }
}
