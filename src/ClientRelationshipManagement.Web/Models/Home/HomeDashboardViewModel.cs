using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClientRelationshipManagement.Web.Models.Home;

public class HomeDashboardViewModel
{
    public string Notice { get; init; }
    public int TotalClients { get; init; }
    public int TotalOpenActions { get; init; }
    public int OverdueActions { get; init; }
    public int DueTodayActions { get; init; }
    public int AdditionalActionCount { get; init; }
    public IReadOnlyList<SelectListItem> StatusOptions { get; init; } =
        Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> StageOptions { get; init; } =
        Array.Empty<SelectListItem>();
    public IReadOnlyList<ClientStateSummaryViewModel> ClientStateSummaries { get; init; } =
        Array.Empty<ClientStateSummaryViewModel>();
    public IReadOnlyList<TodoItemViewModel> TodoItems { get; init; } =
        Array.Empty<TodoItemViewModel>();
}
