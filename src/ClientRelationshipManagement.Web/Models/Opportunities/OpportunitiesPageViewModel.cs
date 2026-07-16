using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClientRelationshipManagement.Web.Models.Opportunities;

public sealed class OpportunitiesPageViewModel
{
    public string Notice { get; init; } = string.Empty;
    public int TotalOpportunities { get; init; }
    public string Search { get; init; } = string.Empty;
    public string StageFilter { get; init; } = string.Empty;
    public string StatusFilter { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string TaskFilter { get; init; } = string.Empty;
    public string Sort { get; init; } = string.Empty;
    public IReadOnlyList<SelectListItem> StageOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> StatusOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> SortOptions { get; init; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<OpportunityListItemViewModel> Opportunities { get; init; } = Array.Empty<OpportunityListItemViewModel>();
}
