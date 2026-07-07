using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Models.Process;

public sealed class SaveProcessDefinitionRequest
{
    public Guid? Id { get; set; }
    public string TenantId { get; set; } = "default";
    public ProcessScopeType ScopeType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}
