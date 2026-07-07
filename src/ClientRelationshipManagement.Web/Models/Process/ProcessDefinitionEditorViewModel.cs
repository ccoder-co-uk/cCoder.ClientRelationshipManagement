using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClientRelationshipManagement.Web.Models.Process;

public sealed class ProcessDefinitionEditorViewModel
{
    public Guid? Id { get; init; }
    public string TenantId { get; init; } = "default";
    public ProcessScopeType ScopeType { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; } = true;
    public IReadOnlyList<SelectListItem> ScopeTypeOptions { get; init; } = Array.Empty<SelectListItem>();
}
