namespace ClientRelationshipManagement.Web.Models.Documentation;

public sealed class DocumentationNavItemViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public bool IsCurrent { get; init; }
    public bool IsCurrentOrAncestor { get; init; }
    public IReadOnlyList<DocumentationNavItemViewModel> Children { get; init; } =
        Array.Empty<DocumentationNavItemViewModel>();
}
