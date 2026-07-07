using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClientRelationshipManagement.Web.Models.Emails;

public sealed class EmailsPageViewModel
{
    public string Notice { get; init; } = string.Empty;
    public string Search { get; init; } = string.Empty;
    public string StateFilter { get; init; } = string.Empty;
    public IReadOnlyList<SelectListItem> StateOptions { get; init; } = Array.Empty<SelectListItem>();
    public int TotalEmails { get; init; }
    public int DraftEmails { get; init; }
    public int ApprovedEmails { get; init; }
    public int SentEmails { get; init; }
    public IReadOnlyList<EmailListItemViewModel> Emails { get; init; } = Array.Empty<EmailListItemViewModel>();
}
