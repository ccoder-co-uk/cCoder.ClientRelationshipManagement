using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClientRelationshipManagement.Web.Models.Leads;

public sealed class LeadsPageViewModel
{
    public string Notice { get; init; } = string.Empty;
    public string Search { get; init; } = string.Empty;
    public string StatusFilter { get; init; } = string.Empty;
    public IReadOnlyList<SelectListItem> StatusOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<LeadListItemViewModel> Leads { get; init; } = Array.Empty<LeadListItemViewModel>();
    public LeadEditorViewModel NewLead { get; init; } = new();
}
