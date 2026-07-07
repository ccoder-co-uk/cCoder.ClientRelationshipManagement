using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClientRelationshipManagement.Web.Models.Clients;

public sealed class ClientListPageViewModel
{
    public string Notice { get; init; } = string.Empty;
    public int TotalClients { get; init; }
    public string Search { get; init; } = string.Empty;
    public string StatusFilter { get; init; } = string.Empty;
    public string Sort { get; init; } = string.Empty;
    public IReadOnlyList<SelectListItem> StatusOptions { get; init; } =
        Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> SortOptions { get; init; } =
        Array.Empty<SelectListItem>();
    public IReadOnlyList<ClientListItemViewModel> Clients { get; init; } =
        Array.Empty<ClientListItemViewModel>();
}
