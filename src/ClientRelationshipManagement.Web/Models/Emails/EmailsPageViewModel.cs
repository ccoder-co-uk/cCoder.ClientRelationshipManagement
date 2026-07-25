using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClientRelationshipManagement.Web.Models.Emails;

public sealed class EmailsPageViewModel
{
    public string Notice { get; init; } = string.Empty;
    public string Search { get; init; } = string.Empty;
    public string StateFilter { get; init; } = string.Empty;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public int TotalPages { get; init; } = 1;
    public int TotalCount { get; init; }
    public int FirstItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int LastItem => Math.Min(Page * PageSize, TotalCount);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
    public IReadOnlyList<SelectListItem> StateOptions { get; init; } = Array.Empty<SelectListItem>();
    public int TotalEmails { get; init; }
    public int DraftEmails { get; init; }
    public int FailedEmails { get; init; }
    public int ApprovedEmails { get; init; }
    public int SentEmails { get; init; }
    public IReadOnlyList<EmailListItemViewModel> Emails { get; init; } = Array.Empty<EmailListItemViewModel>();
}
